using Tsinswreng.CsCore;

namespace RimeTts;

public interface IRespTranslate{

}

public interface IReqTranslate{

}

[Doc(@$"翻譯服務")]
public interface ITranslator{
	[Doc(@$"異步翻譯器 默認中譯英
	默認實現應使用能快速響應的LLM來做 不要傳統機器翻譯
	")]
	public Task<IRespTranslate> Translate(
		IReqTranslate Req, CT Ct
	);
}
