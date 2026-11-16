namespace RimeTts.Test.Fakes;

using RimeTts.Infra.Os;

[Doc($$"""
假熱鍵：不安鉤子，由測試直接觸發 {{nameof(Pressed)}}。

*為何需要*：`RimeTtsPipeline` 依賴 {{nameof(IGlobalCancelHotkey)}}；
真鉤子要拿整個系統的按鍵，測試不該裝。
""")]
public class FakeCancelHotkey : IGlobalCancelHotkey{

	[Doc("熱鍵事件；測試觸發用。")]
	public event Action? Pressed;

	[Doc("Start 被叫過幾次。")]
	public i32 StartCount{get;private set;}

	[Doc("Dispose 被叫過幾次。")]
	public i32 DisposeCount{get;private set;}

	[Doc("開始：只計數。")]
	public void Start(){
		StartCount++;
	}

	[Doc("釋放：只計數。")]
	public void Dispose(){
		DisposeCount++;
	}

	[Doc("模擬一次熱鍵按下。")]
	public void Press(){
		Pressed?.Invoke();
	}
}
