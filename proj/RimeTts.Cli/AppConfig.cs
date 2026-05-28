using RimeTts;

namespace RimeTts.Cli;

public sealed class AppConfig{
	public FileInteractorSection FileInteractor{get;set;} = new();
	public SentenceSegSection SentenceSeg{get;set;} = new();
	public TranslatorSection Translator{get;set;} = new();
	public TtsSection Tts{get;set;} = new();
	public LanguagePipelineSection LanguagePipeline{get;set;} = new();
}

public sealed class FileInteractorSection{
	public str ContentFile{get;set;} = "";
	public str SignalFile{get;set;} = "";
}

public sealed class SentenceSegSection{
	public i64 NoCommitGapMs{get;set;} = 5000;
}

public sealed class TranslatorSection{
	public str ApiKey{get;set;} = "";
	public str BaseUrl{get;set;} = "https://api.openai.com/v1/chat/completions";
	public str Model{get;set;} = "gpt-4o-mini";
	public i32 TimeoutSec{get;set;} = 20;
	public str DefaultSystemPrompt{get;set;} = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: en
Translation: Translated Text
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""";
}

public sealed class TtsSection{
	public str OutputDir{get;set;} = "";
	public List<str> Engines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}

public sealed class LanguagePipelineSection{
	public List<LanguageProfileSection> Languages{get;set;} = new(){
		new LanguageProfileSection{
			Language = "en",
			SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: en
Translation: Translated Text
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfileSection{
			Language = "ja",
			SystemPrompt = """
あなたは翻訳者です。
YAML のみを返してください:
UserInputLang: zh
TargetLang: ja
Translation: 翻訳文
翻訳できない場合は Translation を null にしてください。
Markdown のコードフェンスは付けないでください。
""",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfileSection{
			Language = "es",
			SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: es
Translation: Texto traducido
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfileSection{
			Language = "it",
			SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: it
Translation: Testo tradotto
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfileSection{
			Language = "fr",
			SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: fr
Translation: Texte traduit
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
	};
}

public sealed class LanguageProfileSection{
	public str Language{get;set;} = "en";
	public str SystemPrompt{get;set;} = "";
	public List<str> TtsEngines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}
