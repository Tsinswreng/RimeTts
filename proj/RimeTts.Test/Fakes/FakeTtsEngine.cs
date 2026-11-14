namespace RimeTts.Test.Fakes;

using RimeTts.Domains.Tts;

[Doc($$"""
假 TTS 引擎：不合成，只回一個可預期的檔名；可指定「這次要失敗」。

*為何需要*：{{nameof(FallbackTts)}} 的行為就是「第一個成功的引擎」，
而真的 gTTS／SAPI 都要網絡或音效卡。用假引擎才測得出「換手」與「全失敗」。
""")]
public class FakeTtsEngine(str Name, bool Fail = false) : ITtsEngine{

	[Doc("配置裡指這個引擎用的名字。")]
	public str Name{get;} = Name;

	[Doc("是否每次呼叫都失敗；測試用它在不同引擎之間造出成功／失敗的組合。")]
	public bool Fail{get;set;} = Fail;

	[Doc("本機上是否可用；測「環境不可用時被剔除」用。")]
	public bool IsAvailable{get;set;} = true;

	[Doc("被呼叫過幾次；用來斷言「前一個成功之後就不該再叫下一個」。")]
	public i32 Calls{get;private set;}

	[Doc("合成：成功回 `<引擎名>.<語言>.mp3`。")]
	public Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		Calls++;
		if(Fail){
			throw new InvalidOperationException($"fake engine {Name} always fails");
		}
		return Task.FromResult($"{Name}.{Req.Language}.mp3");
	}
}
