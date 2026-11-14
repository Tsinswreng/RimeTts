using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Translate;
using RimeTts.Infra;

namespace RimeTts.Test.Domains.Translate;

public partial class TestLlmPrompts{

	[Doc("取出提示詞裡那段範例 JSON（第一個 `{` 到最後一個 `}`）。")]
	static str ExtractExample(str Prompt){
		var Start = Prompt.IndexOf('{');
		var End = Prompt.LastIndexOf('}');
		if(Start < 0 || End <= Start){
			return "";
		}
		return Prompt[Start..(End + 1)];
	}

	public partial void RegisterPrompts(){
		this.AddTestCase(nameof(Example_IsValidJsonr), Example_IsValidJsonr!);
		this.AddTestCase(nameof(Example_DoesNotHardcodeDelimiter), Example_DoesNotHardcodeDelimiter!);
		this.AddTestCase(nameof(Prompt_RequiresFreshDelimiter), Prompt_RequiresFreshDelimiter!);
		this.AddTestCase(nameof(En_MatchesDefaultPrompt), En_MatchesDefaultPrompt!);
	}

	public partial async Task<nil> Example_IsValidJsonr(obj? O){
		var T = Assert.IsTrue;
		foreach(var Profile in DfltLanguageProfiles.MkAll()){
			var Example = ExtractExample(Profile.SystemPrompt);
			T(Example.Length > 0, $"{Profile.Language} 的提示詞沒有範例 JSON");

			var Answer = LlmAnswerReader.ParseAnswer(Example);
			T(Answer is not null, $"{Profile.Language} 的範例不是合法 jsonr：\n{Example}");
			T(LangTag.Same(Answer!.TargetLang, Profile.Language),
				$"{Profile.Language} 的範例寫的 TargetLang 是 {Answer.TargetLang}");
			T(!str.IsNullOrWhiteSpace(Answer.Translation),
				$"{Profile.Language} 的範例 Translation 不該是空的");
		}
		return NIL;
	}

	public partial async Task<nil> Example_DoesNotHardcodeDelimiter(obj? O){
		var T = Assert.IsTrue;
		// 這一行是這條用例存在的理由：舊版六份提示詞全部寫死 R"EOF(…)"。
		// 譯文裡若出現 )EOF"（例如翻一段程式碼），解析會在那裡提前收尾。
		foreach(var Prompt in AllPrompts()){
			T(!Prompt.Contains("R\"EOF(", StringComparison.Ordinal),
				"提示詞不得在範例裡寫死 EOF 定界詞");
		}
		return NIL;
	}

	public partial async Task<nil> Prompt_RequiresFreshDelimiter(obj? O){
		var T = Assert.IsTrue;
		foreach(var Prompt in AllPrompts()){
			// 英日各一份文字，各自的說法不同（delimiter／区切り語），故兩種都接受。
			var SaysFresh = Prompt.Contains("random delimiter", StringComparison.OrdinalIgnoreCase)
				|| Prompt.Contains("区切り語", StringComparison.Ordinal);
			T(SaysFresh, "提示詞必須要求模型每次自己造定界詞");
		}
		return NIL;
	}

	public partial async Task<nil> En_MatchesDefaultPrompt(obj? O){
		var T = Assert.IsTrue;
		var En = DfltLanguageProfiles.MkAll()[0];
		T(En.Language == "en", $"第一項應為 en，實際 {En.Language}");
		T(En.SystemPrompt == OptTranslator.DfltSystemPrompt, "英文條目的提示詞應就是兜底提示詞那一份");
		return NIL;
	}

	[Doc("全部出廠提示詞（兜底 ＋ 五個語言）。")]
	static IEnumerable<str> AllPrompts(){
		yield return OptTranslator.DfltSystemPrompt;
		foreach(var Profile in DfltLanguageProfiles.MkAll()){
			yield return Profile.SystemPrompt;
		}
	}
}
