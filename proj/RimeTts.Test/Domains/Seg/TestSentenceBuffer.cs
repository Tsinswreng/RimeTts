using RimeTts.Domains.Seg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Seg;

[Doc($$"""
本分片測 {{nameof(SentenceBuffer)}} 的三個成員：{{nameof(SentenceBuffer.Append)}}、
{{nameof(SentenceBuffer.TryFlushByIdle)}}、{{nameof(SentenceBuffer.Clear)}}。
""")]
public partial class TestSentenceBuffer{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterBuffer();

	[Doc("上屏以終止符結尾時，整段累積內容立刻成句。")]
	public partial Task<nil> Append_TerminatorFlushes(obj? O);

	[Doc("沒有終止符時不成句，只是累積。")]
	public partial Task<nil> Append_WithoutTerminatorAccumulates(obj? O);

	[Doc("閒置超過配置的間隔才成句；不到間隔時回 null。")]
	public partial Task<nil> Idle_FlushesOnlyAfterGap(obj? O);

	[Doc("成句之後緩衝要清空，下一句不該帶上上一句。")]
	public partial Task<nil> Flush_ClearsBuffer(obj? O);

	[Doc("空上屏與純空白被忽略，不影響累積。")]
	public partial Task<nil> Append_IgnoresBlank(obj? O);

	[Doc("未配置終止符時（預設情況），即使上屏帶句號也不成句——只按間隔斷句。")]
	public partial Task<nil> Append_NoTerminatorsNeverFlushesOnPunctuation(obj? O);
}
