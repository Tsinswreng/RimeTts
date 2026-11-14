namespace RimeTts.Scripts;

using Tsinswreng.CsSh;

[Doc($$"""
發布 Windows 原生（NativeAOT）成品目錄，給本機試跑、給別人拷走。

*與 {{nameof(TestAotWin)}} 的分工*：那一個是「AOT 之後測試還過不過」的自檢，
跑完就丟；本命令產出的是**要交出去的那一份**：一個自包含目錄，
目標機器不必裝 .NET runtime，也不必裝 RimeTts 的任何依賴。

*為何發布的是宿主而不是業務程序集*：業務程序集是類庫，編不成 exe。
入口是 `proj/RimeTts.Cli`，它的 `-p:AotPub=true` 只打開這一個專案的 AOT
（理由見該 csproj 的條件區塊）。

*已知限制：離線 TTS（SAPI）在 AOT 下很可能不可用*。ILC 會明確報告
`System.Speech` 的三個 SAPI COM 包裝類「建構子永遠拋（Invalid IL or CLR metadata）」——
那是 COM 類型在 AOT 下沒有運行期生成的元資料所致。線上那條腿（gTTS）不受影響，
故 AOT 版仍可用，只是「網絡斷了就退回本機語音」這個保險在 AOT 版上不成立。
要不要為 AOT 版把 SAPI 從默認引擎清單拿掉，等用戶決定。

用法：
```sh
dotnet run --project proj/RimeTts.Scripts -- PubWinAot
dotnet run --project proj/RimeTts.Scripts -- PubWinAot -Out D:\tmp\rimetts
dotnet run --project proj/RimeTts.Scripts -- PubWinAot -NoAot
dotnet run --project proj/RimeTts.Scripts -- PubWinAot -DryRun
```
""")]
internal static partial class PubWinAot{

	[Doc("執行：發布 → 驗產物 → 印出目錄與大小。")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);

	[Doc($$"""
	解析命令列旗標；未知旗標直接報錯，避免「以為加了參數其實沒生效」。
	#Prm[{{nameof(Ctx)}}][腳本上下文；`Args` 是命令名之後的其餘引數。]
	""")]
	private static partial OptPub ParseOpts(ISCtx Ctx);

	[Doc($$"""
	執行 `dotnet publish`。
	#Prm[{{nameof(Ctx)}}][腳本上下文。]
	#Prm[{{nameof(Opts)}}][發布選項。]
	""")]
	private static partial Task<nil> Publish(ISCtx Ctx, OptPub Opts, CT Ct);

	[Doc($$"""
	檢查產物存在並印出目錄大小；缺 exe 視為失敗（避免「發布成功但其實沒東西」）。
	#Prm[{{nameof(Opts)}}][發布選項。]
	""")]
	private static partial Task<nil> Verify(OptPub Opts);
}

[Doc($$"""
{{nameof(PubWinAot)}} 的選項。
""")]
internal sealed partial class OptPub{

	[Doc(@"產物目錄。空串＝不傳 `-o`，由 SDK 放到它自己的 publish 目錄
	（`proj/RimeTts.Cli/bin/Release/net11.0-windows/win-x64/publish`）；
	只有明確要帶去別處時才給這個參數。")]
	public str OutDir{get;set;} = "";

	[Doc("true 時走 framework-dependent（不編原生碼）：發布快、產物小，但目標機器要裝 .NET 11 runtime。")]
	public bool NoAot{get;set;}

	[Doc("true 時只印出將要執行的命令，不真的發布。")]
	public bool DryRun{get;set;}
}
