namespace RimeTts.Domains.Typing;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using RimeTts.Infra.Json;

[Doc($$"""
以文件為通道的打字監聽器：監看信號檔，一有變動就讀內容檔。

*為何要「信號檔 ＋ 內容檔」兩個檔*：內容檔會被 Lua 整份覆寫，寫到一半的 JSON 是常態。
信號檔只負責「現在去看」，故本端收到通知時可以重試幾次讀內容，直到讀到完整 JSON 或放棄。
若只有一個檔，本端無法分辨「還沒寫完」與「內容真的壞了」。

*為何用 {{nameof(FileSystemWatcher)}} 而不是輪詢*：輪詢為了靈敏度要把間隔壓到幾十毫秒，
代價是常駐 CPU；而檔案變動事件由系統提供，幾乎沒有成本。代價是事件可能重複或漏掉，
故本端做了去重（同樣的內容 ＋ 同樣的寫入時間只處理一次）。

*為何去重要連寫入時間一起比*：使用者可能連續上屏同一個詞（打錯重打），
內容一樣但時間不同——那只按內容比就會把第二次吃掉。
""")]
public class FileTypingListener(
	OptFileInteractor Opt,
	ILogger<FileTypingListener> Log
):ITypingListener, IDisposable{

	[Doc("每收到一次上屏就觸發一次。")]
	public event Action<DtoCommit>? CommitReceived;

	[Doc("每收到一次按鍵事件就觸發一次。")]
	public event Action<DtoKeyEvent>? KeyEventReceived;

	[Doc("序列化「讀內容檔」這件事：多個檔案事件可能同時湧入，並發讀只會互相打斷。")]
	readonly SemaphoreSlim ReadGate = new(1, 1);

	[Doc("去重狀態的鎖。")]
	readonly Lock DedupeLock = new();

	[Doc("檔案監看器；Stop 之後為 null。")]
	FileSystemWatcher? Watcher;

	[Doc("是否已經在監聽。")]
	volatile bool Started;

	[Doc("上一次處理過的內容（原文），用於去重。")]
	str LastPayload = "";

	[Doc("上一次真正派發事件的時刻，用於去重。")]
	DateTimeOffset LastDispatchAt = DateTimeOffset.MinValue;

	[Doc($$"""
	同內容重複事件的最小間隔。

	*為何不是「比信號檔的寫入時間」*：一次寫檔會觸發兩三個監看事件（Created ＋ Changed），
	而每個事件各自去讀 `LastWriteTimeUtc`，讀到的值可能差一格（毫秒或檔案系統時間戳精度）——
	於是同一份 payload 被派發兩次，使用者會聽到同一句唸兩遍（實測確實如此）。
	故改用「同內容 ＋ 短時間窗」判定：同一次寫檔的事件都在幾毫秒內到齊，
	而人類不可能在 300ms 內把同一句上屏兩次。
	""")]
	static readonly TimeSpan DuplicateWindow = TimeSpan.FromMilliseconds(300);

	[Doc($$"""
	接上監看器。
	#Prm[{{nameof(Ct)}}][目前不參與：監看的啟動是同步的。保留參數是為了介面一致。]
	#Throw[{{nameof(TypedErr)}}][兩個路徑有一個是空的（{{nameof(KeysErr.Common.ArgErr__)}}）時。]
	""")]
	public Task StartAsync(CT Ct){
		if(Started){
			return Task.CompletedTask;
		}
		if(str.IsNullOrWhiteSpace(Opt.SignalFile) || str.IsNullOrWhiteSpace(Opt.ContentFile)){
			throw KeysErr.Common.ArgErr__.ToErr($"{nameof(Opt.SignalFile)} / {nameof(Opt.ContentFile)}");
		}

		var SignalDir = Path.GetDirectoryName(Opt.SignalFile);
		var SignalName = Path.GetFileName(Opt.SignalFile);
		if(str.IsNullOrWhiteSpace(SignalDir) || str.IsNullOrWhiteSpace(SignalName)){
			throw KeysErr.Common.ArgErr__.ToErr(nameof(Opt.SignalFile));
		}

		// 目錄可能還不存在（Rime 還沒部署過）：先建出來，否則監看器建構就拋。
		Directory.CreateDirectory(SignalDir);
		var ContentDir = Path.GetDirectoryName(Opt.ContentFile);
		if(!str.IsNullOrWhiteSpace(ContentDir)){
			Directory.CreateDirectory(ContentDir);
		}

		Watcher = new FileSystemWatcher(SignalDir, SignalName){
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
			EnableRaisingEvents = true,
		};
		Watcher.Changed += OnSignal;
		Watcher.Created += OnSignal;
		Watcher.Renamed += OnSignal;

		Started = true;
		Log.LogInformation(
			"typing listener started. signal={SignalFile}; content={ContentFile}",
			Opt.SignalFile, Opt.ContentFile);
		return Task.CompletedTask;
	}

	[Doc("停監看並釋放。")]
	public Task StopAsync(CT Ct){
		if(!Started){
			return Task.CompletedTask;
		}
		Started = false;
		if(Watcher is not null){
			Watcher.EnableRaisingEvents = false;
			Watcher.Changed -= OnSignal;
			Watcher.Created -= OnSignal;
			Watcher.Renamed -= OnSignal;
			Watcher.Dispose();
			Watcher = null;
		}
		Log.LogInformation("typing listener stopped");
		return Task.CompletedTask;
	}

	[Doc($$"""
	檔案變動回呼：記下信號檔的寫入時間，然後到背景去讀內容。
	*為何不在這裡直接讀*：回呼在監看器的線程上跑，讀檔（含重試）會拖住它，
	而拖住期間的新事件會被合併或丟掉。
	""")]
	void OnSignal(obj? Sender, FileSystemEventArgs E){
		if(!Started){
			return;
		}

		DateTime SignalWriteUtc;
		try{
			SignalWriteUtc = File.Exists(Opt.SignalFile)
				? File.GetLastWriteTimeUtc(Opt.SignalFile)
				: DateTime.UtcNow;
		}
		catch(IOException){
			SignalWriteUtc = DateTime.UtcNow;
		}
		_ = Task.Run(() => ReadAndDispatchAsync(SignalWriteUtc));
	}

	[Doc($$"""
	讀內容檔並分派事件。
	#Prm[{{nameof(SignalWriteUtc)}}][這次通知對應的信號檔寫入時間；只寫進日誌，不參與去重（理由見 {{nameof(DuplicateWindow)}}）。]
	""")]
	async Task ReadAndDispatchAsync(DateTime SignalWriteUtc){
		await ReadGate.WaitAsync();
		try{
			var Json = await ReadStableContentJsonAsync();
			if(str.IsNullOrWhiteSpace(Json)){
				Log.LogWarning(
					"content json empty after retries. content={ContentFile}; signal={SignalFile}",
					Opt.ContentFile, Opt.SignalFile);
				return;
			}
			if(IsDuplicate(Json)){
				Log.LogDebug("duplicate payload ignored");
				return;
			}
			// 信號時間只留作診斷：同一份內容被兩個事件讀到時，這裡會看到兩個不同的時間戳，
			// 而去重已經由「內容 ＋ 時間窗」處理掉了，不靠它。
			Log.LogDebug("dispatching payload. signalWriteUtc={SignalWriteUtc:O}", SignalWriteUtc);
			Dispatch(Json);
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "failed to read/dispatch signal file event");
		}
		finally{
			ReadGate.Release();
		}
	}

	[Doc($$"""
	把 JSON 轉成 DTO 並觸發對應事件。
	*為何用強型別而不是手動查屬性*：欄位就兩個，`[JsonSerializable]` 已登記，
	型別錯誤會由源生成在編譯期擋下；手動查 Key 只會多出一堆 `TryGetProperty`。
	""")]
	void Dispatch(str Json){
		var Evt = AppJson.Read(Json, AppJsonCtx.Default.DtoLuaEvent);
		if(Evt is null){
			return;
		}
		if(!Enum.TryParse<ELuaDtoType>(Evt.Type, ignoreCase: true, out var Kind)){
			Log.LogWarning("unknown lua dto type ignored: {Type}", Evt.Type ?? "");
			return;
		}

		switch(Kind){
			case ELuaDtoType.Commit:
				if(str.IsNullOrWhiteSpace(Evt.Text)){
					return;
				}
				CommitReceived?.Invoke(new DtoCommit{ Text = Evt.Text });
				return;
			case ELuaDtoType.KeyEvent:
				KeyEventReceived?.Invoke(new DtoKeyEvent());
				return;
			default:
				return;
		}
	}

	[Doc($$"""
	讀到一份「能解析」的內容 JSON 為止，最多試幾次。
	*為何要重試*：Lua 覆寫檔案不是原子的，本端可能正好讀到半截。
	*為何用 `FileShare.ReadWrite`*：寫端還握著句柄時本端也要能開，否則永遠讀不到。
	#Rtn[合法 JSON 文本；重試用盡回空串。]
	""")]
	async Task<str> ReadStableContentJsonAsync(){
		const i32 MaxTry = 6;
		for(var I = 0; I < MaxTry; I++){
			if(!File.Exists(Opt.ContentFile)){
				await Task.Delay(5);
				continue;
			}
			try{
				str Json;
				using(var Fs = new FileStream(Opt.ContentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using(var Sr = new StreamReader(Fs, System.Text.Encoding.UTF8)){
					Json = await Sr.ReadToEndAsync();
				}
				if(str.IsNullOrWhiteSpace(Json)){
					await Task.Delay(5);
					continue;
				}
				// 只驗語法：內容的形狀交給 Dispatch 的強型別處理。
				using var _ = JsonDocument.Parse(Json);
				return Json;
			}
			catch(JsonException){
				await Task.Delay(5);
			}
			catch(IOException){
				await Task.Delay(5);
			}
		}

		Log.LogWarning("content json still unavailable or invalid after retries. content={ContentFile}", Opt.ContentFile);
		return "";
	}

	[Doc($$"""
	同內容且在上次派發後的短時間內，才是重複。
	*為何要時間窗*：連續上屏同一個詞是正常操作（打錯重打），只看內容會把第二次吃掉；
	而一次寫檔的多個監看事件會落在同一瞬間，時間窗正好把它們收掉。
	見 {{nameof(DuplicateWindow)}}。
	""")]
	bool IsDuplicate(str Json){
		lock(DedupeLock){
			var Now = DateTimeOffset.UtcNow;
			if(str.Equals(LastPayload, Json, StringComparison.Ordinal) && Now - LastDispatchAt < DuplicateWindow){
				return true;
			}
			LastPayload = Json;
			LastDispatchAt = Now;
			return false;
		}
	}

	[Doc("停止監聽並釋放同步物件。")]
	public void Dispose(){
		_ = StopAsync(CancellationToken.None);
		ReadGate.Dispose();
	}
}
