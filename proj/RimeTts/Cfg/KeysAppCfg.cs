namespace RimeTts.Cfg;

using Tsinswreng.CsKeyNode;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Tts;
using RimeTts.Domains.Translate;

[Doc($$"""
配置鍵族（CsKeyNode）。

*本類所在的 `RimeTts.Cfg` 是應用層，不是 `Infra`*：鍵的默認值要引用出廠語言表、
引擎名這些*業務*事實（見各鍵的默認值），故配置層允許依賴 `Domains`；
而 `Infra`（LangTag／Json／Log／Os）保持不依賴業務。
依賴方向因此是單向的：Cfg → Domains → Infra。2026-11-16 審查（ARCH-1）後從 `Infra/Cfg` 上移至此。

*鍵與值分離*：鍵在這裡聲明（有靜態型別、有默認值、可跳轉），值從
{{nameof(AppCfg)}} 的兩個源裡按路徑取。業務側一律用 `AppCfg.Inst.Get(KeysAppCfg.Xxx)`，
不得出現字符串鍵名。

*路徑是分段而非字符串*：取值收的是路徑段清單，所以鍵裡的 `MkrKeyNode` 分隔符只用於顯示與日誌，
不參與取值。

*默認值的唯一出處就是這裡*：第三個參數就是默認值。配置裡沒有這個鍵時，
`ICfgReaderExtn.Get<T>` 直接回它。`OptXxx` 選項物件上的屬性**不再各自帶業務默認值**
（一律中性空值，見各 Opt 類的說明）——兩處各寫一份必然漂移，
2026-11-16 審查（ARCH-2）已刪除這種雙寫。

*列表型鍵為何是 `KeyNode<IList<T>>`*：值模型只認標量，`Get<T>` 沒辦法把一個 JSON 陣列
直接轉成 `IList<T>`；故那幾個鍵的讀法是「先按鍵取裸值，取不到就用鍵上宣告的默認清單」，
由 `CfgMapper` 統一處理。這樣默認值仍然只有一份。
""")]
public class KeysAppCfg : KeysDeclBase<KeysAppCfg>, IStaticKeysDecl{

	[Doc(@"本族共用的節點製造器；分隔符只影響顯示，不參與取值。
	必須寫在鍵聲明之前：靜態字段按文字順序初始化，製造器晚於鍵就會是 null。")]
	public static MkrKeyNode _MkrKeyNode{get;} = new(){ PathSep = "/" };

	[Doc($$"""
	引導鍵：可寫配置檔的路徑，相對應用儲存區。
	它可以被出廠配置改寫，所以實際位置要在讀過出廠配置之後才決定。

	這裡的默認值是默認檔名的*唯一*出處。
	""")]
	public static KeyNode<str> RwCfgPath = Mk(null, [nameof(RwCfgPath)], "rimetts.Rw.jsonr");

	[Doc("Lua 插件與本進程的文件交互位置。")]
	public static class FileInteractor{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(FileInteractor)]);

		[Doc("內容檔案：Lua 寫一段 JSON，本端來讀。")]
		public static KeyNode<str> ContentFile = Mk(_R, [nameof(ContentFile)], "");

		[Doc("信號檔案：本端監看它的變動，一變就去讀內容檔。")]
		public static KeyNode<str> SignalFile = Mk(_R, [nameof(SignalFile)], "");
	}

