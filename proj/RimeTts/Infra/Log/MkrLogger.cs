namespace RimeTts.Infra.Log;

using Microsoft.Extensions.Logging;

[Doc($$"""
內層日誌器的製造器：把「這一端要寫到哪裡」集中在一處。

*為何要有這個類*：`AppLog.Inst` 的內層實現只能在入口決定一次；
把它寫在 `DiCli` 裡就會和其他註冊混在一起，而換 sink（加檔案輪替、加結構化日誌）時
要改的地方應該是這裡。

*本專案只有一種口味*：{{nameof(TieredEtFile)}}——重要日誌到主控台、全部日誌到檔案。
沒有「只有主控台」的版本，因為這是無終端的常駐程式；沒有日誌檔等於沒有日誌。
""")]
public static class MkrLogger{

	[Doc($$"""
	造「重要→主控台、全部→檔案」的分流日誌器。
	#Prm[{{nameof(Category)}}][分類名；同時決定主控台前綴與檔案名（`logs/<分類名>.log`）。]
	#Prm[{{nameof(BaseDir)}}][應用儲存區。收了它才不必依賴容器是否已就緒——
	日誌出口必須早於容器建立，否則「建容器失敗」這件事本身沒有痕跡。]
	#Rtn[可直接當 {{nameof(AppLog)}}.Inst 內層實現的日誌器。]
	""")]
	public static ILogger TieredEtFile(str Category, I_GetBaseDir BaseDir){
		var Console = new ConsoleLogger{ Category = Category };
		var File = new FileLogger(BaseDir){ FileName = Category + ".log" };
		return new TieredLogger(Console, File);
	}
}
