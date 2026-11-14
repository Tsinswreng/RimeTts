namespace RimeTts.Infra.Log;

using Microsoft.Extensions.Logging;

[Doc($$"""
本應用自訂的「重要日誌」事件編號。

*為何用 {{nameof(EventId)}} 而不是自訂 state 型別來標記*：日誌會穿過
{{nameof(AppLogLoggerProvider)}} 的分類包裝，那個包裝會把 state 重新排版成字串
（前綴分類名）。要是「重不重要」記在 state 的型別上，過一層就丟了；
{{nameof(EventId)}} 會被原樣轉發，故它是唯一能穿過包裝的標記。

*編號規則*：>= {{nameof(ImportantFrom)}} 就是重要日誌——那些使用者當場要看、
而且事後要能從檔案裡查回來的行（上屏詞、成句、譯文）。其餘一律只進檔案。
""")]
public static class RimeLogEvents{

	[Doc("重要日誌的起始編號。{{nameof(TieredLogger)}} 以此判斷要不要印到主控台。")]
	public const i32 ImportantFrom = 1000;

	[Doc("使用者這一下上屏了什麼字。")]
	public const i32 Commit = 1001;

	[Doc("累積成了一句，準備送去翻譯。")]
	public const i32 Sentence = 1002;

	[Doc("某語言的譯文拿到了。")]
	public const i32 Translation = 1003;

	[Doc("譯文命中快取（沒有真的發請求）。")]
	public const i32 TranslationCache = 1004;
}

[Doc($$"""
重要日誌的 state。

*為何不用匿名物件或字串*：主控台要分段上色（標籤一段、正文一段），
而「標籤」是資料不是格式；把它們拆成兩個欄位，{{nameof(ConsoleLogger)}} 才不必猜怎麼切字串。
""")]
public readonly record struct RimeLogState{

	[Doc("行首的方括號標籤，例如 `[上屏詞]`。")]
	public str Tag{get;init;}

	[Doc("正文（使用者輸入的字、譯文等）。")]
	public str Text{get;init;}
}

[Doc($$"""
重要日誌的便捷寫法。

*與 {{nameof(AppLogExtn)}} 的分工*：那一個是「應用的一般日誌」（啟動、配置、錯誤），
走 `EventId = 0`，只進檔案；本類這幾條是使用者當場要看的，會同時進主控台。

#Eg[
	```cs
	Log.LogCommitText(Commit.Text);
	Log.LogSentenceText(Sentence);
	Log.LogTranslationText("ja", Translated);
	```
]
""")]
public static class RimeLogExtn{

	[Doc("記錄一次上屏（使用者打出的字）。走 {{nameof(RimeLogEvents.Commit)}}，會印到主控台。")]
	public static void LogCommitText(this ILogger z, str Text){
		Write(z, RimeLogEvents.Commit, "[上屏詞]", Text);
	}

	[Doc("記錄一次成句。走 {{nameof(RimeLogEvents.Sentence)}}，會印到主控台。")]
	public static void LogSentenceText(this ILogger z, str Text){
		Write(z, RimeLogEvents.Sentence, "[成句]", Text);
	}

	[Doc("記錄某語言的譯文。走 {{nameof(RimeLogEvents.Translation)}}，會印到主控台。")]
	public static void LogTranslationText(this ILogger z, str Lang, str Text){
		Write(z, RimeLogEvents.Translation, $"[{Lang}]", Text);
	}

	[Doc("記錄「這條譯文來自快取」。與真的翻譯分開編號，才看得出延遲是不是快取救的。")]
	public static void LogTranslationCacheText(this ILogger z, str Lang, str Text){
		Write(z, RimeLogEvents.TranslationCache, $"[{Lang}][Cache]", Text);
	}

	[Doc("所有重要日誌的唯一出口：要改格式只改這裡。")]
	static void Write(ILogger z, i32 EventId, str Tag, str Text){
		z.Log(
			LogLevel.Information
			,new EventId(EventId, Tag)
			,new RimeLogState{ Tag = Tag, Text = Text }
			,null
			,static (S, _) => $"{S.Tag} {S.Text}"
		);
	}
}
