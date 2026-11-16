namespace RimeTts.Domains.Tts;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using RimeTts.Infra;

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
	public Task<nil> PlayAsync(str AudioFile, CT Ct);

	[Doc($$"""
	取音頻檔的時長；讀不出來回 null（呼叫方據此退回一個默認超時）。
	#Prm[{{nameof(AudioFile)}}][音頻檔路徑。]
	""")]
	public TimeSpan? TryGetDuration(str AudioFile);
}

[Doc($$"""
{{nameof(IAudioPlayer)}} 的實現：NAudio（`WaveOutEvent` 走 WinMM），mp3 另備 MCI 退路。

*為何 mp3 不走 `Mp3FileReader`／`AudioFileReader`*：那條路徑經 ACM／DMO 解碼，
在 NativeAOT 下會因 COM marshal 而在**建構讀取器時就炸**（本專案第一版就是這樣，
AOT 版因此完全沒有聲音）。`Ngan.Dict` 的 Windows 播放器踩過同一個坑，
它的解法是：mp3 先試 Media Foundation 解碼，失敗再交給 Windows 自帶的 MCI 播——
MCI 只是 `winmm.dll` 的 P/Invoke，不需要任何托管 COM 包裝，故天然對 AOT 友好。
本類照抄這個做法。

*為何 wav 不受影響*：`WaveFileReader` 只按 RIFF 頭部解釋字節，全程純托管，
AOT 下可用；故 wav 仍走 NAudio 這條（能拿到時長、能精確取消）。

*為何播放要獨佔*：同一張音效卡上兩個 `WaveOutEvent` 會互搶；用信號量而不是 lock，
因為等待期間不該佔住線程池的線程。
""")]
public partial class AudioPlayer : IAudioPlayer{

	[Doc("播放同一個設備需要互斥。")]
	static readonly SemaphoreSlim PlayGate = new(1, 1);

	[Doc("MCI 播放時輪詢狀態的間隔；太密浪費 CPU，太疏讓「播完」延遲可感。")]
	static readonly TimeSpan MciPollInterval = TimeSpan.FromMilliseconds(100);

	[Doc("日誌；mp3 走 MF 失敗要能看出來走了退路。")]
	readonly ILogger<AudioPlayer> Log;

	[Doc("接上日誌。")]
	public AudioPlayer(ILogger<AudioPlayer> Log){
		this.Log = Log;
	}

	[Doc($$"""
	播放到結束或取消。
	#Prm[{{nameof(AudioFile)}}][音頻檔路徑。]
	#Prm[{{nameof(Ct)}}][取消即停止。]
	""")]
	public async Task<nil> PlayAsync(str AudioFile, CT Ct){
		if(!File.Exists(AudioFile)){
			throw KeysErr.Tts.AudioFileNotFound__.ToErr(AudioFile);
		}

		await PlayGate.WaitAsync(Ct);
		try{
			var Ext = Path.GetExtension(AudioFile).ToLowerInvariant();
			if(Ext == ".wav"){
				await PlayByWaveOutAsync(() => new WaveFileReader(AudioFile), Ct);
				Log.LogDebug("audio played. file={File}", AudioFile);
				return NIL;
			}
			// mp3：ACM 解碼（純 P/Invoke）→ Media Foundation → MCI，逐一退讓（梯子見 Mp3Decoders）。
			foreach(var Decoder in Mp3Decoders(AudioFile)){
				if(await TryPlayByReaderAsync(Decoder, Ct)){
					Log.LogDebug("audio played. file={File}; decoder={Decoder}", AudioFile, Decoder.Tag);
					return NIL;
				}
			}
			await PlayByMciAsync(AudioFile, Ct);
			Log.LogDebug("audio played. file={File}; decoder=mci", AudioFile);
		}
		finally{
			PlayGate.Release();
		}
		return NIL;
	}

	[Doc($$"""
	一個解碼候選：名字（日誌用）、讀取器工廠、失敗回呼。
	""")]
	sealed record DecoderCandidate(str Tag, Func<WaveStream> MkReader, Action<Exception>? OnFail);

