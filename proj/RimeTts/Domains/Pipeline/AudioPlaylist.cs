namespace RimeTts.Domains.Pipeline;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RimeTts.Domains.Tts;

[Doc($$"""
播放隊列裡的一項：某個語言的最終音頻（或一個還在進行中的生成工作）。

*為何存 {{nameof(Task)}} 而不是路徑*：合成與播放要重疊——第一段還在播，第二段的音頻就該開始抓。
把「尚未完成的工作」放進隊列，隊列就能在輪到它時才等它；生成則早在入隊時就開始了。

*這個任務現在是完整的一條鏈*（2026-11-16 審查 BUG-2 重構後）：
「翻譯 → 合成」都在裡面，入隊發生在鏈啟動的同一刻（見 {{nameof(ISentenceFanout)}}）。
於是隊列順序仍然等於播放順序，而一個懸住的語言最多佔住它自己那一格。

*為何 {{nameof(GenTask)}} 回可空*：生成失敗不是致命錯誤（這個語言沒唸出來就算了），
隊列據此跳過它繼續下一項；若讓它拋，整條播放循環得為一個語言中斷。
""")]
public record AudioJob{

	[Doc("語言代碼；僅用於日誌與超時訊息。")]
	public str Language{get;set;} = "";

	[Doc("任務鏈的結果：音頻檔路徑，或 null 表示這個語言翻譯／合成失敗。")]
	public Task<str?> GenTask{get;set;} = null!;
}

[Doc($$"""
有序播放器：一邊等生成、一邊依序播放。

*它解決的問題*：多段語音不能並行播放（會疊在一起），但合成可以並行。
故出口只有一條（本類的消費循環），入口可以有很多（每個語言一條任務鏈）。

*為何順序能保證*：入隊順序就是播放順序；而 {{nameof(Add)}} 的呼叫順序由兩層保證——
語言順序（{{nameof(SentenceFanout)}} 按配置逐個 `Add`）與句子順序
（`RimeTtsPipeline` 的唯一句子消費循環逐句跑 {{nameof(ISentenceFanout.RunAsync)}}）。
本類不去重排、也不排序——順序是呼叫方的責任，這樣它才能表達「先英後日」這種語義。

*超時為何按音頻長度算*：固定超時要嘛把長句切掉、要嘛讓取消後還要空等。
時長讀不到時退回一個保底值（見 MinPlayTimeoutSec 與 PlayGraceSec）。
*超時值來自配置*（`KeysAppCfg.Pipeline`，經 {{nameof(OptPipeline)}} 注入；
2026-11-16 審查 MAINT-3 前這裏是寫死的常量）。
""")]
public interface IAudioPlaylist{

	[Doc($$"""
	追加一項。呼叫順序即播放順序。
	*為何不是 async*：寫入無界隊列不會阻塞，而呼叫方（句子處理）不該為此多一個 await 點。
	""")]
	public void Add(AudioJob Job);

	[Doc($$"""
	跑播放循環，直到隊列被關閉或 {{nameof(Ct)}} 取消。
	*為何要獨立一條*：播放會佔住很長時間（整句音頻），不能擋住翻譯與合成。
	""")]
	public Task<nil> RunAsync(CT Ct);

	[Doc("宣告不會再有新項；循環在把現有的播完之後結束。")]
	public void Complete();

	[Doc("打斷當前正在播的那一段（全域 Esc）。不影響隊列裡其餘的項。")]
	public void CancelCurrent();
}

[Doc($$"""
{{nameof(IAudioPlaylist)}} 的實現。
#See[{{nameof(IAudioPlaylist)}}][契約與理由。]
""")]
public class AudioPlaylist : IAudioPlaylist{

	[Doc("播放器；真正的設備操作在那一層。")]
	readonly IAudioPlayer Player;

	[Doc("編排層超時設定（播放保底超時與餘量）。")]
	readonly OptPipeline Opt;

	[Doc("日誌。")]
	readonly ILogger<AudioPlaylist> Log;

	[Doc("無界隊列：句子的產生速度由使用者打字決定，不該被播放進度反壓。")]
	readonly Channel<AudioJob> Queue = Channel.CreateUnbounded<AudioJob>();

	[Doc("當前播放的取消源；全域 Esc 與超時都作用在它身上。")]
	readonly Lock PlaybackLock = new();

	[Doc("當前的取消源；null 表示沒有在播。")]
	CancellationTokenSource? CurrentPlayback;

