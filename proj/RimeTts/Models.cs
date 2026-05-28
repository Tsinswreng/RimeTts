using System.IO;

namespace RimeTts;

public sealed class DtoCommit:ILuaDtoCommit, IDtoCommit{
	public str Type{get;set;} = ELuaDtoType.Commit.ToString();
	public str Text{get;set;} = "";
}

public sealed class DtoKeyEvent:ILuaDtoKeyEvent, IDtoKeyEvent{
	public str Type{get;set;} = ELuaDtoType.KeyEvent.ToString();
}

public sealed class Sentence:ISentence{
	public str Text{get;set;} = "";
}

public sealed class ReqTranslate:IReqTranslate{
	public str SourceText{get;set;} = "";
	public str TargetLanguage{get;set;} = "en";
	public str SystemPrompt{get;set;} = "";
}

public sealed class RespTranslate:IRespTranslate{
	public str SourceText{get;set;} = "";
	public str TargetLanguage{get;set;} = "en";
	public str? TranslatedText{get;set;} = "";
}

public sealed class YamlTranslateResp{
	public str? UserInputLang{get;set;}
	public str? TargetLang{get;set;}
	public str? Translation{get;set;}
}

public sealed class ReqGenEtPlay:IReqGenEtPlay{
	public str Text{get;set;} = "";
	public str Language{get;set;} = "en";
	public List<str>? PreferredEngines{get;set;}
}

public sealed class PlayState:IPlayState{
	public str AudioFile{get;set;} = "";
	public DateTimeOffset StartedAtUtc{get;set;} = DateTimeOffset.UtcNow;
	public DateTimeOffset EndedAtUtc{get;set;} = DateTimeOffset.UtcNow;
}

public sealed class OptFileInteractor:IOptFileInteractor{
	public str ContentFile{get;set;} = "";
	public str SignalFile{get;set;} = "";
}

public sealed class OptSentenceSeg:IOptSentenceSeg{
	public i64 NoCommitGapMs{get;set;} = 5000;
	public List<str> SentenceTerminators{get;set;} = new();
}

public sealed class OptTranslator{
	public str ApiKey{get;set;} = "";
	public str BaseUrl{get;set;} = "https://api.openai.com/v1/chat/completions";
	public str Model{get;set;} = "gpt-4o-mini";
	public i32 TimeoutSec{get;set;} = 20;
	public str DefaultSystemPrompt{get;set;} = "You are a fast translator. Return YAML only with keys UserInputLang, TargetLang, Translation. UserInputLang and TargetLang must be BCP-47 language tags. If you cannot translate, set Translation to null. Do not wrap the YAML in markdown fences.";
}

public sealed class OptTts{
	public str OutputDir{get;set;} = Path.Combine(AppContext.BaseDirectory, "tts-output");
	public List<str> Engines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}

public interface IOptLanguagePipeline{
	public List<ILanguageProfile> Languages{get;set;}
}

public interface ILanguageProfile{
	public str Language{get;set;}
	public str SystemPrompt{get;set;}
	public List<str> TtsEngines{get;set;}
}

public sealed class OptLanguagePipeline:IOptLanguagePipeline{
	public List<ILanguageProfile> Languages{get;set;} = new List<ILanguageProfile>{
		new LanguageProfile{
			Language = "en",
			SystemPrompt = "",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfile{
			Language = "ja",
			SystemPrompt = "",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfile{
			Language = "es",
			SystemPrompt = "",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfile{
			Language = "it",
			SystemPrompt = "",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
		new LanguageProfile{
			Language = "fr",
			SystemPrompt = "",
			TtsEngines = new(){ "gTTS", "SystemSpeech" },
		},
	};
}

public sealed class LanguageProfile:ILanguageProfile{
	public str Language{get;set;} = "en";
	public str SystemPrompt{get;set;} = "";
	public List<str> TtsEngines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}
