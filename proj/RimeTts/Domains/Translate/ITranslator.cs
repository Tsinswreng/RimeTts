namespace RimeTts.Domains.Translate;

using RimeTts.Infra.Json;

[Doc($$"""
一次翻譯請求。

*為何 {{nameof(SystemPrompt)}} 在請求上而不是在服務上*：它是逐語言配置的（見
{{nameof(LanguageProfile)}}），同一個翻譯器要為不同目標語言用不同提示詞。
""")]
public record ReqTranslate{

	[Doc("待翻譯原文。")]
	public str SourceText{get;set;} = "";

	[Doc("目標語言代碼（如 `en`／`ja`）。")]
	public str TargetLanguage{get;set;} = "en";

	[Doc("本次使用的系統提示詞；空串＝用 OptTranslator.DefaultSystemPrompt。")]
	public str SystemPrompt{get;set;} = "";
}

[Doc($$"""
一次翻譯的結果。

*為何 {{nameof(TranslatedText)}} 可為 null*：模型明確回了 `Translation: null`
（或回的目標語言不對）代表「這句話翻不了」。那是正常結果，不是錯誤——
流水線據此跳過這個語言，繼續下一個。
""")]
public record RespTranslate{

	[Doc("原始輸入文本（回填，便於日誌與測試比對）。")]
	public str SourceText{get;set;} = "";

	[Doc("歸一化後的目標語言代碼。")]
	public str TargetLanguage{get;set;} = "en";

	[Doc("譯文；無法翻譯時為 null。")]
	public str? TranslatedText{get;set;}
}

[Doc($$"""
翻譯服務的抽象。

*為何要抽象的兩層*：內層（{{nameof(ITranslator)}}）只回答「這句話翻成這個語言是什麼」；
它不知道有幾個語言要翻、也不知道要不要朗讀。流水線（`SentenceFanout`）才負責
「同時翻成 N 種語言、按配置順序朗讀」。這樣換翻譯後端（換模型、換協議、加本地模型）不動編排。
""")]
public interface ITranslator{

	[Doc($$"""
	把 {{nameof(ReqTranslate.SourceText)}} 翻成 {{nameof(ReqTranslate.TargetLanguage)}}。
	#Prm[{{nameof(Req)}}][翻譯請求。]
	#Rtn[譯文，或「翻不了」的 null。]
	#Throw[{{nameof(TypedErr)}}][密鑰缺失、HTTP 失敗、回應無法解析時。]
	""")]
	public Task<RespTranslate> Translate(ReqTranslate Req, CT Ct);
}

[Doc($$"""
LLM 回應內容的形狀——**我們自己用提示詞規定的格式**。

*為何這是強型別、而外面那層包絡不是*：OpenAI 風格的包絡（`choices`／`delta`／`output`）
是別人的協議，形狀多態；而 `content` 裡面的內容是我們自己在提示詞裡規定的，
就那三個鍵，故用強型別接收，由源生成保 AOT。

#Eg[
	```jsonr
	{
		"UserInputLang": "zh",
		"TargetLang": "en",
		"Translation": R"q7Zx(Translated Text)q7Zx"
	}
	```

*定界詞是模型每次自己造的*（理由見 {{nameof(LlmPrompts)}}）：生字串在遇到
`)定界詞"` 時收尾，故定界詞若寫死，譯文裡出現同一個字串就會被提前截斷。
本類只承接解析結果，不檢查定界詞——那是提示詞的責任。
""")]
public class ResLlm : IAppSerializable{

	[Doc("模型判定的輸入語言（BCP-47）。目前只記錄、不參與決策。")]
	public str? UserInputLang{get;set;}

	[Doc("模型回報的目標語言。必須與要求的一致，否則視為翻譯失敗。")]
	public str? TargetLang{get;set;}

	[Doc("譯文；模型表明翻不了時為 null。")]
	public str? Translation{get;set;}
}
