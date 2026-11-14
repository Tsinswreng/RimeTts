namespace RimeTts.Infra.Log;

using Microsoft.Extensions.Logging;

[Doc($$"""
把容器裡產生的一切日誌（含泛型分類版）都導向 {{nameof(AppLog)}} 的日誌提供者。

*沒有它會怎樣*：`AddLogging` 預設自己造一條出口（例如主控台），
於是「注入取得的日誌」與「直接調 {{nameof(AppLog)}} 的日誌」會分岔到兩處，
排錯時看到的是殘缺的兩半。

*分類前綴*：容器按型別全名建立分類，本提供者把分類名綴在訊息前，
故日誌行能看出是哪個類別寫的。直接調 {{nameof(AppLog)}}.Inst 的路徑沒有分類，這是預期差異。

*{{nameof(EventId)}} 必須原樣轉發*：{{nameof(TieredLogger)}} 靠它判斷要不要印到主控台。
故本包裝只改訊息的措辭，不動事件編號。
""")]
public sealed class AppLogLoggerProvider : ILoggerProvider{

	[Doc("容器與 AddLogging 共用的唯一實例；本類無狀態，故不必每次 new。")]
	public static AppLogLoggerProvider Inst{get;} = new();

	[Doc($$"""
	建立指定分類的日誌器。
	#Prm[{{nameof(CategoryName)}}][分類名。容器傳入的是型別全名，例如 `RimeTts.Domains.Tts.FallbackTts`。]
	#Rtn[把分類名綴在訊息前、其餘行為照舊轉發的 {{nameof(ILogger)}}。]
	""")]
	public ILogger CreateLogger(str CategoryName){
		return new CategoryAppLog(CategoryName);
	}

	[Doc("本提供者不持有任何可釋放資源，故 Dispose 為空。")]
	public void Dispose(){ }

	[Doc("單一分類的門面包裝：綴上分類前綴後交給 AppLog，其餘一律轉發。")]
	sealed class CategoryAppLog : ILogger{

		[Doc("已拼好的前綴，形如 `[RimeTts.Domains.Tts.FallbackTts] `；建構時算一次，避免每條日誌都重拼。")]
		readonly str _Prefix;

		[Doc($$"""
		建立包裝。
		#Prm[{{nameof(CategoryName)}}][分類名，直接嵌進前綴。]
		""")]
		public CategoryAppLog(str CategoryName){
			_Prefix = "[" + CategoryName + "] ";
		}

		[Doc("是否啟用某等級：直接問門面，故與直接調 AppLog 的判斷完全一致。")]
		public bool IsEnabled(LogLevel Level){
			return AppLog.Inst.IsEnabled(Level);
		}

		[Doc("scope 原樣轉發：scope 資料屬於內層實現的職責，本包裝不加工。")]
		public IDisposable? BeginScope<TState>(TState State) where TState : notnull{
			return AppLog.Inst.BeginScope(State);
		}

		[Doc($$"""
		寫一條日誌：先綴分類前綴，再把例外原樣交給門面，讓各內層實現自行決定堆棧怎麼擺。
		#Prm[{{nameof(Level)}}][等級。]
		#Prm[{{nameof(EventId)}}][事件識別，**原樣轉發**（{{nameof(TieredLogger)}} 依賴它）。]
		#Prm[{{nameof(State)}}][訊息狀態，交給官方格式化委託。]
		#Prm[{{nameof(Exception)}}][可選例外，原樣轉發。]
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
			// 前綴只加在「普通日誌」上：重要日誌自己帶著 `[上屏詞]` 這種標籤，
			// 再加一個型別全名只會把使用者真正想看的字擠到右邊。
			// 例外保持獨立欄位，內層寫檔時才能把堆棧另起一段。
			var Body = Formatter(State, Exception);
			var Message = EventId.Id >= RimeLogEvents.ImportantFrom ? Body : _Prefix + Body;
			AppLog.Inst.Log(Level, EventId, Message, Exception, static (S, _) => S);
		}
	}
}
