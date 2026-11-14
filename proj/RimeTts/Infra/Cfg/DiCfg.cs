namespace RimeTts.Infra.Cfg;

using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsCfg;

[Doc($$"""
配置層的依賴注入裝配。

*寫法照文檔約定*：`public static class DiXxx` ＋ `SetupXxx(this IServiceCollection z)`。

*為何要傳入門面*：庫不知道本應用有幾份配置檔、鍵族叫什麼；
注入方只該拿到「讀」或「寫」的能力，不該拿到整個門面。
故這裡把同一個實例註冊成幾種寬度。
""")]
public static class DiCfg{

	[Doc($$"""
	把配置門面註冊成四種寬度，全部指向同一個實例。
	#Prm[{{nameof(Cfg)}}][已接好源的門面；尚未呼叫 Setup 的門面也能註冊，只是空的。]
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	""")]
	public static IServiceCollection SetupCfg(this IServiceCollection z, LayeredCfgAccessor Cfg){
		// 必須是同一個實例：事件訂閱與寫入路由都掛在那一個物件上，
		// 分成多個實例會出現「這邊寫了那邊讀不到」。
		z.AddSingleton(Cfg.GetType(), Cfg);
		z.AddSingleton<LayeredCfgAccessor>(Cfg);
		z.AddSingleton<ICfgReader>(Cfg);
		z.AddSingleton<ICfgAccessor>(Cfg);
		z.AddSingleton<ILayeredCfgAccessor>(Cfg);
		return z;
	}
}
