using RimeTts.Test.Domains.Cfg;
using RimeTts.Test.Domains.Pipeline;
using RimeTts.Test.Domains.Seg;
using RimeTts.Test.Domains.Translate;
using RimeTts.Test.Domains.Tts;
using RimeTts.Test.Domains.Typing;
namespace RimeTts.Test;

using Tsinswreng.CsTreeTest;

[Doc($$"""
本程序集對外的入口：只產出測試樹，不執行測試。
執行入口在 {{nameof(Program)}}。
""")]
public static class RimeTtsTest{

	[Doc("""
	建出本程序集的整棵測試樹。
	#Rtn[測試樹的根節點；只建樹，不跑用例。]
	""")]
	public static ITestNode MkTree(){
		var Tester = new RimeTtsTester();
		// 根節點名取程序集名，被上層收編時才不會與上層的根重名。
		Tester.TestNode = new TestNode{
			UniqName = "RimeTts.Test",
		};
		Tester.Init();
		// 根節點由本方法手工建立，框架不會代為設置這個旗標。
		Tester.Inited = true;
		return Tester.TestNode;
	}
}

[Doc($$"""
本程序集的組裝器：只掛測試類，不寫用例。
一個被測類配一個測試類（{{nameof(TestCfg)}}／{{nameof(TestSentenceBuffer)}} 等）。
""")]
public class RimeTtsTester : Tester{

	[Doc("把本程序集要跑的各測試類掛到根節點下。")]
	public override void Init(){
		this.AddTester<TestCfg>();
		this.AddTester<TestSentenceBuffer>();
		this.AddTester<TestLlmAnswer>();
		this.AddTester<TestLlmPrompts>();
		this.AddTester<TestFallbackTts>();
		this.AddTester<TestAudioPlaylist>();
		this.AddTester<TestSentenceFanout>();
		this.AddTester<TestRimeTtsPipeline>();
		this.AddTester<TestFastLlmTranslator>();
		this.AddTester<TestFileTypingListener>();
	}
}
