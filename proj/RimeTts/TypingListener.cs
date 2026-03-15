namespace RimeTts;
using Tsinswreng.CsCore;

[Doc(@$"用戶打字監聽器")]
public interface ITypingListener{
	[Doc(@$"RimeLua側每次上屏則C\#側響應一次")]
	public event Action<IDtoCommit>? CommitReceived;

	[Doc(@$"RimeLua側Process組件每來一次keyevent則C\#側響應一次")]
	public event Action<IDtoKeyEvent>? KeyEventReceived;

	[Doc(@$"啓動監聽")]
	public Task StartAsync(CT Ct);

	[Doc(@$"停止監聽")]
	public Task StopAsync(CT Ct);
}
