namespace RimeTts.Infra.Log;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[Doc($$"""
日誌相關的依賴注入裝配。

*為何集中成一個方法*：宿主原本要自己「設 {{nameof(AppLog)}}.Inst 的內層實現 ＋ AddSingleton ＋ AddLogging」，
三段漏一段就會出現「注入的日誌是空的」或「兩條出口各寫一半」。收成 {{nameof(AddAppLog)}}
之後，入口只留一行。
""")]
public static class DiLog{

	[Doc($$"""
	把 {{nameof(AppLog)}} 接到容器，並指定本進程的日誌去向。

	做完之後兩種寫法落到同一個內層實現：注入 `ILogger<T>`（經分類包裝）與直接調
	{{nameof(AppLog)}}.Inst。

	#Prm[{{nameof(InnerLogger)}}][本進程的日誌實現，由宿主提供（本專案只有
	{{nameof(MkrLogger.TieredEtFile)}} 一種）。]
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	#Eg[
		```cs
		Svc.AddAppLog(MkrLogger.TieredEtFile(AppInfo.Name));
		```
	]
	""")]
	public static IServiceCollection AddAppLog(this IServiceCollection z, ILogger InnerLogger){
		// 1: 全局門面先指向本進程的實現——這一步必須在建任何服務之前完成，
		//    否則啟動期間的日誌會落到空的內層實現上而丟失。
		AppLog.Inst.InnerLogger = InnerLogger;

		// 2: 讓注入取得的 `ILogger` 就是同一個門面，而不是另一份實現。
		z.AddSingleton(InnerLogger);
		z.AddSingleton<ILogger>(_ => AppLog.Inst);

		// 3: 泛型日誌介面由 Logging 框架的分類機制產生，故換掉它的 provider 才能接到門面上；
		//    先清空默認 provider，避免多出第二條出口。
		z.AddLogging(B => {
			B.ClearProviders();
			B.AddProvider(AppLogLoggerProvider.Inst);
		});
		return z;
	}
}
