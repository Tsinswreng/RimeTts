namespace RimeTts.Infra.Log;

using System.Text;
using Microsoft.Extensions.Logging;
using RimeTts.Infra;

[Doc($$"""
把日誌追加寫進「應用儲存區／logs/」下某個檔案的日誌器。

*為何一定要有檔案 sink*：這個工具是常駐背景程式，多半從快捷方式或開機啟動跑，
沒有終端可以看；「昨天那句話為什麼沒聲音」只能靠檔案回答。

*路徑從哪來*：建構時注入 {{nameof(I_GetBaseDir)}}，故本類不假設自己在哪個安裝位置。
*為何不經容器取*：入口在建容器之前就要裝日誌出口（配置讀失敗時才有痕跡），
那時 `Di.SvcProvider` 還沒有值。

*失敗策略*：寫檔失敗時吞掉（記日誌不該讓應用崩潰），主控台那一路仍然正常輸出。
*不輪替*：按使用者決定「一直追加、不輪替」——排查時要的是完整歷史，不是整齊的檔案大小。
""")]
public sealed class FileLogger : ILogger{

	[Doc("應用儲存區的來源；建構時給，故本類不依賴容器是否已經就緒。")]
	readonly I_GetBaseDir BaseDir;

	[Doc("日誌檔在應用儲存區下的子目錄名。")]
	public const str DfltSubDir = "logs";

	[Doc("日誌檔名，例如 `RimeTts.log`。")]
	public str FileName{get;set;} = AppInfo.Name + ".log";

	[Doc("應用儲存區下的子目錄。")]
	public str SubDir{get;set;} = DfltSubDir;

	[Doc("序列化追加寫入：日誌可能來自 UI、背景任務、網絡回調多條線程，不加鎖會寫壞同一行。")]
	readonly Lock WriteLock = new();

	[Doc("解析後的路徑快取；null 表示還沒解析過。")]
	str? CachedPath;

	[Doc($$"""
	接上儲存區來源。
	#Prm[{{nameof(BaseDir)}}][應用儲存區；日誌寫在它底下的 {{nameof(SubDir)}}/。
	""")]
	public FileLogger(I_GetBaseDir BaseDir){
		this.BaseDir = BaseDir;
	}

	[Doc("解析日誌檔路徑並確保目錄存在。只做一次，之後用快取。")]
	str GetPath(){
		if(CachedPath is not null){
			return CachedPath;
		}
		var Dir = BaseDir.Combine(SubDir);
		Directory.CreateDirectory(Dir);
		return CachedPath = Path.Combine(Dir, FileName);
	}

	[Doc("本機診斷保留所有非 None 等級，避免排查時發現關鍵的 Debug 行根本沒寫。")]
	public bool IsEnabled(LogLevel Level){
		return Level != LogLevel.None;
	}

	[Doc("本日誌器不保存 scope；檔案格式裡沒有對應欄位。")]
	public IDisposable BeginScope<TState>(TState State) where TState : notnull{
		return NoOpScope.Inst;
	}

	[Doc($$"""
	把格式化訊息與完整例外堆棧追加為一個原子日誌項。
	#Prm[{{nameof(Level)}}][等級。]
	#Prm[{{nameof(EventId)}}][事件識別；檔案格式目前只記等級，不記編號。]
	#Prm[{{nameof(State)}}][訊息狀態，交給官方格式化委託。]
	#Prm[{{nameof(Exception)}}][可選例外，其堆棧另起一段。]
	#Prm[{{nameof(Formatter)}}][把狀態與例外組成文字的官方委託。]
	""")]
	public void Log<TState>(
		LogLevel Level
		, EventId EventId
		, TState State
		, Exception? Exception
		, Func<TState, Exception?, str> Formatter
	){
		if(!IsEnabled(Level)){
			return;
		}
		try{
			var Entry = new StringBuilder()
				.Append(DateTimeOffset.Now.ToString("O"))
				.Append(' ')
				.Append(Level)
				.Append(' ')
				.Append(Formatter(State, Exception));
			if(Exception is not null){
				Entry.AppendLine().Append(Exception);
			}
			Entry.AppendLine();
			// 整條在鎖內一次寫完，避免與其他線程的日誌交錯。
			lock(WriteLock){
				File.AppendAllText(GetPath(), Entry.ToString(), Encoding.UTF8);
			}
		}
		catch(IOException){
			// 寫檔失敗時保持應用可用；主控台那一路仍會輸出。
		}
		catch(UnauthorizedAccessException){
			// 同上：例如應用裝在只讀目錄，那是部署問題，不該讓朗讀功能整個停掉。
		}
	}

	[Doc("無狀態 scope 的單例。")]
	sealed class NoOpScope : IDisposable{
		[Doc("唯一實例。")]
		public static NoOpScope Inst{get;} = new();

		[Doc("沒有資源需要回收。")]
		public void Dispose(){ }
	}
}
