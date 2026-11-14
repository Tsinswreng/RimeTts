using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Translate;
using RimeTts.Infra.Json;

namespace RimeTts.Test.Domains.Translate;

public partial class TestLlmAnswer{

	public partial void RegisterReadContent(){
		this.AddTestCase(nameof(ReadContent_FromMessageString), ReadContent_FromMessageString!);
		this.AddTestCase(nameof(ReadContent_FromContentBlocks), ReadContent_FromContentBlocks!);
		this.AddTestCase(nameof(ReadContent_FromOutputText), ReadContent_FromOutputText!);
		this.AddTestCase(nameof(ReadContent_ReturnsNullWhenAbsent), ReadContent_ReturnsNullWhenAbsent!);
	}

	public partial async Task<nil> ReadContent_FromMessageString(obj? O){
		var T = Assert.IsTrue;
		var Body = """
		{"choices":[{"message":{"role":"assistant","content":"hello"}}]}
		""";
		var Content = LlmAnswerReader.ReadContent(Body);
		T(Content == "hello", $"應取到 hello，實際 {Content ?? "null"}");
		return NIL;
	}

	public partial async Task<nil> ReadContent_FromContentBlocks(obj? O){
		var T = Assert.IsTrue;
		var Body = """
		{"choices":[{"message":{"content":[{"type":"text","text":"a"},{"type":"image"},{"type":"text","text":"b"}]}}]}
		""";
		var Content = LlmAnswerReader.ReadContent(Body);
		T(Content == "a\nb", $"多段文字應以換行相連，實際 {Content ?? "null"}");
		return NIL;
	}

	public partial async Task<nil> ReadContent_FromOutputText(obj? O){
		var T = Assert.IsTrue;
		var Body = """
		{"output_text":"from responses api"}
		""";
		var Content = LlmAnswerReader.ReadContent(Body);
		T(Content == "from responses api", $"應取到 output_text，實際 {Content ?? "null"}");
		return NIL;
	}

	public partial async Task<nil> ReadContent_ReturnsNullWhenAbsent(obj? O){
		var T = Assert.IsTrue;
		T(LlmAnswerReader.ReadContent("""{"choices":[]}""") is null, "沒有 choices 內容時應回 null");
		T(LlmAnswerReader.ReadContent("""{}""") is null, "空物件應回 null");
		return NIL;
	}
}

public partial class TestLlmAnswer{

	public partial void RegisterParseAnswer(){
		this.AddTestCase(nameof(ParseAnswer_WithRawString), ParseAnswer_WithRawString!);
		this.AddTestCase(nameof(ParseAnswer_WithPlainJson), ParseAnswer_WithPlainJson!);
		this.AddTestCase(nameof(ParseAnswer_StripsCodeFence), ParseAnswer_StripsCodeFence!);
		this.AddTestCase(nameof(ParseAnswer_KeepsNullTranslation), ParseAnswer_KeepsNullTranslation!);
		this.AddTestCase(nameof(ParseAnswer_ReturnsNullOnGarbage), ParseAnswer_ReturnsNullOnGarbage!);
	}

	public partial async Task<nil> ParseAnswer_WithRawString(obj? O){
		var T = Assert.IsTrue;
		// 這就是提示詞要模型回的那種形狀：譯文用 raw string，內含引號與換行都不必轉義。
		var Content = """
		{
			"UserInputLang": "zh",
			"TargetLang": "en",
			"Translation": R"EOF(He said "hi"
		and left.)EOF"
		}
		""";
		var Answer = LlmAnswerReader.ParseAnswer(Content);

		T(Answer is not null, "帶 raw string 的 jsonr 應能解析");
		T(Answer!.TargetLang == "en", $"TargetLang 應為 en，實際 {Answer.TargetLang}");
		T(Answer.Translation == "He said \"hi\"\nand left.", $"譯文內容不符：{Answer.Translation}");
		return NIL;
	}

	public partial async Task<nil> ParseAnswer_WithPlainJson(obj? O){
		var T = Assert.IsTrue;
		var Content = """{"UserInputLang":"zh","TargetLang":"ja","Translation":"こんにちは"}""";
		var Answer = LlmAnswerReader.ParseAnswer(Content);

		T(Answer is not null, "純 JSON 也該解析");
		T(Answer!.Translation == "こんにちは", $"譯文不符：{Answer.Translation}");
		return NIL;
	}

	public partial async Task<nil> ParseAnswer_StripsCodeFence(obj? O){
		var T = Assert.IsTrue;
		var Content = """
		```jsonr
		{"TargetLang":"en","Translation":"ok"}
		```
		""";
		var Answer = LlmAnswerReader.ParseAnswer(Content);

		T(Answer is not null, "圍欄包起來的答案仍該解析");
		T(Answer!.Translation == "ok", $"譯文不符：{Answer.Translation}");
		return NIL;
	}

	public partial async Task<nil> ParseAnswer_KeepsNullTranslation(obj? O){
		var T = Assert.IsTrue;
		var Content = """{"TargetLang":"fr","Translation":null}""";
		var Answer = LlmAnswerReader.ParseAnswer(Content);

		T(Answer is not null, "Translation 為 null 時整份仍該解析成功");
		T(Answer!.Translation is null, "Translation 應為 null（模型表明翻不了）");
		return NIL;
	}

	public partial async Task<nil> ParseAnswer_ReturnsNullOnGarbage(obj? O){
		var T = Assert.IsTrue;
		T(LlmAnswerReader.ParseAnswer("sorry, I cannot do that") is null, "非 JSON 應回 null");
		T(LlmAnswerReader.ParseAnswer("") is null, "空字串應回 null");
		return NIL;
	}
}
