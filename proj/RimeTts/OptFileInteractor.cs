using Tsinswreng.CsCore;

namespace RimeTts;

[Doc(@$"文件交互配置
RimeLua插件與C#側通過文件交互")]
public interface IOptFileInteractor{

	[Doc(@$"具體傳輸內容。
	Lua側向此文件中寫入內容、然後C#來讀。
	文件內容形式爲Json純文本。
	#See[{nameof(IBaseLuaDto)}]
	")]
	public str ContentFile{get;set;}
	[Doc(@$"信號文件。
	C#程序監聽此文件、當此文件有變化時、再去讀{nameof(ContentFile)}
	然後觸發事件。
	")]
	public str SignalFile{get;set;}
}

