using RimeTts.Infra.Cfg;
using System.Reflection;
using Tsinswreng.CsTreeTest;
using Tsinswreng.CsKeyNode;
using Tsinswreng.CsCfg;

namespace RimeTts.Test.Domains.Cfg;

public partial class TestCfg{

	public partial void RegisterSample(){
		this.AddTestCase(nameof(Sample_Parses), Sample_Parses!);
		this.AddTestCase(nameof(Sample_CoversAllKeys), Sample_CoversAllKeys!);
		this.AddTestCase(nameof(Sample_MatchesDefaultLanguages), Sample_MatchesDefaultLanguages!);
	}

	public partial async Task<nil> Sample_Parses(obj? O){
		var T = Assert.IsTrue;
		var Text = CfgSample.MkDefaultText();
		T(!str.IsNullOrWhiteSpace(Text), "樣例文本不該是空的");

		// 這一行就是舊版 bug 的守門員：解析不成就拋。
		var Cfg = RimeTts.Infra.Json.JsonrCfg.FromText(Text, "sample");
		var Opt = CfgMapper.MkTranslator(Cfg);
		T(Opt.Model.Length > 0, "樣例應帶一個模型名");
		T(Opt.DefaultSystemPrompt.Length > 0, "樣例應帶兜底提示詞");
		return NIL;
	}

	public partial async Task<nil> Sample_CoversAllKeys(obj? O){
		var T = Assert.IsTrue;
		var Cfg = RimeTts.Infra.Json.JsonrCfg.FromText(CfgSample.MkDefaultText(), "sample");

		foreach(var Key in AllKeys()){
			var Found = Cfg.TryGet((IKeyNode)Key, out _, out _);
			// 分組節點（_R）本身也是一個物件鍵，故一律要求存在。
			T(Found, $"樣例缺少配置鍵：{Key.FullPathSegs.Aggregate((A, B) => A + "/" + B)}");
		}
		return NIL;
	}

	public partial async Task<nil> Sample_MatchesDefaultLanguages(obj? O){
		var T = Assert.IsTrue;
		var Cfg = RimeTts.Infra.Json.JsonrCfg.FromText(CfgSample.MkDefaultText(), "sample");
		var FromSample = CfgMapper.MkLanguagePipeline(Cfg);
		var Dflt = RimeTts.Domains.Translate.DfltLanguageProfiles.MkAll();

		T(FromSample.Languages.Count == Dflt.Count,
			$"樣例應有 {Dflt.Count} 個語言，實際 {FromSample.Languages.Count}");
		for(var I = 0; I < Dflt.Count && I < FromSample.Languages.Count; I++){
			T(FromSample.Languages[I].Language == Dflt[I].Language,
				$"第 {I} 個語言應為 {Dflt[I].Language}，實際 {FromSample.Languages[I].Language}");
			// 提示詞是長文本，只比對「有沒有帶到」與開頭幾個字，避免訊息淹掉報告。
			T(FromSample.Languages[I].SystemPrompt.StartsWith(Dflt[I].SystemPrompt[..20], StringComparison.Ordinal),
				$"第 {I} 個語言的提示詞開頭不符");
		}
		return NIL;
	}

	[Doc($$"""
	反射列出 {{nameof(KeysAppCfg)}} 及其嵌套分組裡宣告的全部鍵。
	*為何可以用反射*：這是測試，不受 AOT 裁剪限制。
	""")]
	static IEnumerable<IKeyNode> AllKeys(){
		var Types = new List<Type>{
			typeof(KeysAppCfg),
			typeof(KeysAppCfg.FileInteractor),
			typeof(KeysAppCfg.SentenceSeg),
			typeof(KeysAppCfg.Translator),
			typeof(KeysAppCfg.Tts),
			typeof(KeysAppCfg.LanguagePipeline),
		};

		foreach(var Type in Types){
			foreach(var Field in Type.GetFields(BindingFlags.Public | BindingFlags.Static)){
				if(!typeof(IKeyNode).IsAssignableFrom(Field.FieldType)){
					continue;
				}
				if(Field.GetValue(null) is IKeyNode Key){
					yield return Key;
				}
			}
		}
	}
}
