using RimeTts;

namespace RimeTts.Cli;

public sealed class AppConfig{
	public FileInteractorSection FileInteractor{get;set;} = new();
	public SentenceSegSection SentenceSeg{get;set;} = new();
	public TranslatorSection Translator{get;set;} = new();
	public TtsSection Tts{get;set;} = new();
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
	public str SystemPrompt{get;set;} = "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text.";
}

public sealed class TtsSection{
	public str PythonDllPath{get;set;} = "";
	public str OutputDir{get;set;} = "";
}
