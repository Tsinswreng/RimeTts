namespace RimeTts.Domains.Tts;

using RimeTts.Domains.Translate;

[Doc($$"""
文本轉語音的設定。

*為何引擎是「有序清單」而不是「選一個」*：線上那條腿（gTTS）音質最好但依賴網絡；
離線那條（SAPI）隨時可用但音色機械。清單讓「優先線上、失敗退回離線」成為配置而不是代碼裡的特判。

*為何輸出目錄可配置*：音頻是快取物，放在哪由使用者決定（例如放到 RAM 盤）。
留空＝執行檔目錄下的 `tts-output`。
""")]
public class OptTts{

	[Doc(@$"生成的音頻檔輸出目錄。空串＝執行檔目錄下的 `tts-output`。
	檔名是「文本內容的雜湊」，故同一句話第二次不再生成，直接播放已存在的檔案。")]
	public str OutputDir{get;set;} = "";

	[Doc($$"""
	全局引擎優先級，越前越優先。語言條目自己的 {{nameof(LanguageProfile.TtsEngines)}} 非空時以它為準。

	目前支援的名字（大小寫不敏感）：`{{nameof(GttsViaHttpTts.EngineName)}}`（人名即 gTTS）、
	`{{nameof(SystemSpeechTts.EngineName)}}`（SAPI）。
	""")]
	public IList<str> Engines{get;set;} = new List<str>{ GttsViaHttpTts.EngineName, SystemSpeechTts.EngineName };
}
