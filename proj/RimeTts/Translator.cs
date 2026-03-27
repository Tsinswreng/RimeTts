using Tsinswreng.CsCore;

namespace RimeTts;

public interface IRespTranslate{
	[Doc(@$"原始輸入文本")]
	public str SourceText{get;set;}

	[Doc(@$"目標語言代碼，如 en / ja")]
	public str TargetLanguage{get;set;}

	[Doc(@$"翻譯後英文文本")]
	public str TranslatedText{get;set;}

}
public interface IReqTranslate{
	[Doc(@$"待翻譯原文，默認中文")]
	public str SourceText{get;set;}

	[Doc(@$"目標語言代碼，如 en / ja")]
	public str TargetLanguage{get;set;}

	[Doc(@$"本次翻譯使用的系統提示詞；空則使用默認提示詞")]
	public str SystemPrompt{get;set;}
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
