namespace RimeTts;

[Doc($$"""
本應用的異常標籤——只宣告「例外情況」。

*約定：不帶標籤 ＝ 公開可見的業務異常*。`ErrNode.Mk` 不加任何東西就表示「可示予用戶」。
故這裡不提供 BizErr／Public 之類的「正向」標籤——那會讓最常見的情況反而要多說一句。

本專案是命令列工具，沒有前端翻文案，故標籤目前只有一個實際用途：
{{nameof(Private)}} 表示「訊息含內部細節，不該原樣印給使用者看」，印到主控台時只說一句通用話。
""")]
public static class AppErrTags {
	[Doc("系統異常：檔案、網絡、外部服務等。對外只給一句通用提示。")]
	public static str SysErr = nameof(SysErr);

	[Doc("不可示予用戶：詳情只進日誌。沒標這個就是默認可示人。")]
	public static str Private = nameof(Private);
}
