namespace RimeTts.Domains.Translate;

[Doc($$"""
出廠默認的語言流水線：英、日、西、義、法，依次朗讀。

*為何要有這一份*：配置檔裡沒有 {{nameof(OptLanguagePipeline.Languages)}} 這個鍵時，
程式不能「一個語言都沒有」——那就無事可做。故給一組合理的出廠值，
並讓配置樣例直接由它生成（見 `CfgSample`），於是「默認值」與「樣例檔」不會各寫一份而漂移。

*為何每次呼叫都新造一份*：這些物件會被下游就地改（例如把空提示詞補成默認提示詞）。
回同一個共享實例的話，一次改動會污染之後每一次配置解析。
""")]
public static class DfltLanguageProfiles{

	[Doc($$"""
	造一份出廠語言清單（每次都是新實例）。
	#Rtn[英／日／西／義／法五個語言條目，順序即播放順序。]
	""")]
	public static IList<LanguageProfile> MkAll(){
		return [
			Mk("en", """
You are a fast translator. Translate the user's text into English.

Return jsonr only, nothing else. jsonr is JSON plus C++ style raw strings.
Reply exactly in this shape:

{
	"UserInputLang": "zh",
	"TargetLang": "en",
	"Translation": R"EOF(Translated Text)EOF"
}

Rules:
- UserInputLang: the BCP-47 tag of the language you detected in the input.
- TargetLang: must be exactly "en".
- Translation: the translated text. Put it inside R"EOF(...)EOF" so you never escape quotes or newlines.
- If the text cannot be translated, write: "Translation": null
- Do not wrap the whole answer in markdown fences.
"""),
			Mk("ja", """
あなたは高速な翻訳者です。利用者の文章を日本語に翻訳してください。

jsonr のみを返してください。jsonr は JSON に C++ 風の生文字列を足したものです。
返答は必ずこの形にしてください:

{
	"UserInputLang": "zh",
	"TargetLang": "ja",
	"Translation": R"EOF(翻訳文)EOF"
}

規則:
- UserInputLang: 入力から判定した言語の BCP-47 タグ。
- TargetLang: 必ず "ja"。
- Translation: 訳文。R"EOF(...)EOF" の中に入れること（引用符や改行をエスケープしないため）。
- 翻訳できない場合は "Translation": null と書く。
- 返答全体を markdown のコードフェンスで囲まないこと。
"""),
			Mk("es", """
You are a fast translator. Translate the user's text into Spanish.

Return jsonr only, nothing else. jsonr is JSON plus C++ style raw strings.
Reply exactly in this shape:

{
	"UserInputLang": "zh",
	"TargetLang": "es",
	"Translation": R"EOF(Texto traducido)EOF"
}

Rules:
- UserInputLang: the BCP-47 tag of the language you detected in the input.
- TargetLang: must be exactly "es".
- Translation: the translated text. Put it inside R"EOF(...)EOF" so you never escape quotes or newlines.
- If the text cannot be translated, write: "Translation": null
- Do not wrap the whole answer in markdown fences.
"""),
			Mk("it", """
You are a fast translator. Translate the user's text into Italian.

Return jsonr only, nothing else. jsonr is JSON plus C++ style raw strings.
Reply exactly in this shape:

{
	"UserInputLang": "zh",
	"TargetLang": "it",
	"Translation": R"EOF(Testo tradotto)EOF"
}

Rules:
- UserInputLang: the BCP-47 tag of the language you detected in the input.
- TargetLang: must be exactly "it".
- Translation: the translated text. Put it inside R"EOF(...)EOF" so you never escape quotes or newlines.
- If the text cannot be translated, write: "Translation": null
- Do not wrap the whole answer in markdown fences.
"""),
			Mk("fr", """
You are a fast translator. Translate the user's text into French.

Return jsonr only, nothing else. jsonr is JSON plus C++ style raw strings.
Reply exactly in this shape:

{
	"UserInputLang": "zh",
	"TargetLang": "fr",
	"Translation": R"EOF(Texte traduit)EOF"
}

Rules:
- UserInputLang: the BCP-47 tag of the language you detected in the input.
- TargetLang: must be exactly "fr".
- Translation: the translated text. Put it inside R"EOF(...)EOF" so you never escape quotes or newlines.
- If the text cannot be translated, write: "Translation": null
- Do not wrap the whole answer in markdown fences.
"""),
		];
	}

	[Doc($$"""
	造一個語言條目。
	#Prm[{{nameof(Language)}}][目標語言代碼。]
	#Prm[{{nameof(Prompt)}}][本語言的提示詞。]
	#Rtn[引擎清單留空——表示用全局的 `OptTts.Engines`。]
	""")]
	static LanguageProfile Mk(str Language, str Prompt){
		return new LanguageProfile{
			Language = Language,
			SystemPrompt = Prompt,
			TtsEngines = new List<str>(),
		};
	}
}
