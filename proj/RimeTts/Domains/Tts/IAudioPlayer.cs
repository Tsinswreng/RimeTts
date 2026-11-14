namespace RimeTts.Domains.Tts;

using NAudio.Wave;

[Doc($$"""
音頻播放器：一次只播一段，播完才返回。

*為何全應用只有一個實現*：播放是獨佔資源（同一張音效卡、同一個進程的 WaveOut），
兩個元件各自 new 一個 WaveOut 就會互相打斷。故它是單例，且由流水線統一排隊。

*為何取消要「Stop 之後再檢查一次」*：`WaveOutEvent.Stop()` 會觸發 `PlaybackStopped`
且不帶例外，於是等待者會以為「正常播完」。取消語義必須由 Token 自己保證，
不能靠回呼的性質。

*為何同時提供時長查詢*：播放超時要按「這段音頻本該多長」算，而不是一個固定值；
固定值要嘛太短（長句被切）、要嘛太長（取消後還要等）。
""")]
public interface IAudioPlayer{

	[Doc($$"""
	把音頻檔播完。
	#Prm[{{nameof(AudioFile)}}][.mp3／.wav 的絕對路徑。]
	#Throw[{{nameof(TypedErr)}}][檔案不存在（{{nameof(KeysErr.Tts.AudioFileNotFound__)}}）時。]
	#Throw[{{nameof(OperationCanceledException)}}][{{nameof(Ct)}} 被取消時；播放會立刻停止。]
	""")]
	public Task PlayAsync(str AudioFile, CT Ct);

	[Doc($$"""
	取音頻檔的時長；讀不出來回 null（呼叫方據此退回一個默認超時）。
	#Prm[{{nameof(AudioFile)}}][音頻檔路徑。]
	""")]
	public TimeSpan? TryGetDuration(str AudioFile);
}

[Doc($$"""
{{nameof(IAudioPlayer)}} 的實現：NAudio 的 `WaveOutEvent`（走 WinMM）。
#See[{{nameof(IAudioPlayer)}}][契約與理由。]
""")]
public class AudioPlayer : IAudioPlayer{

	[Doc("播放同一個設備需要互斥；用信號量而不是 lock，因為等待期間不能佔住線程池的線程。")]
	static readonly SemaphoreSlim PlayGate = new(1, 1);

	[Doc($$"""
	播放到結束或取消。
	#Prm[{{nameof(AudioFile)}}][音頻檔路徑。]
	#Prm[{{nameof(Ct)}}][取消即停止。]
	""")]
	public async Task PlayAsync(str AudioFile, CT Ct){
		if(!File.Exists(AudioFile)){
			throw KeysErr.Tts.AudioFileNotFound__.ToErr(AudioFile);
		}

		await PlayGate.WaitAsync(Ct);
		try{
			await Task.Run(async () => {
				Ct.ThrowIfCancellationRequested();
				// AudioFileReader 自己按副檔名／檔頭挑解碼器，故 mp3 與 wav 走同一條路。
				using var Reader = new AudioFileReader(AudioFile);
				using var Out = new WaveOutEvent();
				var Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				Out.PlaybackStopped += (_, E) => {
					if(E.Exception is not null){
						Tcs.TrySetException(E.Exception);
					} else {
						Tcs.TrySetResult();
					}
				};

				// 取消：先 Stop（讓設備交出緩衝），再由下面的 ThrowIfCancellationRequested 定調。
				using var Reg = Ct.Register(() => {
					try{
						Out.Stop();
					}
					catch{
						// Stop 本身失敗不影響取消語義：外面仍會拋 OperationCanceledException。
					}
				});

				Out.Init(Reader);
				Out.Play();
				await Tcs.Task;
				// 見本類說明：Stop 引起的「正常播完」要在這裡被糾正成取消。
				Ct.ThrowIfCancellationRequested();
			}, Ct);
		}
		finally{
			PlayGate.Release();
		}
	}

	[Doc($$"""
	讀音頻時長；格式不認識或檔案壞掉時回 null。
	#Prm[{{nameof(AudioFile)}}][音頻檔路徑。]
	""")]
	public TimeSpan? TryGetDuration(str AudioFile){
		try{
			using var Reader = new AudioFileReader(AudioFile);
			var D = Reader.TotalTime;
			return D > TimeSpan.Zero ? D : null;
		}
		catch{
			// 時長只用來決定超時；讀不到就讓呼叫方用默認值，不該讓它變成一次播放失敗。
			return null;
		}
	}
}
