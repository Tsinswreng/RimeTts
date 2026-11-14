namespace RimeTts;

[Doc($$"""
應用層面的固定事實：名字、日誌檔名、配置檔名。

*為何不散在各地*：名字會出現在日誌檔名、視窗標題（日後若有）、配置檔名與說明文中；
寫死在各處就會出現「日誌叫 RimeTts.log、配置叫 rimetts.jsonr」這種大小寫不一致。
""")]
public static class AppInfo{

	[Doc("應用名。用作日誌檔名與診斷訊息前綴。")]
	public const str Name = "RimeTts";
}
