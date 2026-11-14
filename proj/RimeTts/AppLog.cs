namespace RimeTts;

using Microsoft.Extensions.Logging;
using RimeTts.Infra.Log;
using Tsinswreng.CsLog;

[Doc($$"""
全局日誌門面：不方便依賴注入的地方（啟動早期、靜態工具、Win32 回呼）直接調 {nameof(AppLog)}.Inst。

它自己不產生任何輸出，只把呼叫轉發給內層實現 {{nameof(IDelegatingLogger.InnerLogger)}}；
內層實現由宿主在入口處指定（見 {{nameof(MkrLogger)}}）：本專案是「重要日誌→主控台＋檔案、
普通日誌→只寫檔案」的分流實現。

*為何不用依賴注入*：{nameof(AppLog)}.Inst 必須在容器建立之前就能用——啟動失敗往往就發生在建容器途中。
兩者並不衝突：注入用的日誌介面與泛型版也被 {{nameof(Infra.Log.DiLog)}} 接到同一個門面上。

#Eg[
	```cs
	AppLog.Info("配置載入完成");
	AppLog.Error(ex, "配置載入失敗");
	```
]
#See[{{nameof(AppLogExtn)}}][Info／Warn／Error 等便捷寫法。]
""")]
public class AppLog : DelegatingLogger {
	[Doc("全局單例；第一次取用時才建立，故不依賴任何啟動順序。")]
	public static AppLog Inst => field ??= new AppLog();
}


[Doc($$"""
{nameof(AppLog)} 的便捷寫法：省去每次都寫 `Inst.Log(level, 0, msg, null, static (s, _) => s?.ToString() ?? "")`。

#Eg[
	```cs
	AppLog.Info("開始載入配置");
	AppLog.Warn("配置缺項，改用默認值");
	AppLog.Error(ex, "載入配置失敗");
	```
]
""")]
public static class AppLogExtn {
	extension(AppLog) {
		[Doc("記錄 Trace 級訊息：最細的流程細節，平時不值得留，排查時才需要。")]
		public static void Trace(str Message) {
			Write(LogLevel.Trace, Message);
		}

		[Doc("記錄 Debug 級訊息：開發期看得懂的流程節點。")]
		public static void Debug(str Message) {
			Write(LogLevel.Debug, Message);
		}

		[Doc("記錄 Info 級訊息：正常但值得留痕的事件，例如啟動、載入完成。")]
		public static void Info(str Message) {
			Write(LogLevel.Information, Message);
		}

		[Doc("記錄 Warn 級訊息：沒壞但偏離預期，例如配置缺項、退回默認值。")]
		public static void Warn(str Message) {
			Write(LogLevel.Warning, Message);
		}

		[Doc("記錄 Error 級訊息：操作失敗，但應用仍可繼續。")]
		public static void Error(str Message) {
			Write(LogLevel.Error, Message);
		}

		[Doc($$"""
		記錄錯誤並附上例外：訊息之後換行接完整堆棧，避免只剩一句「失敗了」而無從排查。
		#Prm[{{nameof(Ex)}}][例外本體，其完整堆棧會被寫出。]
		#Prm[{{nameof(Message)}}][可選的上下文說明；省略時只寫例外本身。]
		""")]
		public static void Error(Exception Ex, str? Message = null) {
			// 沒有上下文說明時只留堆棧；有說明則說明在前、堆棧在後。
			Write(
				LogLevel.Error
				, string.IsNullOrWhiteSpace(Message) ? Ex.ToString() : Message + "\n" + Ex
			);
		}

		[Doc("所有便捷寫法的最終轉發點：要改輸出形態（例如加時間前綴）只改這一處。")]
		static void Write(LogLevel Level, str Message) {
			AppLog.Inst.Log(Level, 0, Message, null, static (State, _) => State?.ToString() ?? "");
		}
	}
}
