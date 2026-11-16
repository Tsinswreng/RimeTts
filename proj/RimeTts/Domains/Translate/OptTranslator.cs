namespace RimeTts.Domains.Translate;

using RimeTts.Cfg;

[Doc($$"""
翻譯（LLM）設定。

*為何用 LLM 而不是傳統機器翻譯*：本工具要的是「一句中文 → 通順的英／日／西／義／法」，
而且回應要快。傳統 MT 的譯文品質在這種短句、口語場景上明顯吃虧，故默認走兼容
OpenAI 協議的接口，模型名可配置。

*為何超時在這裡而不在流水線*：內層知道「這一次請求從發出到拿回內容」花了多久；
外層只知道「這一句話從開始到現在」。超時屬於請求，故由本層的 {{nameof(TimeoutSec)}} 決定。
""")]
public class OptTranslator{

	[Doc("LLM 接口的密鑰。空串＝沒配置，呼叫前就會被擋下。")]
	public str ApiKey{get;set;} = "";

	[Doc($$"""
	兼容 OpenAI 協議的 chat completions 端點。
	*默認值的唯一出處是 {{nameof(KeysAppCfg.Translator.BaseUrl)}}*；本屬性中性空串（未映射佔位）。
	直接 new 只用於測試；空串會在第一次請求時就以明確異常失敗，不會悄悄繞路。
	""")]
	public str BaseUrl{get;set;} = "";

	[Doc($$"""
	模型名。
	*默認值的唯一出處是 {{nameof(KeysAppCfg.Translator.Model)}}*；本屬性中性空串。
	""")]
	public str Model{get;set;} = "";

	[Doc($$"""
	單次請求的超時秒數。

	*為何不共用 `HttpClient.Timeout`*：共用客戶端的超時是全域的，改一個請求的等待就得換一個客戶端；
	每次請求各自一個 `CancellationTokenSource` 才能讓「這一句超時、下一句照常」成立。
	*默認值（20）的唯一出處是 {{nameof(KeysAppCfg.Translator.TimeoutSec)}}*；本屬性中性 0，
	實際生效值取 `Max(3, TimeoutSec)`，故 0 等於 3 秒——足夠讓「忘了映射」在測試裡立刻暴露。
	""")]
	public i32 TimeoutSec{get;set;} = 0;

	[Doc($$"""
	譯文快取的最大條目數。

	*為何需要上限*：輸入法常重複打同一句話（改字、重打），不快取等於每次重付一次延遲；
	但只增不減的字典在長駐進程裡就是內存泄漏。超過上限時淘汰最久未用的那一條。
	`0` 表示不快取（這也是鍵默認值 256 之外一個合法的顯式選擇）。
	""")]
	public i32 CacheCapacity{get;set;} = 0;

	[Doc($$"""
	沒有語言條目時的兜底提示詞（語言條目本身沒寫提示詞時也用它）。

	*由我們規定輸出格式*：要求模型回 jsonr——JSON 加 raw string 文本塊，
	於是譯文裡有引號、換行都不必轉義，解析也不必容忍一堆 `\n` 與 `\"`。
	*這不是與鍵默認值的「雙寫」*：{{nameof(KeysAppCfg.Translator.DefaultSystemPrompt)}} 的鍵默認
	就是引用本類的 {{nameof(DfltSystemPrompt)}} 常量，兩者同一份文字、同一個出處。
	""")]
	public str DefaultSystemPrompt{get;set;} = DfltSystemPrompt;

	[Doc($$"""
	兜底提示詞的默認值。

	*文字本身不在這裡*：格式規則（回 jsonr、隨機定界詞、翻不了寫 null）只有一份，
	在 {{nameof(LlmPrompts.MkEn)}}；本欄位只是給鍵與選項一個穩定名字。
	*為何是 `static readonly` 而不是 `const`*：它由方法產生，而 `const` 只能是編譯期常量。
	""")]
	public static readonly str DfltSystemPrompt = LlmPrompts.MkEn();
}
