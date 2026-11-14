namespace RimeTts.Domains.Tts;

[Doc($$"""
一次語音合成請求。

*為何帶 {{nameof(PreferredEngines)}}*：同一個語言條目可以指定自己的引擎優先級；
空清單表示「用全局設定」。把「用哪些引擎」放在請求上而不是服務上，
是因為它取決於語言——本機不一定每個語言都有系統語音。
""")]
public record ReqGenEtPlay{

	[Doc("要唸的文本（已翻譯、已去掉首尾空白）。")]
	public str Text{get;set;} = "";

	[Doc("語言代碼（BCP-47 兩字母部分，如 `en`）。引擎據此選音色。")]
	public str Language{get;set;} = "en";

	[Doc("本請求的引擎優先級；空＝用全局 OptTts.Engines。")]
	public IList<str>? PreferredEngines{get;set;}
}

[Doc($$"""
文本轉語音服務的抽象。

*只有「合成」一件事*：播放與合成是兩件不同的失敗模式——合成依賴網絡或系統語音，
播放依賴音效卡與解碼器。把播放抽到 {{nameof(IAudioPlayer)}} 之後，
每個引擎只負責「文字 → 檔案」，播放共用一份實現，也就不會出現
「用 A 引擎生成、卻用 B 引擎的播放器去放」這種按副檔名猜的邏輯。

*輸出是檔案路徑而不是流*：同一句話會被重複朗讀（改字重打），檔案快取讓我們第二次直接播；
而播放器（NAudio）本來就以檔案為輸入。
""")]
public interface ITts{

	[Doc($$"""
	合成語音並回傳音頻檔路徑。檔名由「語言 ＋ 文本」的雜湊決定，已存在就直接回傳。
	#Prm[{{nameof(Req)}}][合成請求。]
	#Rtn[音頻檔的絕對路徑（.mp3 或 .wav）。]
	#Throw[{{nameof(TypedErr)}}][文本為空（{{nameof(KeysErr.Common.ArgErr__)}}）或合成失敗時。]
	""")]
	public Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct);
}

[Doc($$"""
帶名字的 TTS 引擎。

*為何要名字*：配置裡寫的是 `"gTTS"`、`"SystemSpeech"` 這種字串；
{{nameof(FallbackTts)}} 要靠名字從清單裡挑。名字與型別放一起，新增引擎就只改引擎本身，
不必再去改一個「名字 ↔ 型別」的對照表。

*為何 FallbackTts 收 {{nameof(ITtsEngine)}} 而不是具體類別*：收具體類別等於把
「有哪幾個引擎」寫死在它身上，既不能測（測試要造假引擎）、也不能擴展。
""")]
public interface ITtsEngine : ITts{

	[Doc("配置裡指這個引擎用的名字（大小寫不敏感）。")]
	public str Name{get;}

	[Doc($$"""
	本機上這個引擎現在能不能用。

	*為何需要它*：有些引擎的可用性是**執行環境**決定的，不是配置決定的——
	離線那條腿（SAPI）在 NativeAOT 下它的 COM 包裝類建構子無效，
	每次呼叫都抛；若不先問一句，每個語言每一句都要白拋一次、日誌也會被灌滿。
	{{nameof(FallbackTts)}} 直接把不可用的引擎從候選裡剔除。

	*契約*：這是一個便宜的查詢（結果快取在實現裡），可以在每次合成前呼叫；
	回 false 之後不保證永遠 false（使用者可能剛裝了語音），故不寫成一次性結論。
	""")]
	public bool IsAvailable{get;}
}
