namespace RimeTts.Cli;

[Doc($$"""
Windows 命令列入口。

*本類只有四步*：確保配置存在 → 建容器 → 交給 {{nameof(Di)}} → 跑到取消。
業務邏輯一行都不放這裡：那些屬於業務程序集與 {{nameof(DiCli)}}。

*為何沒有 `Microsoft.Extensions.Hosting`*：Hosting 帶來的是「多個背景服務、
優雅停止、配置熱重載、DI 作用域」這些機制；本進程只有一條流水線、一個容器，
用 Hosting 等於為了它的規矩（例如不能在 StartAsync 裡阻塞）多寫一層殼。
一條 `RunAsync(CT)` 就是全部的生命週期。
""")]
public static partial class Program{

	[Doc($$"""
	進程入口。
	#Prm[{{nameof(Args)}}][目前不使用：本工具沒有命令列參數。保留參數以便日後加 `--check` 之類。]
	#Rtn[退出碼：0＝正常；1＝配置或啟動失敗；2＝非 Windows。]
	""")]
	public static partial i32 Main(str[] Args);

	[Doc($$"""
	組裝依賴注入容器。
	*必須在取出任何服務之前*完成並交給 {{nameof(Di)}}，否則日誌的延遲路徑解析取不到儲存區。
	#Rtn[建好的服務提供源。]
	""")]
	internal static partial IServiceProvider MkServices();

	[Doc($$"""
	真正的異步入口：建容器、跑流水線、把失敗翻譯成一句人話。
	#Rtn[退出碼。]
	""")]
	internal static partial Task<i32> RunAsync();

	[Doc($$"""
	把異常印成使用者看得懂的一行。
	*為何要分「可示人」與「不可示人」*：標了 {{nameof(AppErrTags.SysErr)}}／{{nameof(AppErrTags.Private)}}
	的錯，訊息裡是內部細節（路徑、堆棧、HTTP 原文），只留給日誌；畫面上只說「請看日誌」。
	#Prm[{{nameof(Ex)}}][捕獲到的異常。]
	""")]
	internal static partial void PrintErr(Exception Ex);
}
