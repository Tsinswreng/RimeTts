namespace RimeTts.Domains.Typing;

using RimeTts.Infra.Json;

[Doc($$"""
Lua 插件送來的事件種類。

*為何用字串而不是數字*：Lua 那邊寫的是 `Type = "Commit"` 這種人看得懂的值。
用 `nameof` 取得同一個詞，兩邊就不會漂移。

*為何不認識的種類要被忽略而不是報錯*：插件與本工具的版本可以不同步；
舊版工具遇到新版插件的新事件時，正確行為是「看不懂就跳過」，而不是整個罷工。
""")]
public enum ELuaDtoType{
	[Doc("Process 組件每觸發一次按鍵事件就送一個。目前 C# 側不關心。")]
	KeyEvent,

	[Doc("一次上屏。帶上屏字。")]
	Commit,
}

[Doc($$"""
從內容檔案讀到的原始事件。

*為何只有兩個欄位*：這是唯一的跨進程契約，越窄越不容易壞。
`Text` 只在 {{nameof(ELuaDtoType.Commit)}} 時有意義。
""")]
public class DtoLuaEvent : IAppSerializable{

	[Doc("事件種類，取自 {{nameof(ELuaDtoType)}} 的成員名。")]
	public str? Type{get;set;}

	[Doc("上屏字（`ctx.get_commit_text()` 的結果）。")]
	public str? Text{get;set;}
}

[Doc("一次上屏。")]
public record DtoCommit{

	[Doc("完整上屏字。可能是一兩個字（逐詞上屏），也可能是一整句（貼上）。")]
	public str Text{get;set;} = "";
}

[Doc($$"""
一次按鍵事件。
*目前沒有任何欄位*：C\# 側不需要知道按了什麼鍵——它的用途只是「使用者還在打字」這個信號，
而那個信號已經由「有新上屏」表達了。保留型別是為了契約穩定。
""")]
public record DtoKeyEvent;
