namespace RimeTts.Infra;

[Doc($$"""
桌面宿主的應用儲存區根目錄：可執行檔所在目錄。

*為何不是當前工作目錄*：從 cmd 或捷徑啟動時當前目錄可能是任何地方，
配置、日誌與音頻輸出會跟著跑；可執行檔目錄才是這份應用真正「屬於自己」的位置。

*為何與 Rime 的 User_Data 無關*：本工具只是被 Rime 的 Lua 插件用文件通知，
它自己的配置與產物留在自己的安裝位置，避免污染 Rime 的用戶目錄。
""")]
public class AppDirBaseDirMgr : I_GetBaseDir {
	[Doc("全局單例；入口註冊進容器時用的就是它。")]
	public static AppDirBaseDirMgr Inst => field ??= new AppDirBaseDirMgr();

	[Doc("根目錄本體。默認是可執行檔所在目錄；測試或特殊宿主可以改寫。")]
	public str _BaseDir { get; set; } = AppContext.BaseDirectory;

	[Doc("取根目錄。要拼子路徑時用 I_GetBaseDirExtn.Combine，不要自己接字串。")]
	public str GetBaseDir() {
		return _BaseDir;
	}
}
