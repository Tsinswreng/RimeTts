namespace RimeTts.Infra;

[Doc($$"""
應用儲存區的根目錄。

*為何要抽象*：同一個業務程序集要被不同宿主用，而「根目錄」在每個宿主不同
（桌面是執行檔所在目錄、Android 是外部私有目錄）。抽象掉之後，寫檔的元件
（配置、日誌、音頻輸出）只要向容器取 {{nameof(I_GetBaseDir)}}，不必自己猜位置。

*為何是方法而不是屬性*：取根目錄可能要碰檔案系統且可能失敗；屬性的語義是「取值、無副作用」。
""")]
public interface I_GetBaseDir{

	[Doc(@$"應用儲存區的根目錄（絕對路徑，不以分隔符結尾）。
	要拼子路徑時用 {nameof(I_GetBaseDirExtn.Combine)}，不要自己接字串。")]
	public str GetBaseDir();
}

public static class I_GetBaseDirExtn{

	[Doc($$"""
	把若干段路徑接到根目錄之後。
	#Prm[{{nameof(z)}}][根目錄來源。]
	#Prm[{{nameof(Segs)}}][相對段，按順序拼接。]
	#Rtn[拼好的絕對路徑。]
	#Eg[
		```cs
		var LogPath = Di.GetRSvc<I_GetBaseDir>().Combine("logs", "RimeTts.log");
		```
	]
	""")]
	public static str Combine(
		this I_GetBaseDir z
		,params IList<str> Segs
	){
		return Path.Combine([z.GetBaseDir(), ..Segs]);
	}
}
