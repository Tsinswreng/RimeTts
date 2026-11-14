namespace RimeTts.Domains.Translate;

[Doc($$"""
出廠默認的語言流水線：英、日、西、義、法，依次朗讀。

*為何要有這一份*：配置檔裡沒有 {{nameof(OptLanguagePipeline.Languages)}} 這個鍵時，
程式不能「一個語言都沒有」——那就無事可做。故給一組合理的出廠值，
並讓配置樣例直接由它生成（見 `CfgSample`），於是「默認值」與「樣例檔」不會各寫一份而漂移。

*為何提示詞都走 {{nameof(LlmPrompts)}}*：格式規則（回 jsonr、隨機定界詞、翻不了寫 null）
只能有一份。逐語言手抄六份的結果就是改一條規則要改六處，漏一處就某個語言行為不一致。
英文與日文的規則段各自完整（用該語言寫），其餘語言共用英文規則段。

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
			// 英文那份與兜底提示詞是同一個常量：兩者本來就是同一段文字，沒有理由各寫一份。
			Mk("en", OptTranslator.DfltSystemPrompt),
			Mk("ja", LlmPrompts.MkJa()),
			Mk("es", LlmPrompts.Mk(
				"You are a fast translator. Translate the user's text into Spanish."
				,"es"
				,"Texto traducido"
			)),
			Mk("it", LlmPrompts.Mk(
				"You are a fast translator. Translate the user's text into Italian."
				,"it"
				,"Testo tradotto"
			)),
			Mk("fr", LlmPrompts.Mk(
				"You are a fast translator. Translate the user's text into French."
				,"fr"
				,"Texte traduit"
			)),
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
