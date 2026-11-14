//Impl: 聲明見 AppCfg.cs。
namespace RimeTts.Infra.Cfg;

using System.Text.Json;
using Tsinswreng.CsCfg;
using Tsinswreng.CsCfg.Json;
using Tsinswreng.CsJsonr;
using RimeTts.Infra.Json;

public partial class AppCfg{

	public partial void Setup(I_GetBaseDir BaseDir){
		// 門面的名字：事件負載與日誌裡用它標識「這一層」。
		Name = "app";
		var Dir = BaseDir.GetBaseDir();

		// 1: 出廠那份。檔案不存在就當空源；開關檔與初始讀取都在源的建構子裡。
		var RoPath = Path.Combine(Dir, RoCfgFileName);
		var Ro = OpenReader(RoPath);
		RoCfg = Ro;
		RoCfgPath = Ro.Path;

		// 2: 日常那份。位置由出廠配置的引導鍵決定，所以必須先讀過出廠那份。
		//    這條耦合是本應用的策略，庫不知道「配置裡有一個鍵指向配置檔」這種事。
		var RwRelPath = Ro.Get(KeysAppCfg.RwCfgPath);
		if(str.IsNullOrWhiteSpace(RwRelPath)){
			// 走到這裡不是「沒有配置檔」，而是「配置檔把引導鍵寫成空串」，屬於手改錯誤。
			throw KeysErr.Cfg.RwCfgPathEmpty.ToErr(nameof(KeysAppCfg.RwCfgPath));
		}
		var Rw = OpenAccessor(Path.Combine(Dir, RwRelPath));
		RwCfg = Rw;
		RwCfgPath = Rw.Path;

		// 3: 疊起來：只讀在前、日常在後，寫入只落日常那份。
		this.SetRoRw([Ro], Rw);
	}

	private static partial JsonrFileCfgReader OpenReader(str Path){
		try{
			return new JsonrFileCfgReader(Path, RoSrcName);
		}
		catch(JsonrException Ex){
			throw KeysErr.Cfg.ParseFailed__.WithMsg($"{Path} 第 {Ex.Line} 行第 {Ex.Col} 列：{Ex.Message}");
		}
		catch(JsonException Ex){
			throw KeysErr.Cfg.ParseFailed__.WithMsg($"{Path}：{Ex.Message}");
		}
	}

	private static partial JsonrFileCfgAccessor OpenAccessor(str Path){
		try{
			return new JsonrFileCfgAccessor(Path, RwSrcName);
		}
		catch(JsonrException Ex){
			throw KeysErr.Cfg.ParseFailed__.WithMsg($"{Path} 第 {Ex.Line} 行第 {Ex.Col} 列：{Ex.Message}");
		}
		catch(JsonException Ex){
			throw KeysErr.Cfg.ParseFailed__.WithMsg($"{Path}：{Ex.Message}");
		}
	}
}