	[Doc($$"""
	接上播放器、超時設定與日誌。
	#Prm[{{nameof(Player)}}][音頻播放器（單例，獨佔音效卡）。]
	#Prm[{{nameof(Opt)}}][編排層超時設定。]
	#Prm[{{nameof(Log)}}][日誌。]
	""")]
	public AudioPlaylist(IAudioPlayer Player, OptPipeline Opt, ILogger<AudioPlaylist> Log){
		this.Player = Player;
		this.Opt = Opt;
		this.Log = Log;
	}

	[Doc("追加一項；失敗（隊列已關閉）只記一條 Debug。")]
	public void Add(AudioJob Job){
		if(!Queue.Writer.TryWrite(Job)){
			Log.LogDebug("audio queue is closed; job dropped. lang={Lang}", Job.Language);
		}
	}

	[Doc("宣告結束。")]
	public void Complete(){
		Queue.Writer.TryComplete();
	}

	[Doc("打斷當前播放。")]
	public void CancelCurrent(){
		lock(PlaybackLock){
			CurrentPlayback?.Cancel();
		}
	}

	[Doc("播放循環本體：取一項 → 等它的音頻 → 播完再取下一項。")]
	public async Task<nil> RunAsync(CT Ct){
		try{
			while(await Queue.Reader.WaitToReadAsync(Ct)){
				while(Queue.Reader.TryRead(out var Job)){
					await ProcessJobAsync(Job, Ct);
				}
			}
		}
		catch(OperationCanceledException) when(Ct.IsCancellationRequested){
			// 正常停止：不是錯誤。
		}
		return NIL;
	}

	[Doc($$"""
	處理一項：等任務鏈的結果，再按長度超時播放。
	*為何等 {{nameof(AudioJob.GenTask)}} 沒有再包一層超時*：鏈的每一段已經各自有上限
	（翻譯在翻譯器內按 `Translator.TimeoutSec`、合成在扇出層按 `Pipeline.GenerateTimeoutSec`），
	這裏再疊一個只會與它們打架（舊版三層超時互毆正是教訓）；
	鏈的總時長因此有界（兩個上限之和），不會無限等待。
	#Prm[{{nameof(Job)}}][隊列項。]
	""")]
	async Task<nil> ProcessJobAsync(AudioJob Job, CT Ct){
		str? File;
		try{
			File = await Job.GenTask;
		}
		catch(OperationCanceledException) when(Ct.IsCancellationRequested){
			// 全局關停：如實上拋讓循環結束。
			throw;
		}
		catch(OperationCanceledException){
			// 非全局的取消殘餘（防禦）：按這一格失敗處理，不殺播放循環。
			Log.LogWarning("audio generation canceled unexpectedly. lang={Lang}", Job.Language);
			return NIL;
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "audio generation failed. lang={Lang}", Job.Language);
			return NIL;
		}
		if(str.IsNullOrWhiteSpace(File)){
			return NIL;
		}

		var Timeout = ResolvePlayTimeout(File);
		using var TimeoutCts = new CancellationTokenSource(Timeout);
		using var PlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(Ct, TimeoutCts.Token);
		lock(PlaybackLock){
			CurrentPlayback = PlaybackCts;
		}
		try{
			await Player.PlayAsync(File, PlaybackCts.Token);
		}
		catch(OperationCanceledException) when(!Ct.IsCancellationRequested && TimeoutCts.IsCancellationRequested){
			Log.LogWarning("audio play timed out. lang={Lang}; file={File}; timeoutSec={Timeout}",
				Job.Language, File, Timeout.TotalSeconds);
		}
		catch(OperationCanceledException) when(!Ct.IsCancellationRequested){
			// Esc 打斷：預期行為，只需留痕。
			Log.LogWarning("audio play canceled by global esc. lang={Lang}; file={File}", Job.Language, File);
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "audio play failed. lang={Lang}; file={File}", Job.Language, File);
		}
		finally{
			lock(PlaybackLock){
				if(ReferenceEquals(CurrentPlayback, PlaybackCts)){
					CurrentPlayback = null;
				}
			}
		}
		return NIL;
	}

	[Doc("按音頻真實長度算超時；讀不到時退回保底值。")]
	TimeSpan ResolvePlayTimeout(str File){
		var MinTimeout = TimeSpan.FromSeconds(Math.Max(1, Opt.MinPlayTimeoutSec));
		var Grace = TimeSpan.FromSeconds(Math.Max(0, Opt.PlayGraceSec));
		var Duration = Player.TryGetDuration(File);
		if(Duration is null){
			return MinTimeout;
		}
		var Timeout = Duration.Value + Grace;
		return Timeout > MinTimeout ? Timeout : MinTimeout;
	}
}
