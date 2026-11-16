namespace RimeTts.Domains.Pipeline;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Typing;
using RimeTts.Infra;
using RimeTts.Infra.Log;
using RimeTts.Infra.Os;

[Doc($$"""
本工具的主流水線：監聽上屏 → 累積成句 → 逐句翻譯 → 有序播放。

*為何是「逐句」而不是「並行處理多句」*：播放本身是串行的，句子之間也必須按時間順序唸；
若兩句並行入隊，先翻完的那句可能先入隊，聽起來就是前後倒置。
故句子一律排隊（一條消費循環）。「逐句」約束的是**入隊順序**；
真正的等待（翻譯、合成）與播放重疊——上一句還在播時，下一句已經在翻譯與合成。

*為何不用 `BackgroundService`／Hosting*：這個進程沒有 Hosting 要管的其他生命週期
（沒有配置熱重載、沒有 DI 作用域、沒有健康檢查）；用 `BackgroundService` 只換來一堆
「StartAsync 裡不能阻塞」的規矩。一條 `RunAsync(CT)` 就是它的全部。

*殭屍態的防線*：本類跑三條「本該跑到全局取消為止」的循環。曾經它們被一個
`Task.WhenAll` 等着，任何一條因意外異常先死，另外兩條照常活着，
`WhenAll` 永不完成、清理不執行——進程變成「監聽照常、永不翻譯」的殭屍，
且死時零日誌（2026-11-16 實測事故，審查 BUG-1）。
現在由實現裡的循環監視器（`RunLoopsUntilExit`）盯着：任一循環先退（無論故障還是「正常」提前返回）
都記 Error、取消其餘、讓進程退出——*寧可退出也不殭屍*。
""")]
public interface ISvcRimeTts{

	[Doc($$"""
	跑到 {{nameof(Ct)}} 取消為止。
	#Prm[{{nameof(Ct)}}][取消即收工：停監聽、停播放、卸鉤子。]
	""")]
	public Task<nil> RunAsync(CT Ct);
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

	[Doc($$"""
	跑到取消為止：起三條循環並等它們一起結束。
	意外結束（某條循環故障）會由循環監視器（`RunLoopsUntilExit`）轉成「記錄 → 取消其餘 → 上拋」，
	最終讓進程以非零退出碼結束，而不是無聲地癱瘓。
	""")]
	public async Task<nil> RunAsync(CT Ct){
		Listener.CommitReceived += OnCommit;
		Listener.KeyEventReceived += OnKeyEvent;
		Hotkey.Pressed += Playlist.CancelCurrent;
		Hotkey.Start();

		try{
			await Listener.StartAsync(Ct);
			Log.LogInformation("rime tts pipeline started");

			await RunLoopsUntilExit(Ct);
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
		return NIL;
	}

	[Doc($$"""
	跑三條常駐循環，並監視它們的生命體徵。

	*監視規則*：三條循環（句子消費、成句定時、播放）本該都活到 {{nameof(Ct)}} 取消。
	`Task.WhenAny` 一旦回來而 {{nameof(Ct)}} 尚未取消，就說明有人先退了——
	不論它是拋了異常還是「莫名其妙正常返回」（後者同樣意外：隊列不會自己關閉）。
	此時記一條含原因的 Error、取消其餘兩條，然後把異常原樣上拋，
	讓宿主（`RimeTts.Cli` 的入口）把進程帶著非零退出碼收掉。
	殭屍態（其他人還活着、`WhenAll` 永不完成）從結構上不可能再出現。
	""")]
	async Task<nil> RunLoopsUntilExit(CT Ct){
		using var Graceful = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var ConsumeTask = ConsumeSentencesAsync(Graceful.Token);
		var SegmentTask = SegmentLoopAsync(Graceful.Token);
		var PlayTask = Playlist.RunAsync(Graceful.Token);

		var EndedWithoutFault = false;
		var First = await Task.WhenAny(ConsumeTask, SegmentTask, PlayTask);
		if(!Ct.IsCancellationRequested){
			var Name = LoopName(First, ConsumeTask, SegmentTask, PlayTask);
			if(First.IsFaulted){
				Log.LogError(First.Exception, "loop {Loop} crashed; shutting down the whole pipeline", Name);
			}else{
				// 沒拋異常卻先退了：同樣意外（這些循環本該跑到取消為止），也要留痕。
				// 記住它：收尾之後要拋錯讓進程以非零退出碼結束，不能裝作正常停止。
				EndedWithoutFault = true;
				Log.LogError("loop {Loop} ended unexpectedly; shutting down the whole pipeline", Name);
			}
			Graceful.Cancel();
		}

		// 收尾：全局取消時其餘循環以 OCE 結束，屬正常；
		// 故障收場時把元兇異常原樣上拋（WhenAll 只會拋其中第一個故障的異常）。
		try{
			await Task.WhenAll(ConsumeTask, SegmentTask, PlayTask);
		}
		catch(OperationCanceledException) when(Ct.IsCancellationRequested || Graceful.IsCancellationRequested){
			// 兩種取消（使用者關停／監視器關停）之下的 OCE 都不是這裡要報的錯；
			// 若還有非 OCE 的故障，WhenAll 會在下一個 await 把它拋出來。
		}
		if(EndedWithoutFault){
			// 「無異常地提前退出」沒有堆棧可帶，用帶鍵的錯把這件事本身報出去。
			throw KeysErr.Common.UnknownErr.WithMsg(
				Todo.I18n("流水線有循環意外提前結束，進程已停止；詳見日誌。"));
		}
		return NIL;
	}

	[Doc("三條循環的名字；監視器記日誌用。")]
	static str LoopName(Task First, Task Consume, Task Segment, Task Play){
		if(ReferenceEquals(First, Consume)){
			return nameof(ConsumeSentencesAsync);
		}
		if(ReferenceEquals(First, Segment)){
			return nameof(SegmentLoopAsync);
		}
		return nameof(IAudioPlaylist.RunAsync);
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
	逐句消費：一句的入隊完成（見 {{nameof(ISentenceFanout.RunAsync)}}）才處理下一句——
	入隊順序就是播放順序的唯一保證。

	*異常的兩段過濾（BUG-1 修復點之二）*：處理一句時拋出的
	`OperationCanceledException` 只有在 {{nameof(Ct)}} 已取消時才代表關停、才上拋；
	其餘（包括舊代碼會漏過來的「單次請求超時」殘餘，以及任何未預期的異常）
	一律記 Error 後繼續下一句——一句話的失敗不該殺死整條流水線。
	""")]
	async Task<nil> ConsumeSentencesAsync(CT Ct){
		await foreach(var Sentence in SentenceQueue.Reader.ReadAllAsync(Ct)){
			try{
				await Fanout.RunAsync(Sentence, Ct);
			}
			catch(OperationCanceledException) when(Ct.IsCancellationRequested){
				// 全局關停：正常退出循環。
				throw;
			}
			catch(Exception Ex){
				// 一句失敗不該讓整條流水線停掉：記下來，繼續下一句。
				Log.LogError(Ex, "sentence processing failed. sentence={Sentence}", Sentence);
			}
		}
		return NIL;
	}

	[Doc("定時檢查「閒置夠久了就成句」。")]
	async Task<nil> SegmentLoopAsync(CT Ct){
		using var Timer = new PeriodicTimer(SegmentTick);
		while(await Timer.WaitForNextTickAsync(Ct)){
			Emit(Buffer.TryFlushByIdle());
		}
		return NIL;
	}
}
