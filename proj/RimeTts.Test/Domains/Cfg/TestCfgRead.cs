using RimeTts.Infra.Cfg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Cfg;

[Doc($$"""
本分片測 {{nameof(CfgMapper)}} 的五個映射入口：配置樹 → 選項物件。
""")]
public partial class TestCfg{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterRead();

	[Doc("標量鍵要全部讀進來（密鑰、模型、超時、快取容量、提示詞）。")]
	public partial Task<nil> Read_TranslatorScalars(obj? O);

	[Doc("語言清單要保留配置裡的順序，且提示詞與引擎清單逐項帶過來。")]
	public partial Task<nil> Read_LanguageOrderAndPrompts(obj? O);

	[Doc("語言清單寫成空陣列時要退回出廠清單，而不是「一個語言都沒有」。")]
	public partial Task<nil> Read_LanguageFallsBackToDefault(obj? O);

	[Doc("終止符清單要讀成字串清單；沒有這個鍵時回空清單。")]
	public partial Task<nil> Read_SentenceTerminators(obj? O);

	[Doc("輸出目錄寫成空串時要解析成儲存區下的 tts-output，而不是相對當前目錄。")]
	public partial Task<nil> Read_TtsResolvesDefaultOutputDir(obj? O);
}