	[Doc($$"""
	mp3 的解碼候選梯：ACM → Media Foundation（這個運行環境還值得試的話）。
	MCI 不是 `WaveStream`，不入梯，永遠是最後的單獨退路。

	*為何收成一份（2026-11-16 審查 MAINT-1）*：播放與時長查詢都要走同一串退讓，
	原本兩處各寫一遍「先 ACM、再 MF、最後 MCI」，一處加了新解碼器另一處就漏。

	*為何 MF 要問 {{nameof(IsMfWorthTrying)}}*：NAudio 的 Media Foundation 互操作是手寫的
	`[ComImport]` 介面，需要 `ComWrappers` 註冊；NativeAOT 下它會在
	`MFCreateSourceReaderFromURL` 直接拋 `NotSupportedException: COM Interop requires
	ComWrapper instance registered for marshalling`（本專案實測）。
	這個能力在一個行程內不會變，故失敗一次（由 {{nameof(MarkMfUnusable)}} 記下）
	就永久跳過，免得每段音頻都拋一次、日誌被灌滿。
	""")]
	IEnumerable<DecoderCandidate> Mp3Decoders(str AudioFile){
		yield return new DecoderCandidate("acm", () => new Mp3FileReader(AudioFile), null);
		if(IsMfWorthTrying){
			yield return new DecoderCandidate("mediafoundation", () => new MediaFoundationReader(AudioFile), MarkMfUnusable);
		}
	}

	[Doc($$"""
	用某個解碼候選播一次；解不了就回 false 換下一個。
	*取消為什麼不能吞*：播放途中被取消也是拋 `OperationCanceledException`，
	若當成「解不了」就會改用下一個解碼器再播一次，等於取消無效。
	#Prm[{{nameof(Decoder)}}][解碼候選（名字／工廠／失敗回呼）。]
	""")]
	async Task<bool> TryPlayByReaderAsync(DecoderCandidate Decoder, CT Ct){
		try{
			await PlayByWaveOutAsync(Decoder.MkReader, Ct);
			return true;
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception Ex){
			Log.LogDebug(Ex, "decoder {Decoder} failed. file will be retried with the next decoder", Decoder.Tag);
			Decoder.OnFail?.Invoke(Ex);
			return false;
		}
	}

