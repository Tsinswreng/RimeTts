namespace RimeTts.Infra.Os;

[Doc($$"""
全域中斷熱鍵。

*為何要抽象*：安裝 Win32 低級鍵盤鉤子是有副作用的操作（它會拿到整個系統的按鍵），
測試與「不需要中斷」的場合不該被迫裝它。故流水線只依賴這個接口，
真實實現由宿主註冊。

#Eg[
	```cs
	// 流水線
	Hotkey.Pressed += StopCurrentPlayback;
	Hotkey.Start();
	```
]
""")]
public interface IGlobalCancelHotkey : IDisposable{

	[Doc("熱鍵被按下。可能來自任意線程（鉤子線程），訂閱方要自行保證線程安全。")]
	public event Action? Pressed;

	[Doc($$"""
	開始收聽。重複呼叫是 no-op。
	#Throw[{{nameof(TypedErr)}}][鉤子安裝失敗（{{nameof(KeysErr.Hotkey.EscHookFailed__)}}）時。]
	""")]
	public void Start();
}
