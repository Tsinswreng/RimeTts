using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace RimeTts.Scripts;

// PubWinAot 的流程實現。聲明見 PubWinAot.cs。
internal static partial class PubWinAot{

	[Doc("要發布的專案；相對於倉庫根。")]
	const str CliCsprojRelPath = "proj/RimeTts.Cli/RimeTts.Cli.csproj";

	[Doc(@"沒給 `-Out` 時的產物目錄：`dotnet publish` 的標準位置
	（`bin/Release/<tfm>/win-x64/publish`）。*不自己另開目錄*——產物放在建置輸出裡，
	要帶走的時候再由呼叫方用 `-Out` 指定。")]
	const str DfltPublishRelPath = "proj/RimeTts.Cli/bin/Release/net11.0-windows/win-x64/publish";

	[Doc("產物 exe 的檔名。")]
	const str ExeName = "RimeTts.Cli.exe";

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var Opts = ParseOpts(Ctx);
		// 解析一次目錄名，後面 Publish 與 Verify 共用同一個結果（否則 Verify 會拿到空串）。
		Opts.OutDir = ResolveOutDir(Ctx, Opts);
		await Publish(Ctx, Opts, Ct);
		if(Opts.DryRun){
			return;
		}
		await Verify(Opts);
	}

	private static partial OptPub ParseOpts(ISCtx Ctx){
		var Opts = new OptPub();
		// 逐個消費引數：接受 `-Flag` 與 `-Flag value` 兩種寫法，未知旗標直接拋。
		for(var I = 0; I < Ctx.Args.Count; I++){
			var Arg = Ctx.Args[I];
			switch(Arg){
				case "-NoAot":
					Opts.NoAot = true;
					break;
				case "-DryRun":
					Opts.DryRun = true;
					break;
				case "-Out":
					if(I + 1 >= Ctx.Args.Count){
						throw new ArgumentException("-Out 需要一個目錄參數。");
					}
					Opts.OutDir = Ctx.Args[++I];
					break;
				default:
					throw new ArgumentException($"PubWinAot 不認識的參數：{Arg}");
			}
		}
		return Opts;
	}

	private static async partial Task<nil> Publish(ISCtx Ctx, OptPub Opts, CT Ct){
		var Csproj = Ctx.RootDir/CliCsprojRelPath;

		var Args = new List<str>{
			"publish", Csproj
			,"-c", "Release"
			,"-r", "win-x64"
		};
		// 只有呼叫方明確指定時才用 -o；預設就讓 SDK 放到它自己的 publish 目錄。
		if(!str.IsNullOrWhiteSpace(Opts.OutDir)){
			Args.Add("-o");
			Args.Add(Opts.OutDir);
		}
		if(Opts.NoAot){
			// 逃生門：AOT 因為原生工具鏈或某個套件而編不過時，至少還能產出一份可跑的目錄。
			// 明確寫 false 而不是「不傳」——不傳會繼承 csproj 的條件，而那個條件由下面的屬性控制。
			Args.Add("-p:PublishAot=false");
		}else{
			// 只被 RimeTts.Cli.csproj 解讀的屬性：不能用 -p:PublishAot=true，
			// 那會傳染給被引用的 CsCore（netstandard2.0 目標不支援 AOT，NETSDK1207）。
			Args.Add("-p:AotPub=true");
		}

		if(Opts.DryRun){
			Echo("dry run: dotnet " + string.Join(' ', Args));
			return NIL;
		}
		await Exe("dotnet", Args, Ct);
		return NIL;
	}

	private static async partial Task<nil> Verify(OptPub Opts){
		var ExePath = System.IO.Path.Combine(Opts.OutDir, ExeName);
		if(!File.Exists(ExePath)){
			throw new InvalidOperationException($"發布完成但找不到 {ExePath}；請看上面的 publish 輸出。");
		}
		var Bytes = new DirectoryInfo(Opts.OutDir)
			.EnumerateFiles("*", SearchOption.AllDirectories)
			.Sum(F => F.Length);
		Echo($"PubWinAot: {ExePath}");
		Echo($"  directory: {Opts.OutDir}");
		Echo($"  size: {Bytes / 1024.0 / 1024.0:0.0} MiB");
		// 提醒而非代辦：程式首次啟動會自己生成樣例配置，我們不預先塞一份（免得看起來像已配置好）。
		Echo("  首次執行會在同目錄生成 rimetts.jsonr，填好 Translator.ApiKey 後再啟動。");
		await Task.CompletedTask;
		return NIL;
	}

	[Doc($$"""
	把產物目錄解析成絕對路徑：有 `-Out` 就用它，否則用 SDK 的預設 publish 目錄。

	*為何用 `Path.GetFullPath` 而不是 {{nameof(FullPath)}}*：`Pth` 的 `/` 拼接產生正斜線路徑，
	後面又要用 `Path.Combine` 拼檔名；不先正規化的話，印出來會是 `a/b\c.exe` 這種混用的樣子。
	`Path.GetFullPath` 會把分隔符統一成該平臺的寫法。
	""")]
	static str ResolveOutDir(ISCtx Ctx, OptPub Opts){
		var Raw = str.IsNullOrWhiteSpace(Opts.OutDir)
			? System.IO.Path.Combine(Ctx.RootDir.ToString(), DfltPublishRelPath)
			: Opts.OutDir;
		return System.IO.Path.GetFullPath(Raw);
	}
}
