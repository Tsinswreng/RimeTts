namespace RimeTts;
using Tsinswreng.CsCore;

[Doc(@$"用戶打字監聽器")]
public interface ITypingListener{
	[Doc(@$"RimeLua側每次上屏則C\#側響應一次 {nameof(OnCommit)}")]
	[Doc(@$"TODO 改造成事件、能在事件中取得{nameof(IDtoCommit)}")]
	public obj OnCommit{get;set;}

	[Doc(@$"RimeLua側Process組件每來一次keyevent則C\#側響應一次 {nameof(OnKeyEvent)}")]
	public obj OnKeyEvent{get;set;}
}
