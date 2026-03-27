using Tsinswreng.CsCore;

namespace RimeTts;


[Doc(@$"句子劃分配置")]
public interface IOptSentenceSeg{
	[Doc(@$"無提交間隔（毫秒）
	自從最後一次收到{nameof(IDtoCommit)}後
	若超過此時間則認爲發生了一次句子分段
	默認5秒
	")]
	public i64 NoCommitGapMs{get;set;}

	[Doc(@$"成句終止符列表
	當上屏詞的最後一個字符是這些字符之一時，立即成句
	如果為空，則只根據時間間隔成句
	")]
	public List<str> SentenceTerminators{get;set;}
}
