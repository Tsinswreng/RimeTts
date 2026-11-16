using RimeTts.Domains.Translate;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Translate;

[Doc($$"""
{{nameof(FastLlmTranslator)}} 的測試類主檔：只串起各分片。

*為何值得單獨測*：快取的定性（BUG-3——「解析失敗」與「模型明確翻不了」）
發生在這一層；用假 HTTP handler 才能在不碰網絡的前提下控制模型的回覆。
""")]
public partial class TestFastLlmTranslator : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterCacheSemantics();
	}
}

[Doc($$"""
本分片測 {{nameof(FastLlmTranslator)}} 的快取定性。
""")]
public partial class TestFastLlmTranslator{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterCacheSemantics();

	[Doc("模型明確回 `Translation: null`：結論進快取——第二次同句不再發請求。")]
	public partial Task<nil> ModelNull_IsCached(obj? O);

	[Doc("回應不是合法 jsonr（我端解析失敗）：*不*進快取——第二次同句仍然發請求（可重試）。")]
	public partial Task<nil> ParseFailure_IsNotCached(obj? O);

	[Doc("模型回錯目標語言：*不*進快取——第二次同句仍然發請求。")]
	public partial Task<nil> LangMismatch_IsNotCached(obj? O);

	[Doc("成功譯文進快取：第二次同句直接命中，不再發請求。")]
	public partial Task<nil> Success_IsCached(obj? O);
}
