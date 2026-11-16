using RimeTts.Cfg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Cfg;

[Doc($$"""
配置映射的測試類主檔：只串起各分片。

被測對象是 {{nameof(Tsinswreng.CsCfg.ICfgReader)}} → 選項物件的映射，
故每個用例自己用一段 jsonr 文本造來源（純資料，沒有共享狀態）。
""")]
public partial class TestCfg : Tester{

	[Doc("掛上各分片；分片自己往本測試類的節點上掛用例。")]
	public override void Init(){
		RegisterRead();
		RegisterSample();
	}
}
