using Tsinswreng.CsCore;

namespace RimeTts;

public enum ELuaDtoType{
	KeyEvent,
	Commit,
}


public interface IBaseLuaDto{
	[Doc(@$"取自{nameof(ELuaDtoType)}的成員再ToString")]
	public str Type{get;set;}
}


[Doc(@$"Lua側觸發按鍵事件
#See[{nameof(IDtoKeyEvent)}]
")]
public interface ILuaDtoKeyEvent:IBaseLuaDto{
//目前不需要任何字段、C#側不關心發生了甚麼按鍵事件
}

[Doc(@$"Lua側觸發Commit
#See[{nameof(IDtoCommit)}]
")]
public interface ILuaDtoCommit:IBaseLuaDto{
	[Doc(@$"上屏字
	#See[{nameof(IDtoCommit.Text)}]
	")]
	public str Text{get;set;}
}
