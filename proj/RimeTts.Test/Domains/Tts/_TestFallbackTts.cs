using RimeTts.Domains.Tts;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Tts;

[Doc($$"""
{{nameof(FallbackTts)}} 的測試類主檔：只串起各分片。

*為何這套特別值得測*：它決定「哪個聲音會出來」。舊版的同一段邏輯在
`GenEtPlay` 與 `GenerateAudio` 各抄一遍，行為差一點（超時不同）也沒人發現。
""")]
public partial class TestFallbackTts : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterFallback();
	}
}

[Doc($$"""
本分片測 {{nameof(FallbackTts.GenerateAudio)}}：依優先級試引擎、失敗換手、全失敗拋錯。
""")]
public partial class TestFallbackTts{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterFallback();

	[Doc("第一個引擎成功時就用它，不再打擾後面的引擎。")]
	public partial Task<nil> UsesFirstSuccessfulEngine(obj? O);

	[Doc("第一個失敗時換下一個；回傳成功的引擎的結果。")]
	public partial Task<nil> FallsThroughToNextEngine(obj? O);

	[Doc("請求自帶的引擎清單優先於全局設定。")]
	public partial Task<nil> RequestEnginesOverrideGlobal(obj? O);

	[Doc("全部失敗時拋帶 {{nameof(KeysErr.Tts.AllEnginesFailed)}} 鍵的錯誤。")]
	public partial Task<nil> ThrowsWhenAllFail(obj? O);

	[Doc("配置裡寫了不認識的引擎名時跳過它，不讓整句失敗。")]
	public partial Task<nil> IgnoresUnknownEngine(obj? O);

	[Doc("環境層面不可用的引擎（例如 AOT 下的 SAPI）要被剔除，且根本不會被呼叫。")]
	public partial Task<nil> SkipsUnavailableEngine(obj? O);
}