	[Doc("成句規則。")]
	public static class SentenceSeg{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(SentenceSeg)]);

		[Doc("距最後一次上屏多少毫秒成句。默認 5000。")]
		public static KeyNode<i64> NoCommitGapMs = Mk(_R, [nameof(NoCommitGapMs)], 5000L);

		[Doc($$"""
		成句終止符清單；上屏以其中之一結尾就立刻成句。
		空清單＝只按 {{nameof(NoCommitGapMs)}} 成句。

		*本項默認是空的*：不同輸入方案的標點習慣不同，猜一組反而會切錯句。
		""")]
		public static KeyNode<IList<str>> SentenceTerminators = Mk<IList<str>>(_R, [nameof(SentenceTerminators)], new List<str>());
	}

	[Doc("翻譯（LLM）設定。")]
	public static class Translator{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(Translator)]);

		[Doc("LLM 接口密鑰。空串＝沒配置，啟動時就會被擋下。")]
		public static KeyNode<str> ApiKey = Mk(_R, [nameof(ApiKey)], "");

		[Doc("兼容 OpenAI 協議的 chat completions 端點。")]
		public static KeyNode<str> BaseUrl = Mk(_R, [nameof(BaseUrl)], "https://api.deepseek.com/chat/completions");

		[Doc("模型名。")]
		public static KeyNode<str> Model = Mk(_R, [nameof(Model)], "deepseek-v4-flash");

		[Doc("單次請求超時秒數。")]
		public static KeyNode<i32> TimeoutSec = Mk(_R, [nameof(TimeoutSec)], 20);

		[Doc("譯文快取條目上限；0＝不快取。")]
		public static KeyNode<i32> CacheCapacity = Mk(_R, [nameof(CacheCapacity)], 256);

		[Doc($$"""
		兜底系統提示詞（語言條目沒寫提示詞時用它）。
		*默認值鏡像 {{nameof(OptTranslator.DfltSystemPrompt)}}*：那一個是類型上的常量，
		這裡的第三個參數只是把它填成鍵的默認值，兩者必須一致。
		""")]
		public static KeyNode<str> DefaultSystemPrompt = Mk(_R, [nameof(DefaultSystemPrompt)], OptTranslator.DfltSystemPrompt);
	}

	[Doc("語音合成設定。")]
	public static class Tts{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(Tts)]);

		[Doc("音頻輸出目錄；空串＝執行檔目錄下的 `tts-output`。")]
		public static KeyNode<str> OutputDir = Mk(_R, [nameof(OutputDir)], "");

		[Doc("全局引擎優先級（語言條目沒寫自己的引擎清單時用它）。")]
		public static KeyNode<IList<str>> Engines = Mk<IList<str>>(
			_R
			,[nameof(Engines)]
			,new List<str>{ GttsViaHttpTts.EngineName, SystemSpeechTts.EngineName }
		);
	}

	[Doc("語言流水線：翻成哪些語言、按什麼順序唸。")]
	public static class LanguagePipeline{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(LanguagePipeline)]);

		[Doc($$"""
		語言條目清單，順序即翻譯與播放順序。

		*這是列表型鍵*，讀法見本類說明；{{nameof(CfgMapper)}}
		會把裸值（`IList<obj?>` of `IDictionary<str,obj?>`）逐項轉成 {{nameof(LanguageProfile)}}。
		""")]
		public static KeyNode<IList<LanguageProfile>> Languages = Mk<IList<LanguageProfile>>(
			_R
			,[nameof(Languages)]
			,DfltLanguageProfiles.MkAll()
		);
	}

	[Doc("編排層（扇出與播放隊列）的超時設定。")]
	public static class Pipeline{

		[Doc("本組的根節點。")]
		public static KeyNode _R = Mk(null, [nameof(Pipeline)]);

		[Doc($$"""
		單段音頻生成的超時秒數。超過就把這個語言當失敗——讓整句話卡在一個引擎上不值得。

		*為何放配置而不是代碼常量*：它與 `Translator.TimeoutSec` 是同一類旋鈕
		（「等多久算放棄」），一半可配一半寫死會讓維護心智分裂（2026-11-16 審查 MAINT-3）。
		""")]
		public static KeyNode<i32> GenerateTimeoutSec = Mk(_R, [nameof(GenerateTimeoutSec)], 40);

		[Doc("播放不足這個秒數時仍給這麼多超時；避免極短音頻因設備延遲被誤判為卡住。")]
		public static KeyNode<i32> MinPlayTimeoutSec = Mk(_R, [nameof(MinPlayTimeoutSec)], 20);

		[Doc("在音頻真實長度之外多給的餘量秒數：設備啟動、緩衝、系統繁忙。")]
		public static KeyNode<i32> PlayGraceSec = Mk(_R, [nameof(PlayGraceSec)], 10);
	}
}
