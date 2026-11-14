namespace RimeTts.Cli;

using Microsoft.Extensions.DependencyInjection;
using RimeTts.Infra;
using RimeTts.Infra.Cfg;
using RimeTts.Infra.Log;

[Doc($$"""
命令列宿主的依賴注入裝配。

*寫法照文檔約定*：`public static class DiXxx` ＋ `SetupXxx(this IServiceCollection z)`。
一個程序集一處裝配，入口的 {{nameof(Program.MkServices)}} 只剩「呼叫它」。

*本宿主決定的三件事*（都不屬於業務程序集）：
應用儲存區是執行檔目錄、日誌出口是「重要→主控台、全部→檔案」、配置檔從儲存區讀。
""")]
public static class DiCli{

	[Doc($$"""
	註冊本宿主的實現，並接上業務程序集。
	執行順序有依賴，不可調換：
	儲存區 → 日誌（早於任何可能失敗的步驟，才留下痕跡）→ 配置 → 業務。
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	#Throw[{{nameof(TypedErr)}}][配置檔解析失敗或配置缺必要項時。]
	""")]
	public static IServiceCollection SetupCli(this IServiceCollection z){
		// 1: 應用儲存區。桌面取執行檔所在目錄。
		z.AddSingleton<I_GetBaseDir>(AppDirBaseDirMgr.Inst);

		// 2: 日誌出口。必須排在配置之前：配置讀失敗時要能看到原因。
		z.AddAppLog(MkrLogger.TieredEtFile(AppInfo.Name, AppDirBaseDirMgr.Inst));

		// 3: 配置（雙源：出廠＋日常）。必須排在業務裝配之前——選項全部從它映射。
		AppCfg.Inst.Setup(AppDirBaseDirMgr.Inst);
		z.SetupCfg(AppCfg.Inst);

		// 4: 業務程序集。
		z.SetupCore(AppDirBaseDirMgr.Inst);

		return z;
	}
}
