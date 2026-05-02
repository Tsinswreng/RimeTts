using Microsoft.Extensions.Logging;

namespace RimeTts;

public readonly record struct RimeTtsConsoleState(
	string Tag,
	string Text,
	ConsoleColor? Color,
	string OriginalFormat
);

public static class RimeTtsLogExt{
	public static void LogRimeText(this ILogger logger, LogLevel level, string tag, string text, ConsoleColor? color = null){
		var state = new RimeTtsConsoleState(tag, text, color, "{Tag} {Text}");
		logger.Log(level, default, state, null, static (s, _) => $"{s.Tag} {s.Text}");
	}

	public static void LogCommitText(this ILogger logger, string text){
		logger.LogRimeText(LogLevel.Information, "[上屏詞]", text, ConsoleColor.Cyan);
	}

	public static void LogSentenceText(this ILogger logger, string text){
		logger.LogRimeText(LogLevel.Information, "[成句]", text, ConsoleColor.Yellow);
	}

	public static void LogTranslationText(this ILogger logger, string text){
		logger.LogRimeText(LogLevel.Information, "[AI翻譯]", text, ConsoleColor.Green);
	}

	public static void LogTranslationCacheText(this ILogger logger, string text){
		logger.LogRimeText(LogLevel.Information, "[AI翻譯][Cache]", text, ConsoleColor.Green);
	}
}
