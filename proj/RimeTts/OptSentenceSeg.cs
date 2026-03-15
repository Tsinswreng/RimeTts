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
}
