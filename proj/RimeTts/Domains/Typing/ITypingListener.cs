namespace RimeTts.Domains.Typing;

[Doc($$"""
使用者打字監聽器。

*為何是「事件」而不是「異步流」*：上屏是離散的瞬間事件，且它的處理（累積成句）必須在
呼叫者線程上立刻完成，否則「間隔多久沒有上屏」這個判斷會被排隊延遲污染。
事件回呼裡只做「塞進緩衝」，慢的工作（翻譯、合成）由流水線自己排。

*為何事件可能在任意線程觸發*：檔案監看器的回呼在線程池上跑。訂閱方要自己保證線程安全。
""")]
public interface ITypingListener{

	[Doc("每收到一次上屏就觸發一次。")]
	public event Action<DtoCommit>? CommitReceived;

	[Doc("每收到一次按鍵事件就觸發一次。目前流水線不訂閱，保留給日後「打字中」的提示。")]
	public event Action<DtoKeyEvent>? KeyEventReceived;

	[Doc($$"""
	開始監聽。
	#Throw[{{nameof(TypedErr)}}][路徑沒配置或不合法（{{nameof(KeysErr.Common.ArgErr__)}}）時。]
	""")]
	public Task<nil> StartAsync(CT Ct);

	[Doc("停止監聽並釋放檔案監看器。重複呼叫安全。")]
	public Task<nil> StopAsync(CT Ct);
}
