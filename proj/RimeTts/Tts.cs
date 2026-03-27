using Tsinswreng.CsCore;

namespace RimeTts;

public interface IPlayState{
	public str AudioFile{get;set;}
	public DateTimeOffset StartedAtUtc{get;set;}
	public DateTimeOffset EndedAtUtc{get;set;}
}

public interface IReqGenEtPlay{
	public str Text{get;set;}
	public str Language{get;set;}
	public List<str>? PreferredEngines{get;set;}
}


[Doc(@$"文本轉語音服務")]
public interface ITts{

	[Doc(@$"生成並播放語音")]
	public Task<IPlayState> GenEtPlay(
		IReqGenEtPlay Req, CT Ct
	);

	[Doc(@$"只生成語音文件，返回文件路徑")]
	public Task<str> GenerateAudio(
		IReqGenEtPlay Req, CT Ct
	);

	[Doc(@$"播放指定的音頻文件")]
	public Task PlayAudio(
		str AudioFile, CT Ct
	);
}
