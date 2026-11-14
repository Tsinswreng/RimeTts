namespace RimeTts.Domains.Typing;

[Doc($$"""
Lua 插件與 C\# 之間的文件交互位置。

*為何是文件而不是管道*：Rime 的 Lua 插件跑在輸入法進程裡，不能保證與本進程同時存活；
一個「寫入內容檔案、再摸一下信號檔案」的約定讓兩邊完全解耦——本進程晚一秒啟動也不會漏掉事後的通知，
而管道要求兩端同時在線。代價是輪詢／監看檔案，見 {{nameof(FileTypingListener)}}。

*為何兩個檔案*：內容檔案常被覆寫、可能被寫到一半；信號檔案只負責「變了」這一個 bit。
先寫內容、後摸信號，是本協議唯一的時序約定。
""")]
public class OptFileInteractor{

	[Doc(@$"具體傳輸內容檔案的路徑。Lua 側向此檔寫入一段 JSON，然後 C\# 來讀。
	寫入必須是「整份覆寫」：本端讀到不完整 JSON 會重試幾次，但無法分辨『還沒寫完』與『真的壞了』。")]
	public str ContentFile{get;set;} = "";

	[Doc(@$"信號檔案的路徑。C\# 監看它的變動，一有變動就去讀 {nameof(ContentFile)}。
	Lua 側只要改寫它的時間戳或大小即可，內容本身無意義。")]
	public str SignalFile{get;set;} = "";
}