	[Doc($$"""
	用 `WaveOutEvent` 播一個 `WaveStream` 到結束。
	*為何把 Reader 用工廠傳進來*：解碼器可能是 `WaveFileReader` 也可能是 `MediaFoundationReader`，
	兩者都要在同一個「播完／取消」框架裡跑。
	""")]
	async Task<nil> PlayByWaveOutAsync(Func<WaveStream> MkReader, CT Ct){
		await Task.Run(async () => {
			Ct.ThrowIfCancellationRequested();
			using var Reader = MkReader();
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
		return NIL;
	}

	[Doc($$"""
	Media Foundation 在這個運行環境是否可用；null＝還沒試過。
	*為何要有這一個旗標*：見 {{nameof(Mp3Decoders)}}；
	它同時決定時長能不能用 MF 去讀。
	""")]
	bool? MfUsable;

	[Doc("保護 {{nameof(MfUsable)}}。")]
	readonly Lock MfGate = new();

	[Doc("查 MF 是否還值得試。")]
	bool IsMfWorthTrying{
		get{
			lock(MfGate){
				return MfUsable is not false;
			}
		}
	}

	[Doc("記下「MF 用不了」，只記一次；之後同一行程不再嘗試。")]
	void MarkMfUnusable(Exception Ex){
		lock(MfGate){
			if(MfUsable is false){
				return;
			}
			MfUsable = false;
		}
		// 這是環境層面的結論，用 Warning 記一次就夠；之後每次播放只會走 MCI。
		Log.LogWarning(Ex,
			"media foundation is unavailable in this runtime; mp3 playback falls back to mci from now on");
	}

	[Doc($$"""
	用 MCI 播放（`mciSendString` 的 `open`／`play`／`status`／`stop`／`close`）。
	*為何這條路對 AOT 友好*：MCI 是 `winmm.dll` 的命令字符串接口，沒有托管 COM 包裝；
	不需要 `BuiltInComInteropSupport` 也能用。
	""")]
	async Task<nil> PlayByMciAsync(str AudioFile, CT Ct){
		var Alias = "RimeTtsAudio" + Guid.NewGuid().ToString("N");
		await Task.Run(() => {
			try{
				OpenByMci(AudioFile, Alias);
				SendMciCommand($"play {Alias}");
				while(true){
					Ct.ThrowIfCancellationRequested();
					if(GetMciMode(Alias) == "stopped"){
						return;
					}
					Thread.Sleep(MciPollInterval);
				}
			}
			catch(OperationCanceledException){
				TrySendMciCommand($"stop {Alias}");
				throw;
			}
			finally{
				TrySendMciCommand($"close {Alias}");
			}
		}, Ct);
		return NIL;
	}

	[Doc($$"""
	用 MCI 打開一個 mp3。
	*為何要試三種寫法*：不同 Windows 版本對 `open` 的參數寬容度不同，
	帶 `type mpegvideo` 與不帶的行為不一致；逐個試比「猜對一個」穩。
	""")]
	void OpenByMci(str AudioFile, str Alias){
		str[] Commands = [
			$"open \"{AudioFile}\" alias {Alias}",
			$"open \"{AudioFile}\" type mpegvideo alias {Alias}",
			$"open \"{AudioFile}\" type MPEGVideo alias {Alias}",
		];
		Exception? LastErr = null;
		foreach(var Command in Commands){
			try{
				SendMciCommand(Command);
				return;
			}
			catch(Exception Ex){
				LastErr = Ex;
			}
		}
		throw KeysErr.Tts.MciOpenFailed__.WithMsg(LastErr?.Message ?? "all open commands failed");
	}

	[Doc("讀 MCI 設備的當前狀態（`playing`／`paused`／`stopped`）。")]
	str GetMciMode(str Alias){
		var Buffer = new char[128];
		SendMciCommand($"status {Alias} mode", Buffer);
		return CharBufferToString(Buffer).Trim().ToLowerInvariant();
	}

	[Doc($$"""
	發一條 MCI 命令；非 0 返回碼換成可讀錯誤，方便日誌定位。
	*日誌走注入的 {{nameof(ILogger)}} 而不是 {{nameof(AppLog)}}（2026-11-16 審查 ARCH-3）*：
	Domains 層的紅線是「只走構造注入、不碰靜態門面」；本類同類的失敗日誌都在
	{{nameof(Log)}} 上，MCI 不該是例外。方法因此從 static 改成實例方法。
	""")]
	void SendMciCommand(str Command, char[]? Buffer = null){
		// 前後各留一條 Debug：MCI 是外部設備接口，某些機器上會在呼叫裡卡住，
		// 到時「有送出去但沒有回來」這兩條就是唯一的線索。
		// *但輪詢不記*：播放期間每 100ms 一條 `status … mode`，記下來會把日誌灌滿。
		var Trace = !Command.StartsWith("status", StringComparison.Ordinal);
		if(Trace){
			Log.LogDebug("[mci] {Command}", Command);
		}
		var Code = NativeMethods.MciSendString(Command, Buffer, Buffer?.Length ?? 0, IntPtr.Zero);
		if(Trace){
			Log.LogDebug("[mci] {Command} -> {Code}", Command, Code);
		}
		if(Code != 0){
			throw KeysErr.Tts.MciCommandFailed__.WithMsg($"{Command} -> {Code}: {GetMciErrorText(Code)}");
		}
	}

	[Doc("清理階段盡量吞錯，避免掩蓋原始異常。")]
	void TrySendMciCommand(str Command){
		try{
			SendMciCommand(Command);
		}
		catch(TypedErr){
			// 清理失敗不阻斷主流程。
		}
		catch(Exception){
			// 同上。
		}
	}

	[Doc("MCI 錯誤碼 → 人可讀文本。")]
	static str GetMciErrorText(i32 Code){
		var Buffer = new char[256];
		return NativeMethods.MciGetErrorString(Code, Buffer, Buffer.Length)
			? CharBufferToString(Buffer).Trim()
			: "unknown mci error";
	}

	[Doc("MCI 回的是 `\\0` 結尾的字符緩衝，只取有效內容。")]
	static str CharBufferToString(char[] Buffer){
		var End = Array.IndexOf(Buffer, '\0');
		return End < 0 ? new str(Buffer) : new str(Buffer, 0, End);
	}

	[Doc($$"""
	讀音頻時長；幾條路都拿不到就回 null（呼叫方退回保底超時）。

	*為何要多條路*：wav 用 `WaveFileReader`（純托管，一定行）；mp3 先試 ACM
	（`Mp3FileReader`，純 P/Invoke，AOT 下可用）；再試 Media Foundation
	（AOT 下會被 `ComWrappers` 那條限制擋住，故失敗一次就記住）；最後用 MCI 的
	`status <alias> length` 問 Windows 自己（返回毫秒）。
	*為何不自己解 mp3 幀頭*：VBR 與 ID3 會讓算式變成猜謎。
	""")]
	public TimeSpan? TryGetDuration(str AudioFile){
		var Ext = Path.GetExtension(AudioFile).ToLowerInvariant();
		if(Ext == ".wav"){
			return TryReadDuration(() => new WaveFileReader(AudioFile), null);
		}
		// 與播放同一條解碼梯（Mp3Decoders）：兩處的退讓順序必須一致，
		// 否則會出現「時長讀得出來、播放卻不會用那個解碼器」的錯位。
		foreach(var Decoder in Mp3Decoders(AudioFile)){
			var ByReader = TryReadDuration(Decoder.MkReader, Decoder.OnFail);
			if(ByReader is not null){
				return ByReader;
			}
		}
		return TryGetMciLength(AudioFile);
	}

	[Doc($$"""
	用一個 `WaveStream` 讀總時長；失敗回 null。
	#Prm[{{nameof(MkReader)}}][解碼器工廠。]
	#Prm[{{nameof(OnFail)}}][可選的失敗回呼（例如記下「MF 用不了」）。]
	""")]
	static TimeSpan? TryReadDuration(Func<WaveStream> MkReader, Action<Exception>? OnFail){
		try{
			using var Reader = MkReader();
			var Duration = Reader.TotalTime;
			return Duration > TimeSpan.Zero ? Duration : null;
		}
		catch(Exception Ex){
			// 時長只用來決定超時；讀不到不該讓它變成一次播放失敗。
			OnFail?.Invoke(Ex);
			return null;
		}
	}

	[Doc("用 MCI 問時長（毫秒）。播放本來就要用 MCI，能播就一定能報長度。")]
	TimeSpan? TryGetMciLength(str AudioFile){
		var Alias = "RimeTtsDur" + Guid.NewGuid().ToString("N");
		try{
			OpenByMci(AudioFile, Alias);
			var Buffer = new char[64];
			SendMciCommand($"status {Alias} length", Buffer);
			var Text = CharBufferToString(Buffer).Trim();
			return i64.TryParse(Text, out var Ms) && Ms > 0
				? TimeSpan.FromMilliseconds(Ms)
				: null;
		}
		catch(Exception Ex){
			Log.LogDebug(Ex, "mci could not report length. file={File}", AudioFile);
			return null;
		}
		finally{
			TrySendMciCommand($"close {Alias}");
		}
	}

	[Doc("AOT 友好的 WinMM P/Invoke（源生成式封送，不依賴運行期 IL stub）。")]
	static partial class NativeMethods{

		[LibraryImport("winmm.dll", EntryPoint = "mciSendStringW", StringMarshalling = StringMarshalling.Utf16)]
		internal static partial i32 MciSendString(str Command, [Out] char[]? ReturnString, i32 ReturnLength, IntPtr Callback);

		[LibraryImport("winmm.dll", EntryPoint = "mciGetErrorStringW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static partial bool MciGetErrorString(i32 ErrorCode, [Out] char[] ErrorText, i32 ErrorTextSize);
	}
}
