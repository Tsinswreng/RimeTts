namespace RimeTts.Infra.Cfg;

using Tsinswreng.CsCfg;
using RimeTts.Infra.Json;

[Doc($$"""
本應用的配置門面。

*雙源*：兩份 jsonr 配置檔疊在一起，只讀那份在前——

1. `rimetts.jsonr`（出廠）：手寫、可帶註釋、只讀。可以不存在，不存在就是空源。
2. `rimetts.Rw.jsonr`（日常）：程式寫的那份，唯一可寫源。

*為何要分兩份*：手寫那份要能放註釋、要能原樣留在版本庫裡；而程式存檔是整份重寫，
註釋與排版一定保不住。兩件事放同一份檔案裡必然互相破壞。

*目前沒有寫入者*：本工具沒有設定界面，所以「日常那份」現階段不會被建立。
保留它是因為一旦要記住運行期發現的事實（例如上次成功的模型、使用者換過的引擎順序），
寫入點就是它，而不必再改一次裝配。沒有這個位置時，那種需求最後一定變成
「往只讀那份裡塞」——那才是真的破壞。

*同一個鍵兩邊都有時*：出廠那份贏。這正是「出廠設定」該有的樣子。

*哪些事屬於本類而不屬於庫*：檔名、兩份檔誰只讀誰可寫、日常檔的位置由哪個鍵決定。
庫只知道「源」「優先級」「唯一可寫源」；這三件事是本應用的決定，所以裝配寫在這裡。
""")]
public partial class AppCfg : LayeredCfgAccessor{

	[Doc("全局唯一實例。宿主在建容器之前呼叫 Setup，之後人人取這一個。")]
	public static AppCfg Inst{get;} = new();

	[Doc("開發者手寫的出廠配置檔名，位於應用儲存區下。可以不存在。")]
	public const str RoCfgFileName = "rimetts.jsonr";

	[Doc("出廠那份源的技術標識；日誌與診斷據此顯示「這一項由誰提供」。")]
	public const str RoSrcName = "file";

	[Doc("日常那份源的技術標識。")]
	public const str RwSrcName = "app";

	[Doc(@$"出廠那份的讀者。{nameof(Setup)} 之後才有值；檔案不存在時它是一個空源（取值全走鍵上宣告的默認值）。
	對外只給 {nameof(ICfgReader)}：這一層不該有人以為它寫得進去。")]
	public ICfgReader? RoCfg{get;private set;}

	[Doc(@$"日常那份的訪問器。{nameof(Setup)} 之後才有值。
	對外只給 {nameof(ICfgAccessor)}：呼叫方要寫配置就走這裡，不要自己去開檔。")]
	public ICfgAccessor? RwCfg{get;private set;}

	[Doc(@$"出廠配置檔的實際路徑（絕對）。{nameof(Setup)} 之後才有值，供日誌與診斷顯示。")]
	public str RoCfgPath{get;private set;} = "";

	[Doc(@$"日常配置檔的實際路徑（絕對）。{nameof(Setup)} 之後才有值，供日誌與診斷顯示。")]
	public str RwCfgPath{get;private set;} = "";

	[Doc($$"""
	按應用儲存區接上兩個源並疊起來。宿主呼叫一次，必須早於任何取配置的地方。

	日常配置檔的位置由引導鍵 {{nameof(KeysAppCfg.RwCfgPath)}} 決定，
	它的默認值就是默認檔名；出廠檔裡寫了這個鍵就以出廠檔為準。

	*重跑 {{nameof(Setup)}}* 會重建兩個源，已訂閱舊源事件的訂閱者要自己重新訂閱。

	#Prm[{{nameof(BaseDir)}}][應用儲存區根目錄；本專案是執行檔所在目錄。]
	#Throw[{{nameof(TypedErr)}}][配置檔內容不是合法 jsonr（{{nameof(KeysErr.Cfg.ParseFailed__)}}）時。]
	#Throw[{{nameof(IOException)}}][檔案開不起來時；那屬於部署錯誤，不靜默降級。]
	""")]
	public partial void Setup(I_GetBaseDir BaseDir);

	[Doc($$"""
	開一份只讀源，並把 jsonr 的語法錯誤換成本專案的錯誤鍵。
	*為何要換*：{{nameof(Tsinswreng.CsJsonr.JsonrException)}} 帶行列號，但它不屬於本應用的鍵族；
	換成 {{nameof(KeysErr.Cfg.ParseFailed__)}} 之後，呼叫方可以統一按鍵處置，
	而把「哪個檔、第幾行」塞進參數。
	#Prm[{{nameof(Path)}}][配置檔絕對路徑。]
	""")]
	private static partial JsonrFileCfgReader OpenReader(str Path);

	[Doc("開一份可寫源；錯誤處置同 OpenReader。")]
	private static partial JsonrFileCfgAccessor OpenAccessor(str Path);
}
