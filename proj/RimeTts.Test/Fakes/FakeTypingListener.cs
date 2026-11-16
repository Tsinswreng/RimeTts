namespace RimeTts.Test.Fakes;

using RimeTts.Domains.Typing;

[Doc($$"""
假監聽器：不碰檔案系統，由測試直接觸發 {{nameof(CommitReceived)}}。

*為何需要*：`RimeTtsPipeline` 的輸入側契約是 {{nameof(ITypingListener)}}，
用假監聽器才能在不部署 Rime 插件的前提下餵上屏。
""")]
public class FakeTypingListener : ITypingListener{

	[Doc("上屏事件；測試觸發用。")]
	public event Action<DtoCommit>? CommitReceived;

	[Doc("按鍵事件；測試觸發用。")]
	public event Action<DtoKeyEvent>? KeyEventReceived;

	[Doc("StartAsync 被叫過幾次。")]
	public i32 StartCount{get;private set;}

	[Doc("StopAsync 被叫過幾次。")]
	public i32 StopCount{get;private set;}

	[Doc("開始：只計數。")]
	public Task<nil> StartAsync(CT Ct){
		StartCount++;
		return Task.FromResult(NIL);
	}

	[Doc("停止：只計數。")]
	public Task<nil> StopAsync(CT Ct){
		StopCount++;
		return Task.FromResult(NIL);
	}

	[Doc("模擬一次上屏。")]
	public void EmitCommit(str Text){
		CommitReceived?.Invoke(new DtoCommit{ Text = Text });
	}
}
