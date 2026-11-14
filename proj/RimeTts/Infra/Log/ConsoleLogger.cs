namespace RimeTts.Infra.Log;

using System.Text;
using Microsoft.Extensions.Logging;

[Doc($$"""
只把「重要日誌」印到主控台的日誌器。

*為何只有重要那幾條*：這個工具的日誌有兩種用途——使用時要看到「我剛打了什麼、翻成什麼」，
事後要能查「那次為什麼沒有聲音」。前者要短、要彩色、要在眼前；後者要全、要進檔案。
把它們分流之後，主控台不會被 HTTP 細節與心跳淹掉，`logs/` 底下又什麼都有。

*為何自己寫而不接 `AddConsole()`*：官方主控台 provider 的分級與格式都是按「全部日誌」設計的，
要它只印特定事件編號得再包一層過濾；而我們還要逐行上色（標籤與正文不同色），
自己寫反而更短。也少一個套件依賴。
""")]
public class ConsoleLogger : ILogger{

	[Doc("行首的分類前綴（通常是應用名），讓同一臺機器上跑兩個工具時分得出來。")]
	public str Category{get;set;} = "";

	[Doc("多條工作線程會同時印（翻譯在背景跑），不加鎖會看到兩行交錯成一行。")]
	static readonly Lock WriteGate = new();

	[Doc("本日誌器只收 Info 以上的重要事件，故這裡的門檻只是形式。")]
	public bool IsEnabled(LogLevel Level){
		return Level != LogLevel.None;
	}

	[Doc("本日誌器不保存 scope；主控台格式裡沒有對應欄位。")]
	public IDisposable BeginScope<TState>(TState State) where TState : notnull{
		return NoOpScope.Inst;
	}

	[Doc($$"""
	印一行：時間、標籤（按事件上色）、正文。
	#Prm[{{nameof(Level)}}][等級；只有場合需要，顏色由事件編號決定。]
	#Prm[{{nameof(EventId)}}][本應用用它區分「上屏／成句／譯文」。]
	#Prm[{{nameof(State)}}][訊息狀態，交給官方格式化委託排版。]
	#Prm[{{nameof(Exception)}}][可選例外，其堆棧另起幾行。]
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
		var Msg = Formatter(State, Exception);
		lock(WriteGate){
			var Old = Console.ForegroundColor;
			try{
				// 時間與分類用灰色，正文用事件對應的顏色：一眼看到的是「字」，不是時間戳。
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.Write($"{DateTime.Now:HH:mm:ss} ");
				if(Category.Length > 0){
					Console.Write($"[{Category}] ");
				}
				Console.ForegroundColor = ColorOf(EventId, Level);
				Console.WriteLine(Msg);
				if(Exception is not null){
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(Exception);
				}
			}
			finally{
				Console.ForegroundColor = Old;
			}
		}
	}

	[Doc($$"""
	事件 → 顏色。上屏詞青色、成句黃色、譯文綠色；其餘按等級。
	*為何顏色寫在這裡而不是在呼叫端*：呼叫端只說「這是什麼事件」，怎麼顯示是這一層的決定。
	""")]
	static ConsoleColor ColorOf(EventId EventId, LogLevel Level){
		return EventId.Id switch{
			RimeLogEvents.Commit => ConsoleColor.Cyan,
			RimeLogEvents.Sentence => ConsoleColor.Yellow,
			RimeLogEvents.Translation => ConsoleColor.Green,
			RimeLogEvents.TranslationCache => ConsoleColor.DarkGreen,
			_ => Level switch{
				LogLevel.Warning => ConsoleColor.Yellow,
				LogLevel.Error => ConsoleColor.Red,
				LogLevel.Critical => ConsoleColor.Magenta,
				LogLevel.Debug or LogLevel.Trace => ConsoleColor.Gray,
				_ => ConsoleColor.White,
			},
		};
	}

	[Doc("無狀態 scope 的單例，滿足 ILogger 契約而不為每條日誌多做一次配置。")]
	sealed class NoOpScope : IDisposable{
		[Doc("唯一實例。")]
		public static NoOpScope Inst{get;} = new();

		[Doc("沒有資源需要回收。")]
		public void Dispose(){ }
	}
}
