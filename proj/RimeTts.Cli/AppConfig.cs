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
	public str DefaultSystemPrompt{get;set;} = "You are a fast translator. Translate source text to target language only. Return only translation text.";
}

public sealed class TtsSection{
	public str OutputDir{get;set;} = "";
	public List<str> Engines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}

public sealed class LanguagePipelineSection{
	public List<LanguageProfileSection> Languages{get;set;} = new(){
		new LanguageProfileSection{
			Language = "en",
			SystemPrompt = "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text.",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfileSection{
			Language = "ja",
			SystemPrompt = "あなたは高速な翻訳者です。中国語を自然で簡潔な日本語に翻訳してください。翻訳結果の本文のみを返してください。",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
	};
}

public sealed class LanguageProfileSection{
	public str Language{get;set;} = "en";
	public str SystemPrompt{get;set;} = "";
	public List<str> TtsEngines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}
