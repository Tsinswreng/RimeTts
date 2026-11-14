//Impl: 聲明見 CfgMapper.cs。
namespace RimeTts.Infra.Cfg;

using Tsinswreng.CsCfg;
using Tsinswreng.CsKeyNode;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Typing;
using RimeTts.Domains.Tts;
using RimeTts.Domains.Translate;

public static partial class CfgMapper{

	public static partial OptFileInteractor MkFileInteractor(ICfgReader Cfg){
		return new OptFileInteractor{
			ContentFile = Cfg.Get(KeysAppCfg.FileInteractor.ContentFile).Trim(),
			SignalFile = Cfg.Get(KeysAppCfg.FileInteractor.SignalFile).Trim(),
		};
	}

	public static partial OptSentenceSeg MkSentenceSeg(ICfgReader Cfg){
		return new OptSentenceSeg{
			NoCommitGapMs = Cfg.Get(KeysAppCfg.SentenceSeg.NoCommitGapMs),
			SentenceTerminators = ReadStrList(Cfg, KeysAppCfg.SentenceSeg.SentenceTerminators),
		};
	}

	public static partial OptTranslator MkTranslator(ICfgReader Cfg){
		return new OptTranslator{
			ApiKey = Cfg.Get(KeysAppCfg.Translator.ApiKey).Trim(),
			BaseUrl = Cfg.Get(KeysAppCfg.Translator.BaseUrl).Trim(),
			Model = Cfg.Get(KeysAppCfg.Translator.Model).Trim(),
			TimeoutSec = Cfg.Get(KeysAppCfg.Translator.TimeoutSec),
			CacheCapacity = Cfg.Get(KeysAppCfg.Translator.CacheCapacity),
			DefaultSystemPrompt = Cfg.Get(KeysAppCfg.Translator.DefaultSystemPrompt),
		};
	}

	public static partial OptTts MkTts(ICfgReader Cfg, I_GetBaseDir BaseDir){
		var Dir = Cfg.Get(KeysAppCfg.Tts.OutputDir).Trim();
		if(str.IsNullOrWhiteSpace(Dir)){
			// 留空＝用預設位置。路徑一定相對於應用儲存區，故不能讓它變成進程當前目錄下的相對路徑。
			Dir = BaseDir.Combine("tts-output");
		}
		return new OptTts{
			OutputDir = Dir,
			Engines = ReadStrList(Cfg, KeysAppCfg.Tts.Engines),
		};
	}

	public static partial OptLanguagePipeline MkLanguagePipeline(ICfgReader Cfg){
		return new OptLanguagePipeline{
			Languages = ReadLanguages(Cfg),
		};
	}

	private static partial IList<str> ReadStrList(ICfgReader Cfg, KeyNode<IList<str>> Key){
		// 明確轉成非泛型鍵，才會呼叫「取裸值」那一版 TryGet：
		// 泛型版會嘗試把清單轉成 IList<str>，而值模型只給 IList<obj?>，必然失敗。
		if(!Cfg.TryGet((IKeyNode)Key, out var Got, out _) || Got is null){
			// 回一份拷貝：鍵上的默認清單是共享的，交出去之後被下游就地改就會污染下一次解析。
			return [.. Key.Data];
		}
		if(Got is not IList<obj?> Items){
			// 回一份拷貝：鍵上的默認清單是共享的，交出去之後被下游就地改就會污染下一次解析。
			return [.. Key.Data];
		}
		var Result = new List<str>();
		foreach(var Item in Items){
			if(Item is null){
				continue;
			}
			var Text = Item as str ?? ICfgReaderExtn.MkInvariantStr(Item);
			if(!str.IsNullOrWhiteSpace(Text)){
				Result.Add(Text.Trim());
			}
		}
		return Result;
	}

	private static partial IList<LanguageProfile> ReadLanguages(ICfgReader Cfg){
		var Key = KeysAppCfg.LanguagePipeline.Languages;
		if(!Cfg.TryGet((IKeyNode)Key, out var Got, out _) || Got is not IList<obj?> Items){
			// 回一份拷貝：鍵上的默認清單是共享的，交出去之後被下游就地改就會污染下一次解析。
			return [.. Key.Data];
		}

		var Result = new List<LanguageProfile>();
		foreach(var Item in Items){
			if(Item is not IDictionary<str, obj?> Dict){
				continue;
			}
			var Lang = LangTag.Normalize(ReadStr(Dict, nameof(LanguageProfile.Language)));
			if(Lang.Length == 0){
				// 沒有語言代碼的條目等於沒寫：靜默丟掉，比讓它變成「翻成空語言」好。
				continue;
			}
			Result.Add(new LanguageProfile{
				Language = Lang,
				SystemPrompt = ReadStr(Dict, nameof(LanguageProfile.SystemPrompt)) ?? "",
				TtsEngines = ReadStrList(Dict, nameof(LanguageProfile.TtsEngines)),
			});
		}

		// 全部條目都無效時退回鍵上宣告的出廠清單：總比「一個語言都沒有」好。
		return Result.Count > 0 ? Result : Key.Data;
	}

	private static partial str? ReadStr(IDictionary<str, obj?> Dict, str Key){
		if(!Dict.TryGetValue(Key, out var Value) || Value is null){
			return null;
		}
		return Value as str ?? ICfgReaderExtn.MkInvariantStr(Value);
	}

	private static partial IList<str> ReadStrList(IDictionary<str, obj?> Dict, str Key){
		var Result = new List<str>();
		if(!Dict.TryGetValue(Key, out var Value) || Value is not IList<obj?> Items){
			return Result;
		}
		foreach(var Item in Items){
			if(Item is null){
				continue;
			}
			var Text = Item as str ?? ICfgReaderExtn.MkInvariantStr(Item);
			if(!str.IsNullOrWhiteSpace(Text)){
				Result.Add(Text.Trim());
			}
		}
		return Result;
	}
}
