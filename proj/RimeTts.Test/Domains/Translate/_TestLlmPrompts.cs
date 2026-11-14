using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Translate;

namespace RimeTts.Test.Domains.Translate;

[Doc($$"""
{{nameof(LlmPrompts)}} 與出廠語言提示詞的測試類主檔。

*為何要測提示詞*：提示詞就是協議。範例裡的格式錯了、或定界詞寫死了，
症狀是「某句話翻不出來」而不是編譯錯——那種錯只有拿提示詞當輸入跑一次才看得到。
""")]
public partial class TestLlmPrompts : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterPrompts();
	}
}

[Doc($$"""
本分片測兩件事：範例本身能不能被我們的解析器讀懂、以及定界詞是不是要求模型每次自己造。
""")]
public partial class TestLlmPrompts{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterPrompts();

	[Doc("每份出廠提示詞裡的範例都要是合法 jsonr，且解析出來的目標語言與該條目一致。")]
	public partial Task<nil> Example_IsValidJsonr(obj? O);

	[Doc("範例不得寫死 `EOF` 這種常見詞——寫死之後譯文裡一旦出現 `)EOF\"` 就會提前收尾。")]
	public partial Task<nil> Example_DoesNotHardcodeDelimiter(obj? O);

	[Doc("每份提示詞都要明說「定界詞每次自己造、且不得出現在譯文裡」。")]
	public partial Task<nil> Prompt_RequiresFreshDelimiter(obj? O);

	[Doc("兜底提示詞與語言清單第一項（英文）必須是同一段文字，不能各寫一份。")]
	public partial Task<nil> En_MatchesDefaultPrompt(obj? O);
}
