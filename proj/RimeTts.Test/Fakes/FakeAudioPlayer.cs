namespace RimeTts.Test.Fakes;

using RimeTts.Domains.Tts;
using RimeTts.Domains.Pipeline;

[Doc($$"""
假播放器：只把「播了什麼」記下來，不出聲、不碰音效卡。

*為何需要*：{{nameof(AudioPlaylist)}} 的契約是「按入隊順序播放」，
而真的播放要等音頻時長。用假播放器可以把「順序」單獨測出來。
""")]
public class FakeAudioPlayer : IAudioPlayer{

	[Doc("播放過的檔案，依呼叫順序。")]
	public IList<str> Played{get;} = new List<str>();

	[Doc("假裝的時長；用來驗「超時按時長算」的分支。")]
	public TimeSpan? Duration{get;set;} = TimeSpan.FromSeconds(1);

	[Doc("播放：只記錄。")]
	public Task<nil> PlayAsync(str AudioFile, CT Ct){
		Played.Add(AudioFile);
		return Task.FromResult(NIL);
	}

	[Doc("取時長：回測試指定的值。")]
	public TimeSpan? TryGetDuration(str AudioFile){
		return Duration;
	}
}
