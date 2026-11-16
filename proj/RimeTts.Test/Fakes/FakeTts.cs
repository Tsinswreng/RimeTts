namespace RimeTts.Test.Fakes;

using RimeTts.Domains.Tts;

[Doc($$"""
假 TTS 服務（{{nameof(ITts)}}，非引擎）：可控延遲、可控失敗，並記錄呼叫順序。

*與 {{nameof(FakeTtsEngine)}} 的分工*：引擎測的是 {{nameof(FallbackTts)}} 的換手；
本假測的是 {{nameof(RimeTts.Domains.Pipeline.SentenceFanout)}} 的「等翻譯→合成」鏈，
故要能模擬「合成很慢」「合成拋異常」「合成被超時取消」這些時序。
""")]
public class FakeTts : ITts{

	[Doc("每次合成的延遲；延遲期間回應取消令牌（與兩個真實引擎一致：gTTS 的 HttpClient、SAPI 的 SpeakAsyncCancel），用來觸發扇出層的生成超時。")]
	public TimeSpan Delay{get;set;} = TimeSpan.Zero;

	[Doc("每次呼叫都拋這個異常（設了就不走延遲）。")]
	public Exception? FailWith{get;set;}

	[Doc("被呼叫過的（語言, 文本），依順序。")]
	public IList<(str Lang, str Text)> Calls{get;} = new List<(str, str)>();

	[Doc("合成：按配置延遲／失敗；成功回 `<語言>.<文本>.mp3`。")]
	public async Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		Calls.Add((Req.Language, Req.Text));
		if(FailWith is not null){
			throw FailWith;
		}
		if(Delay > TimeSpan.Zero){
			await Task.Delay(Delay, Ct);
		}
		return $"{Req.Language}.{Req.Text}.mp3";
	}
}
