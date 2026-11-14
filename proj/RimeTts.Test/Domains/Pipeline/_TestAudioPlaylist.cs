using RimeTts.Domains.Pipeline;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Pipeline;

[Doc($$"""
{{nameof(AudioPlaylist)}} 的測試類主檔：只串起各分片。

*為何值得單獨測*：它同時管三件事——等生成、按序播、被打斷。
這三件的交互（後面的先生成完、前面的還沒好）用真播放器很難重現，用假播放器卻很容易。
""")]
public partial class TestAudioPlaylist : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterPlaylist();
	}
}

[Doc($$"""
本分片測 {{nameof(AudioPlaylist)}}：{{nameof(AudioPlaylist.Add)}} 的順序、生成失敗的跳過、{{nameof(AudioPlaylist.CancelCurrent)}}。
""")]
public partial class TestAudioPlaylist{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterPlaylist();

	[Doc("即使後面的音頻先生成完，也要等前面那一段播完才輪到它。")]
	public partial Task<nil> PlaysInAddOrder_EvenIfLaterReadyFirst(obj? O);

	[Doc("生成失敗（結果為 null）的項被跳過，不影響後面的項。")]
	public partial Task<nil> SkipsFailedGeneration(obj? O);

	[Doc("{{nameof(AudioPlaylist.Complete)}} 之後，隊列裡剩下的照樣播完，循環才結束。")]
	public partial Task<nil> Complete_DrainsRemainingJobs(obj? O);
}
