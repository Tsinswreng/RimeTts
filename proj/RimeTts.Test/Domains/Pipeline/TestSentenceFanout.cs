using RimeTts.Domains.Pipeline;
using RimeTts.Domains.Translate;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Pipeline;

[Doc($$"""
{{nameof(SentenceFanout)}} 的測試類主檔：只串起各分片。

*為何值得單獨測*：2026-11-16 審查的 BUG-1（單語言超時殺死整條流水線）、
BUG-2（翻譯屏障扣住已翻好的語言）、BUG-4（空清單兜底翻英文）都發生在這一層；
本測試類就是那三個結論的釘子。
""")]
public partial class TestSentenceFanout : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterFanout();
	}
}

[Doc($$"""
本分片測 {{nameof(SentenceFanout)}}：入隊順序、失敗隔離、超時定性、空清單行為。
""")]
public partial class TestSentenceFanout{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterFanout();

	[Doc("每個語言按配置順序入隊一個任務鏈；入隊即刻完成（不等翻譯）。")]
	public partial Task<nil> EnqueuesPerLanguage_InConfigOrder(obj? O);

	[Doc("一個語言翻譯拋異常，只影響它那一格；其他語言照常生成。")]
	public partial Task<nil> TranslateFailure_IsolatesItsOwnSlot(obj? O);

	[Doc($$"""
	單一語言的請求級取消（非全局）不殺流水線：那個語言回 null、循環繼續。
	這正是 BUG-1 的守門員——舊代碼把這種取消上拋，殺死了唯一的句子消費循環。
	""")]
	public partial Task<nil> PerRequestCancel_DoesNotKillPipeline(obj? O);

	[Doc("合成超時（配置的 GenerateTimeoutSec）只放棄那個語言，不上拋。")]
	public partial Task<nil> GenerateTimeout_AbortsOnlyThatLanguage(obj? O);

	[Doc("歸一化後語言清單為空時：只記警告、全部跳過——不兜底翻英文（BUG-4 的釘子）。")]
	public partial Task<nil> EmptyProfiles_DropsSentence_NoEnFallback(obj? O);

	[Doc("語言代碼 `JP` 被歸一化成 `ja` 後才交給翻譯與合成。")]
	public partial Task<nil> NormalizesLanguageTags(obj? O);
}
