namespace RimeTts.Test.Fakes;

using RimeTts.Infra;

[Doc($$"""
假應用儲存區：回一個固定的目錄，讓「空輸出目錄解析成預設位置」這條分支可以斷言。
""")]
public class FakeBaseDir(str Dir) : I_GetBaseDir{

	[Doc("假目錄本體。")]
	public str Dir{get;} = Dir;

	[Doc("回傳假目錄。")]
	public str GetBaseDir(){
		return Dir;
	}
}
