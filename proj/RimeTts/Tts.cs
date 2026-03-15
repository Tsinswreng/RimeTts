using Tsinswreng.CsCore;

namespace RimeTts;

public interface IPlayState{

}

public interface IReqGenEtPlay{

}


[Doc(@$"文本轉語音服務")]
public interface ITts{

	[Doc(@$"生成並播放語音")]
	public Task<IPlayState> GenEtPlay(
		IReqGenEtPlay Req, CT Ct
	);
}
