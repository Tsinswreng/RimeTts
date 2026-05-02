using Microsoft.Extensions.Logging;
using RimeTts;

namespace RimeTts.Cli;

public sealed class RimeTtsConsoleLoggerProvider : ILoggerProvider{
	public ILogger CreateLogger(string categoryName){
		return new RimeTtsConsoleLogger(categoryName);
	}

	public void Dispose(){}

	private sealed class RimeTtsConsoleLogger(string categoryName) : ILogger{
		private static readonly Lock Gate = new();
		private readonly string _categoryName = categoryName;

		public IDisposable BeginScope<TState>(TState state) where TState : notnull{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel){
			return logLevel != LogLevel.None;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter){
			if(!IsEnabled(logLevel)){
				return;
			}

			if(state is RimeTtsConsoleState rimeState){
				ConsoleColorOut.WriteLine(rimeState.Tag, rimeState.Text, rimeState.Color ?? ConsoleColor.White);
				return;
			}

			var message = formatter(state, exception);
			if(string.IsNullOrWhiteSpace(message) && exception is null){
				return;
			}

			lock(Gate){
				var old = Console.ForegroundColor;
				try{
					Console.ForegroundColor = GetColor(logLevel);
					Console.Write($"{DateTime.Now:HH:mm:ss} {logLevel}: {_categoryName} ");
					if(!string.IsNullOrWhiteSpace(message)){
						Console.Write(message);
					}
					if(exception is not null){
						if(!string.IsNullOrWhiteSpace(message)){
							Console.WriteLine();
						}
						Console.Write(exception);
					}
					Console.WriteLine();
				}
				finally{
					Console.ForegroundColor = old;
				}
			}
		}

		private static ConsoleColor GetColor(LogLevel logLevel){
			return logLevel switch{
				LogLevel.Trace => ConsoleColor.DarkGray,
				LogLevel.Debug => ConsoleColor.Gray,
				LogLevel.Information => ConsoleColor.White,
				LogLevel.Warning => ConsoleColor.Yellow,
				LogLevel.Error => ConsoleColor.Red,
				LogLevel.Critical => ConsoleColor.Magenta,
				_ => ConsoleColor.White,
			};
		}
	}

	private sealed class NullScope : IDisposable{
		public static readonly NullScope Instance = new();
		public void Dispose(){}
	}
}
