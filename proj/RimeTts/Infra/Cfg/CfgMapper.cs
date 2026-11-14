namespace RimeTts.Infra.Cfg;

using Tsinswreng.CsCfg;
using Tsinswreng.CsKeyNode;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Tts;
using RimeTts.Domains.Translate;
using RimeTts.Domains.Typing;

[Doc($$"""
把配置樹映射成業務用的選項物件。

*為何要有這一層*：配置是「按路徑取值的樹」，業務要的是「有型別的選項物件」。
把映射寫在這裡（而不是散在各服務的建構子裡），有三個好處：
所有默認值的來源只有一處（{{nameof(KeysAppCfg)}}）、映射可以單獨測、
服務不需要知道 {{nameof(ICfgReader)}} 這種東西存在。

*列表型鍵的讀法*：值模型只認標量，故像終止符清單、語言條目這種「一串東西」
要先按鍵取裸值（`IList<obj?>`），取不到就用鍵上宣告的默認清單；
再由本類逐項轉成強型別。這是 CsCfg 值模型的限制，不是可以繞過的設計選擇。
""")]
public static partial class CfgMapper{

	[Doc($$"""
	讀文件交互位置。
	#Prm[{{nameof(Cfg)}}][配置來源。]
	""")]
	public static partial OptFileInteractor MkFileInteractor(ICfgReader Cfg);

	[Doc("讀成句規則（間隔與終止符清單）。")]
	public static partial OptSentenceSeg MkSentenceSeg(ICfgReader Cfg);

	[Doc("讀翻譯設定。")]
	public static partial OptTranslator MkTranslator(ICfgReader Cfg);

	[Doc($$"""
	讀 TTS 設定；並把「空輸出目錄」解析成 {{nameof(I_GetBaseDir)}} 下的 `tts-output`。
	#Prm[{{nameof(Cfg)}}][配置來源。]
	#Prm[{{nameof(BaseDir)}}][應用儲存區；輸出目錄為空時以此為基準。]
	""")]
	public static partial OptTts MkTts(ICfgReader Cfg, I_GetBaseDir BaseDir);

	[Doc("讀語言流水線（順序、提示詞、各語言引擎）。")]
	public static partial OptLanguagePipeline MkLanguagePipeline(ICfgReader Cfg);

	[Doc($$"""
	讀一個字串清單型鍵：配置有就用配置，沒有就用鍵上宣告的默認清單。
	*為何要轉型*：值模型給的是 `IList<obj?>`，元素可能是字串或數字；
	本專案的清單都是字串（終止符、引擎名），非字串元素轉成它的 invariant 表示再收下。
	""")]
	private static partial IList<str> ReadStrList(ICfgReader Cfg, Tsinswreng.CsKeyNode.KeyNode<IList<str>> Key);

	[Doc($$"""
	讀語言條目清單。
	*每一項的形狀*：`IDictionary<str,obj?>`，鍵名即 {{nameof(Domains.Translate.LanguageProfile)}} 的成員名。
	缺語言代碼的條目會被丟掉（那條配置等於沒寫）。
	""")]
	private static partial IList<Domains.Translate.LanguageProfile> ReadLanguages(ICfgReader Cfg);

	[Doc("從字典裡讀一個字串成員；不存在或為 null 回 null。")]
	private static partial str? ReadStr(IDictionary<str, obj?> Dict, str Key);

	[Doc("從字典裡讀一個字串清單成員；不存在回空清單。")]
	private static partial IList<str> ReadStrList(IDictionary<str, obj?> Dict, str Key);
}
