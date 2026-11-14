namespace RimeTts.Domains.Translate;

[Doc($$"""
語言流水線裡的一個語言：要翻成哪種語言、用什麼提示詞、用哪些 TTS 引擎。

*為何提示詞逐語言配置而不是一份共用的*：翻成日語時用日文寫提示詞、翻成西語時用西語寫，
模型對「用目標語言描述的任務」明顯更穩；而且不同語言要強調的點不一樣
（例如日語要保留敬語語氣、英語要口語化）。所以提示詞的擁有者是語言條目，不是全局。

*為何 TTS 引擎也逐語言*：本機不一定每個語言都有系統語音。英語可以兩條腿都試，
日語可能只有 gTTS 可用；把引擎清單放在語言條目上，就不必為「某語言缺語音」寫特判。
""")]
public class LanguageProfile{

	[Doc("目標語言代碼（BCP-47 的兩字母部分即可，如 `en`／`ja`）。空串＝這條配置無效，會被丟掉。")]
	public str Language{get;set;} = "en";

	[Doc("本語言的翻譯提示詞。空串＝用 OptTranslator.DefaultSystemPrompt。")]
	public str SystemPrompt{get;set;} = "";

	[Doc($$"""
	本語言的 TTS 引擎優先級，越前越優先。
	空清單＝用全局的 `OptTts.Engines`。
	""")]
	public IList<str> TtsEngines{get;set;} = new List<str>();
}

[Doc($$"""
語言流水線：一句話要依次翻成哪幾種語言、並依次朗讀。

*順序即播放順序*：列表從上到下就是「先讀英語、再讀日語…」。
把順序寫在資料裡而不是代碼裡，是因為「先英後日」或「只讀英」是使用習慣問題，不是邏輯問題。

*為何不是字典*：字典的鍵序不保證，而這個清單的順序就是語義的一部分。
""")]
public class OptLanguagePipeline{

	[Doc("語言條目，順序即翻譯與播放順序。空清單＝退化為只翻英文（見 FastLlmTranslator 的兜底）。")]
	public IList<LanguageProfile> Languages{get;set;} = DfltLanguageProfiles.MkAll();
}
