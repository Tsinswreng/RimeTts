namespace RimeTts.Infra.Json;

using Tsinswreng.CsCfg.Json;
using Tsinswreng.CsJsonr;

[Doc($$"""
以 jsonr 檔案為來源的**只讀**配置源。

*它是甚麼*：{{nameof(JsonCfgReader)}} 換掉「開檔」那一步——讀進來的是 jsonr，
先轉成 JSONC 再交給庫解析。其餘（鍵序、默認值、Reload 事件）全由庫負責。

*為何是檔案源而不是流式源*：配置是每次啟動讀一次的東西，不需要長期持有句柄；
每次重讀都重新開檔，別的編輯器隨時能改。
#See[{{nameof(JsonrCfg)}}][為何要有這一層。]
""")]
public class JsonrFileCfgReader : JsonCfgReader{

	[Doc("配置檔的絕對路徑。建構時正規化，故 `sub/a.jsonr` 與 `sub\a.jsonr` 是同一個字符串。")]
	public str Path{get;init;} = "";

	[Doc($$"""
	接上一份 jsonr 配置檔並立刻讀一次。
	#Prm[{{nameof(Path)}}][配置檔路徑；可以相對，會正規化成絕對路徑。]
	#Prm[{{nameof(Name)}}][源的技術標識，例如 `file`；多源疊加時它就是「由誰接管」的依據。]
	#Throw[{{nameof(ArgumentException)}}][路徑是空串時。]
	#Throw[{{nameof(IOException)}}][檔案存在但讀不了時。]
	#Throw[{{nameof(JsonrException)}}][內容不是合法 jsonr 時。]
	""")]
	public JsonrFileCfgReader(str Path, str Name) : base(null, Name){
		if(str.IsNullOrWhiteSpace(Path)){
			throw new ArgumentException("Config file path is empty.", nameof(Path));
		}
		this.Path = System.IO.Path.GetFullPath(Path);
		// 基底在建構子裡只會「有流才讀」；我們的流是延遲開的，故在這裡主動讀一次，
		// 讓「建構即已載入」這個契約對呼叫方成立（模板的檔案源也是這樣）。
		Load();
	}

	[Doc("每次讀取都重新開檔，並轉成 JSONC 流。")]
	protected override Stream? OpenInputStream(){
		return JsonrCfg.OpenReadStream(Path);
	}

	[Doc("本類自己開的流由自己關。")]
	protected override nil CloseInputStream(Stream Utf8){
		Utf8.Dispose();
		return NIL;
	}
}
