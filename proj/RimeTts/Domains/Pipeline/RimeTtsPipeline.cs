namespace RimeTts.Domains.Pipeline;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Typing;
using RimeTts.Infra.Log;
using RimeTts.Infra.Os;

[Doc($$"""
本工具的主流水線：監聽上屏 → 累積成句 → 逐句翻譯 → 有序播放。

*為何是「逐句」而不是「並行處理多句」*：播放本身是串行的，句子之間也必須按時間順序唸；
若兩句並行處理，先翻完的那句可能先播，聽起來就是前後倒置。
故句子一律排隊（一條消費循環），而真正的等待（翻譯、合成）與播放重疊——
第一句在播時，第二句已經在翻譯與合成。

*為何不用 `BackgroundService`／Hosting*：這個進程沒有 Hosting 要管的其他生命週期
（沒有配置熱重載、沒有 DI 作用域、沒有健康檢查）；用 `BackgroundService` 只換來一堆
「StartAsync 裡不能阻塞」的規矩。一條 `RunAsync(CT)` 就是它的全部。
""")]
public interface ISvcRimeTts{

	[Doc($$"""
	跑到 {{nameof(Ct)}} 取消為止。
	#Prm[{{nameof(Ct)}}][取消即收工：停監聽、停播放、卸鉤子。]
	""")]
	public Task RunAsync(CT Ct);
}

[Doc($$"""
{{nameof(ISvcRimeTts)}} 的實現。
#See[{{nameof(ISvcRimeTts)}}][契約與理由。]
""")]
public class RimeTtsPipeline(
	ITypingListener Listener
	,ISentenceBuffer Buffer
	,ISentenceFanout Fanout
	,IAudioPlaylist Playlist
	,IGlobalCancelHotkey Hotkey
	,ILogger<RimeTtsPipeline> Log
):ISvcRimeTts{

	[Doc("成句檢查的週期。取 200ms：比它更短只是空轉，更長會讓成句延遲可感。")]
	public static readonly TimeSpan SegmentTick = TimeSpan.FromMilliseconds(200);

	[Doc("待處理的句子。無界：句子由使用者打字產生，不該被下游反壓。")]
	readonly Channel<str> SentenceQueue = Channel.CreateUnbounded<str>();

	[Doc("跑到取消為止：起三條循環並等它們一起結束。")]
	public async Task RunAsync(CT Ct){
		Listener.CommitReceived += OnCommit;
		Listener.KeyEventReceived += OnKeyEvent;
		Hotkey.Pressed += Playlist.CancelCurrent;
		Hotkey.Start();

		try{
			await Listener.StartAsync(Ct);
			Log.LogInformation("rime tts pipeline started");

			await Task.WhenAll(
				ConsumeSentencesAsync(Ct)
				,SegmentLoopAsync(Ct)
				,Playlist.RunAsync(Ct)
			);
		}
		catch(OperationCanceledException) when(Ct.IsCancellationRequested){
			// 正常停止。
		}
		finally{
			// 先斷輸入，再讓下游把手上做完：這樣退出時不會有一句翻到一半被丟掉。
			Listener.CommitReceived -= OnCommit;
			Listener.KeyEventReceived -= OnKeyEvent;
			Hotkey.Pressed -= Playlist.CancelCurrent;
			SentenceQueue.Writer.TryComplete();
			Playlist.Complete();
			Playlist.CancelCurrent();
			Buffer.Clear();
			await Listener.StopAsync(CancellationToken.None);
			Hotkey.Dispose();
			Log.LogInformation("rime tts pipeline stopped");
		}
	}

	[Doc($$"""
	收到一次上屏：記日誌、累積成句。
	*為何日誌在這一層*：這是「使用者打了什麼」唯一的第一手位置；
	若等成句才記，逐詞上屏的細節就看不到了。
	""")]
	void OnCommit(DtoCommit Commit){
		if(str.IsNullOrWhiteSpace(Commit.Text)){
			return;
		}
		Log.LogCommitText(Commit.Text);
		Emit(Buffer.Append(Commit.Text));
	}

	[Doc("按鍵事件目前不參與決策；保留接收點，避免日後要加「打字中」提示時改介面。")]
	void OnKeyEvent(DtoKeyEvent Event){
		_ = Event;
	}

	[Doc("把成句寫進句子隊列（若真的有句子）。")]
	void Emit(str? Sentence){
		if(str.IsNullOrWhiteSpace(Sentence)){
			return;
		}
		Log.LogSentenceText(Sentence);
		SentenceQueue.Writer.TryWrite(Sentence);
	}

	[Doc($$"""
	逐句消費：一句處理完（翻譯都回來、合成任務都入隊）才處理下一句。
	*這條循環是順序的唯一保證*：{{nameof(AudioPlaylist)}} 只按入隊順序播，
	故入隊順序必須就是字句順序。
	""")]
	async Task ConsumeSentencesAsync(CT Ct){
		await foreach(var Sentence in SentenceQueue.Reader.ReadAllAsync(Ct)){
			try{
				await Fanout.RunAsync(Sentence, Ct);
			}
			catch(OperationCanceledException){
				throw;
			}
			catch(Exception Ex){
				// 一句失敗不該讓整條流水線停掉：記下來，繼續下一句。
				Log.LogError(Ex, "sentence processing failed. sentence={Sentence}", Sentence);
			}
		}
	}

	[Doc("定時檢查「閒置夠久了就成句」。")]
	async Task SegmentLoopAsync(CT Ct){
		using var Timer = new PeriodicTimer(SegmentTick);
		while(await Timer.WaitForNextTickAsync(Ct)){
			Emit(Buffer.TryFlushByIdle());
		}
	}
}
