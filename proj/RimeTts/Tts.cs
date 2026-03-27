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
}
