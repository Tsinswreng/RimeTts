using RimeTts.Domains.Seg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Seg;

[Doc($$"""
{{nameof(SentenceBuffer)}} 的測試類主檔：只取被測對象、串起各分片。

被測對象是純邏輯（時間由假時鐘提供），故不經容器。
""")]
public partial class TestSentenceBuffer : Tester{

	[Doc("掛上各分片。")]
	public override void Init(){
		RegisterBuffer();
	}
}
