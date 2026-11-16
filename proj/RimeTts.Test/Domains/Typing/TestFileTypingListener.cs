using RimeTts.Domains.Typing;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Typing;

[Doc($$"""
{{nameof(FileTypingListener)}} 的測試類主檔：只串起各分片。

*為何值得單獨測*：Lua 插件與本進程的協議（內容檔＋信號檔）落在這一層；
用真實的臨時檔案（在工作區內）驗「寫信號→觸發讀→派發事件」與去重。
""")]
public partial class TestFileTypingListener : Tester{

	[Doc("掛上分片。")]
	public override void Init(){
		RegisterListener();
	}
}

[Doc($$"""
本分片測 {{nameof(FileTypingListener)}}：信號觸發、內容讀取與去重。
""")]
public partial class TestFileTypingListener{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterListener();

	[Doc("信號檔被碰之後，內容檔裡的 commit JSON 被派發成 {{nameof(ITypingListener.CommitReceived)}}。")]
	public partial Task<nil> SignalTouch_DispatchesCommit(obj? O);

	[Doc("非 JSON 的內容（寫到一半的快照）不派發、也不拋。")]
	public partial Task<nil> GarbageContent_IsIgnored(obj? O);
}
