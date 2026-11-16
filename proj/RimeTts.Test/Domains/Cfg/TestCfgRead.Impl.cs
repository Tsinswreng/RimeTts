using RimeTts.Cfg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Cfg;

public partial class TestCfg{

	[Doc("一段最小但完整的配置；各用例按需改其中的片段。")]
	const str MinimalJsonr = """
	{
		"FileInteractor": {
			"ContentFile": "c.json",
			"SignalFile": "s.signal"
		},
		"SentenceSeg": {
			"NoCommitGapMs": 2500,
			"SentenceTerminators": ["。", "！"]
		},
		"Translator": {
			"ApiKey": "sk-test",
			"BaseUrl": "https://example.test/v1/chat",
			"Model": "test-model",
			"TimeoutSec": 7,
			"CacheCapacity": 3,
			"DefaultSystemPrompt": "default prompt"
		},
		"Tts": {
			"OutputDir": "",
			"Engines": ["SystemSpeech"]
		},
		"LanguagePipeline": {
			"Languages": [
				{
					"Language": "JA",
					"SystemPrompt": "japanese prompt",
					"TtsEngines": ["gTTS"]
				},
				{
					"Language": "en",
					"SystemPrompt": "",
					"TtsEngines": []
				}
			]
		}
	}
	""";

	[Doc("造一個配置來源。")]
	static Tsinswreng.CsCfg.ICfgReader MkCfg(str Jsonr){
		return RimeTts.Infra.Json.JsonrCfg.FromText(Jsonr, "test");
	}

	public partial void RegisterRead(){
		this.AddTestCase(nameof(Read_TranslatorScalars), Read_TranslatorScalars!);
		this.AddTestCase(nameof(Read_LanguageOrderAndPrompts), Read_LanguageOrderAndPrompts!);
		this.AddTestCase(nameof(Read_LanguageFallsBackToDefault), Read_LanguageFallsBackToDefault!);
		this.AddTestCase(nameof(Read_SentenceTerminators), Read_SentenceTerminators!);
		this.AddTestCase(nameof(Read_TtsResolvesDefaultOutputDir), Read_TtsResolvesDefaultOutputDir!);
	}

	public partial async Task<nil> Read_TranslatorScalars(obj? O){
		var T = Assert.IsTrue;
		var Opt = CfgMapper.MkTranslator(MkCfg(MinimalJsonr));

		T(Opt.ApiKey == "sk-test", $"ApiKey 應為 sk-test，實際 {Opt.ApiKey}");
		T(Opt.BaseUrl == "https://example.test/v1/chat", $"BaseUrl 不符：{Opt.BaseUrl}");
		T(Opt.Model == "test-model", $"Model 不符：{Opt.Model}");
		T(Opt.TimeoutSec == 7, $"TimeoutSec 應為 7，實際 {Opt.TimeoutSec}");
		T(Opt.CacheCapacity == 3, $"CacheCapacity 應為 3，實際 {Opt.CacheCapacity}");
		T(Opt.DefaultSystemPrompt == "default prompt", "DefaultSystemPrompt 不符");
		return NIL;
	}

	public partial async Task<nil> Read_LanguageOrderAndPrompts(obj? O){
		var T = Assert.IsTrue;
		var Opt = CfgMapper.MkLanguagePipeline(MkCfg(MinimalJsonr));

		T(Opt.Languages.Count == 2, $"應有 2 個語言，實際 {Opt.Languages.Count}");
		// 順序即播放順序，故必須與配置一致，不能靠字典或排序。
		T(Opt.Languages[0].Language == "ja", $"第一個語言應為 ja（jp 類大小寫要歸一化），實際 {Opt.Languages[0].Language}");
		T(Opt.Languages[0].SystemPrompt == "japanese prompt", "第一個語言的提示詞不符");
		T(Opt.Languages[0].TtsEngines.Count == 1 && Opt.Languages[0].TtsEngines[0] == "gTTS", "第一個語言的引擎清單不符");
		T(Opt.Languages[1].Language == "en", $"第二個語言應為 en，實際 {Opt.Languages[1].Language}");
		T(Opt.Languages[1].TtsEngines.Count == 0, "第二個語言沒寫引擎，應為空清單（＝用全局設定）");
		return NIL;
	}

	public partial async Task<nil> Read_LanguageFallsBackToDefault(obj? O){
		var T = Assert.IsTrue;
		var Jsonr = """
		{
			"LanguagePipeline": {
				"Languages": []
			}
		}
		""";
		var Opt = CfgMapper.MkLanguagePipeline(MkCfg(Jsonr));

		// 出廠清單是英日西義法；重點是「不能是 0」，且第一個是 en。
		T(Opt.Languages.Count > 1, $"空清單應退回出廠清單，實際 {Opt.Languages.Count} 個");
		T(Opt.Languages[0].Language == "en", $"出廠清單第一個應為 en，實際 {Opt.Languages[0].Language}");
		return NIL;
	}

	public partial async Task<nil> Read_SentenceTerminators(obj? O){
		var T = Assert.IsTrue;
		var Opt = CfgMapper.MkSentenceSeg(MkCfg(MinimalJsonr));

		T(Opt.NoCommitGapMs == 2500, $"NoCommitGapMs 應為 2500，實際 {Opt.NoCommitGapMs}");
		T(Opt.SentenceTerminators.Count == 2, $"應有 2 個終止符，實際 {Opt.SentenceTerminators.Count}");
		T(Opt.SentenceTerminators[0] == "。" && Opt.SentenceTerminators[1] == "！", "終止符內容或順序不符");
		return NIL;
	}

	public partial async Task<nil> Read_TtsResolvesDefaultOutputDir(obj? O){
		var T = Assert.IsTrue;
		var BaseDir = new Fakes.FakeBaseDir(@"X:\app");
		var Opt = CfgMapper.MkTts(MkCfg(MinimalJsonr), BaseDir);

		T(Opt.OutputDir == System.IO.Path.Combine(@"X:\app", "tts-output"), $"輸出目錄應解析成儲存區下的 tts-output，實際 {Opt.OutputDir}");
		T(Opt.Engines.Count == 1 && Opt.Engines[0] == "SystemSpeech", "引擎清單不符");
		return NIL;
	}
}
