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
}

public sealed class RespTranslate:IRespTranslate{
	public str SourceText{get;set;} = "";
	public str TranslatedText{get;set;} = "";
}

public sealed class ReqGenEtPlay:IReqGenEtPlay{
	public str Text{get;set;} = "";
	public str Language{get;set;} = "en";
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
}

public sealed class OptTranslator{
	public str ApiKey{get;set;} = "";
	public str BaseUrl{get;set;} = "https://api.openai.com/v1/chat/completions";
	public str Model{get;set;} = "gpt-4o-mini";
	public i32 TimeoutSec{get;set;} = 20;
	public str SystemPrompt{get;set;} = "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text.";
}

public sealed class OptTts{
	public str OutputDir{get;set;} = Path.Combine(AppContext.BaseDirectory, "tts-output");
	public List<str> Engines{get;set;} = new(){ "gTTS", "SystemSpeech" };
}
