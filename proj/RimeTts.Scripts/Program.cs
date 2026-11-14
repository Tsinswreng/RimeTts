namespace RimeTts.Scripts;

using System.Runtime.CompilerServices;
using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

[Doc($$"""
子命令共用的執行上下文。

*為何要有它*：每個子命令都要「找到專案、知道 TFM、知道根目錄」。
讓 dispatcher 統一建立並填入這些固定事實，子命令就不必各自解析路徑
（那會讓「從哪個目錄執行」變成隱含依賴）。
""")]
public interface ISCtx{

	[Doc("倉庫根目錄（`RimeTts/`），各命令以此定位 `proj/` 下的專案。")]
	public Pth RootDir{get;set;}

	[Doc($$"""
	目標框架（TFM）。與 `proj/Directory.Build.props` 的 `TargetFramework` 同步，
	決定建置／發布輸出目錄名（如 `net11.0-windows`）。升級 TFM 時此處要同步改。
	""")]
	public str Tfm{get;set;}
}

[Doc($$"""
{{nameof(ISCtx)}} 的實現。
*為何 TFM 默認空串*：dispatcher 建立時必定覆蓋；留空是為了「忘了填」時一眼看出來。
""")]
public class SCtx : ISCtx{

	[Doc("倉庫根目錄。")]
	public Pth RootDir{get;set;}

	[Doc("目標框架；dispatcher 建立時覆蓋。")]
	public str Tfm{get;set;} = "";
}

[Doc($$"""
倉庫維護腳本的命令列入口。

*為何是「一個 dispatcher ＋ 多個具名腳本」*：每個腳本要能獨立跑、獨立讀，
而入口只有一個（`Program.Main`）。新命令在 switch 與 {{nameof(PrintUsage)}} 各加一行。
""")]
internal static partial class Program{

	[Doc("將命令列入口分派至具名腳本；沒有引數時印用法。")]
	internal static async Task Main(str[] Args){
		var Ct = default(CT);
		// Program.cs 位於 <根>/proj/RimeTts.Scripts；上推兩級得到倉庫根。
		var Root = FullPath(DirName(OwnPath())/"../..");

		// step 1: 建立統一的子命令上下文。
		ISCtx Ctx = new SCtx{
			RootDir = Root,
			// 與 proj/Directory.Build.props 的 TargetFramework 同步。
			Tfm = "net11.0-windows",
		};

		if(Args.Length == 0){
			PrintUsage();
			return;
		}

		// step 2: 依首個引數分派。
		switch(Args[0]){
			case nameof(Test):
				await Test.Main(Ctx, Ct);
				break;
			case nameof(TestAotWin):
				await TestAotWin.Main(Ctx, Ct);
				break;
			case nameof(GenAppJsonCtx):
				await GenAppJsonCtx.Main(Ctx, Ct);
				break;
			default:
				throw new ArgumentException($"Unknown RimeTts script: {Args[0]}.", nameof(Args));
		}
	}

	[Doc("列出可由 `dotnet run -- <entry>` 呼叫的腳本名稱。")]
	static void PrintUsage(){
		Console.Error.WriteLine("Usage: dotnet run --project proj/RimeTts.Scripts -- <entry>");
		Console.Error.WriteLine("Entries: Test, TestAotWin, GenAppJsonCtx");
		Console.Error.WriteLine("GenAppJsonCtx：改過 DTO 之後跑一次，重寫 Tsinswreng.SrcGen/RimeTts.Gen.g.cs。");
	}

	[Doc("讓編譯器提供本源碼路徑，故腳本不依賴啟動時的當前目錄。")]
	static str OwnPath([CallerFilePath] str CallerPath = ""){
		return CallerPath;
	}
}
