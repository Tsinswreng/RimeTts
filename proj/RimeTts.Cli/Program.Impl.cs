//Impl: 聲明見 Program.cs。
namespace RimeTts.Cli;

using Microsoft.Extensions.DependencyInjection;
using RimeTts.Infra;
using RimeTts.Cfg;
using RimeTts.Infra.Log;
using RimeTts.Domains.Pipeline;

public static partial class Program{

	public static partial i32 Main(str[] Args){
		_ = Args;
		if(!OperatingSystem.IsWindows()){
			// 本工具的兩條腿（Win32 鉤子、SAPI）都是 Windows 專屬，故這裡直接拒絕，不做半殘的降級。
			Console.Error.WriteLine("RimeTts currently supports Windows only.");
			return 2;
		}
		// Avalonia／Hosting 都不在，沒有同步上下文要照顧；這裡直接等異步入口即可。
		return RunAsync().GetAwaiter().GetResult();
	}

	internal static partial IServiceProvider MkServices(){
		var Svc = new ServiceCollection();
		// 本平臺的全部註冊都集中在 DiCli（約定：一處裝配、入口只呼叫）。
		Svc.SetupCli();
		return Svc.BuildServiceProvider();
	}

	internal static partial async Task<i32> RunAsync(){
		// step 1: 沒有配置就吐一份樣例然後退出。這不是錯誤，是首次啟動的引導。
		if(CfgSample.EnsureExists(AppDirBaseDirMgr.Inst, out var CfgPath)){
			Console.WriteLine(Todo.I18n("已生成配置樣例：") + CfgPath);
			Console.WriteLine(Todo.I18n("請填入 Translator.ApiKey 之後重新啟動。"));
			return 1;
		}

		// step 2: 建容器。這一步就會映射並驗證配置，故失敗都集中在這裡。
		try{
			Di.SetSvcProvider(MkServices());
		}
		catch(Exception Ex){
			PrintErr(Ex);
			return 1;
		}

		// step 3: Ctrl+C 走取消，讓流水線有機會收工（停監聽、停播放、卸鉤子）。
		using var Cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, E) => {
			// 不讓 CLR 直接殺進程：先取消，由流水線自己收尾。
			E.Cancel = true;
			Cts.Cancel();
		};

		try{
			await Di.GetRSvc<ISvcRimeTts>().RunAsync(Cts.Token);
			return 0;
		}
		catch(OperationCanceledException) when(Cts.IsCancellationRequested){
			return 0;
		}
		catch(Exception Ex){
			PrintErr(Ex);
			return 1;
		}
		finally{
			(Di.SvcProviderOrNull as IDisposable)?.Dispose();
		}
	}

	internal static partial void PrintErr(Exception Ex){
		// 一律先記日誌：使用者可能只截圖了螢幕上的那一行，而完整的堆棧只存在檔案裡。
		AppLog.Error(Ex, Todo.I18n("啟動或執行失敗"));

		var IsPrivate = Ex is TypedErr Typed
			&& (Typed.Tags?.Contains(AppErrTags.SysErr) == true
				|| Typed.Tags?.Contains(AppErrTags.Private) == true);
		if(IsPrivate){
			var LogFile = AppDirBaseDirMgr.Inst.Combine(FileLogger.DfltSubDir, AppInfo.Name + ".log");
			Console.Error.WriteLine(Todo.I18n("啟動失敗：詳細原因見日誌 ") + LogFile);
			return;
		}
		Console.Error.WriteLine(Todo.I18n("啟動失敗：") + Ex.Message);
	}
}
