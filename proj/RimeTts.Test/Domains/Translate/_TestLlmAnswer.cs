using RimeTts.Domains.Translate;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Translate;

[Doc($$"""
{{nameof(LlmAnswerReader)}} 的測試類主檔：只串起各分片。

被測對象是純解析（輸入是字串），故不碰網絡、不經容器。
""")]
public partial class TestLlmAnswer : Tester{

	[Doc("掛上兩個分片：包絡取正文、正文解析成 DTO。")]
	public override void Init(){
		RegisterReadContent();
		RegisterParseAnswer();
	}
}

[Doc($$"""
本分片測 {{nameof(LlmAnswerReader.ReadContent)}}：從別人的包絡裡挖出助手正文。
""")]
public partial class TestLlmAnswer{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterReadContent();

	[Doc("`choices[0].message.content` 是字串時直接取出。")]
	public partial Task<nil> ReadContent_FromMessageString(obj? O);

	[Doc("`content` 是「文字積木」陣列時，把各段的 text 串起來。")]
	public partial Task<nil> ReadContent_FromContentBlocks(obj? O);

	[Doc("根上有 `output_text` 時取它（Responses 風格）。")]
	public partial Task<nil> ReadContent_FromOutputText(obj? O);

	[Doc("包絡裡完全沒有可用內容時回 null。")]
	public partial Task<nil> ReadContent_ReturnsNullWhenAbsent(obj? O);
}

[Doc($$"""
本分片測 {{nameof(LlmAnswerReader.ParseAnswer)}}：把正文變成 `ResLlm`。
""")]
public partial class TestLlmAnswer{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterParseAnswer();

	[Doc("帶 raw string 的 jsonr 要能解析，且譯文裡的引號原樣保留（不被轉義破壞）。")]
	public partial Task<nil> ParseAnswer_WithRawString(obj? O);

	[Doc("純 JSON 也照收（jsonr 是 JSON 的超集）。")]
	public partial Task<nil> ParseAnswer_WithPlainJson(obj? O);

	[Doc("模型自己加的 markdown 圍欄要被剝掉。")]
	public partial Task<nil> ParseAnswer_StripsCodeFence(obj? O);

	[Doc("`Translation: null` 要解析成 null，而不是字串 \"null\"。")]
	public partial Task<nil> ParseAnswer_KeepsNullTranslation(obj? O);

	[Doc("不是 JSON 的內容回 null（視為「模型沒按格式回」，屬正常失敗，不拋）。")]
	public partial Task<nil> ParseAnswer_ReturnsNullOnGarbage(obj? O);
}
