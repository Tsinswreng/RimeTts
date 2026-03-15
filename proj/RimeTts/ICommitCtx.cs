namespace RimeTts;
using Tsinswreng.CsCore;

[Doc(@$"一次上屏對一個{nameof(IDtoCommit)}")]
public interface IDtoCommit{
	[Doc(@$"完整上屏字 即 rimelua端ctx.get_commit_text()所得")]
	public str Text{get;set;}
}


[Doc(@$"RimeLua Process組件中每觸發一次按鍵事件
就向C\#側發送一個{nameof(IDtoKeyEvent)}")]
public interface IDtoKeyEvent{
}


[Doc(@$"由多個{nameof(IDtoCommit)}組成的句子")]
public interface ISentence{
	[Doc(@$"原始中文句子")]
	public str Text{get;set;}
}


