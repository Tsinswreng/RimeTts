using RimeTts.Domains.Pipeline;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Pipeline;

[Doc($$"""
`RimeTtsPipeline` 的測試類主檔：只串起各分片。

*為何值得單獨測*：殭屍態事故（三循環被 `WhenAll` 等着、一死全卡）的防線在這一層；
本測試類驗證「任一循環先退會把整條流水線帶停（而不是掛着）」與正常的端到端順序。
""")]
public partial class TestRimeTtsPipeline : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterPipeline();
	}
}

[Doc($$"""
本分片測 `RimeTtsPipeline`：端到端順序與循環監視器。
""")]
public partial class TestRimeTtsPipeline{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterPipeline();

	[Doc("上屏累積、成句（終止符）、按語言順序播放——最小端到端。")]
	public partial Task<nil> EndToEnd_PlaysInLanguageOrder(obj? O);

	[Doc($$"""
	全局取消後 {{nameof(RimeTts.Domains.Pipeline.ISvcRimeTts.RunAsync)}} 必須在有界時間內返回
	（三循環一起收工、監聽被停）——殭屍態（掛着不返回）的行為學守門員。
	""")]
	public partial Task<nil> DeadConsumerLoop_TearsDownWholePipeline(obj? O);
}
