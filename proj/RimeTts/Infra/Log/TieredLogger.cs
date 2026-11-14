namespace RimeTts.Infra.Log;

using Microsoft.Extensions.Logging;

[Doc($$"""
把同一條日誌分送到兩個出口：全部進檔案，重要的再進主控台。

*為何要這一層*：使用者的要求是兩級日誌——重要日誌「主控台 ＋ 檔案」都要有，
普通日誌只進檔案。兩個 sink 都能各自寫，但「哪一條算重要」的判斷只能有一處，
否則兩邊的理解遲早不一致（例如主控台覺得不重要、檔案覺得重要）。

*判斷依據是 {{nameof(EventId)}} 而不是 state 型別*：日誌會穿過
{{nameof(AppLogLoggerProvider)}} 的分類包裝，那個包裝會重新排版 state，
型別信息過一層就沒了；{{nameof(EventId)}} 會被原樣帶過去。
""")]
public class TieredLogger : ILogger{

	[Doc("重要日誌的出口（主控台）。")]
	readonly ILogger ImportantSink;

	[Doc("全部日誌的出口（檔案）。")]
	readonly ILogger AllSink;

	[Doc($$"""
	接兩個出口。
	#Prm[{{nameof(ImportantSink)}}][只收重要日誌的那一路，通常是 {{nameof(ConsoleLogger)}}。]
	#Prm[{{nameof(AllSink)}}][收全部日誌的那一路，通常是 {{nameof(FileLogger)}}。]
	""")]
	public TieredLogger(ILogger ImportantSink, ILogger AllSink){
		this.ImportantSink = ImportantSink;
		this.AllSink = AllSink;
	}

	[Doc("只要任一出口要這個等級就回 true；否則「全部」那一路會把檔案的 Debug 行也吃掉。")]
	public bool IsEnabled(LogLevel Level){
		return AllSink.IsEnabled(Level) || ImportantSink.IsEnabled(Level);
	}

	[Doc("scope 交給兩個出口各自處理：它們的格式不一樣，不該在這裡先攤平。")]
	public IDisposable? BeginScope<TState>(TState State) where TState : notnull{
		return AllSink.BeginScope(State);
	}

	[Doc($$"""
	分送一條日誌。
	#Prm[{{nameof(Level)}}][等級。]
	#Prm[{{nameof(EventId)}}][重要與否的判斷依據。]
	#Prm[{{nameof(State)}}][訊息狀態，原樣轉發給兩個出口。]
	#Prm[{{nameof(Exception)}}][可選例外，原樣轉發。]
	#Prm[{{nameof(Formatter)}}][格式化委託，原樣轉發。]
	""")]
	public void Log<TState>(
		LogLevel Level
		, EventId EventId
		, TState State
		, Exception? Exception
		, Func<TState, Exception?, str> Formatter
	){
		// 先寫檔案：即使主控台那段因為重導向而失敗，紀錄也已經留下了。
		AllSink.Log(Level, EventId, State, Exception, Formatter);
		if(EventId.Id >= RimeLogEvents.ImportantFrom){
			ImportantSink.Log(Level, EventId, State, Exception, Formatter);
		}
	}
}
