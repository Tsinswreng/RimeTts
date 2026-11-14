namespace RimeTts.Infra.Json;

using System.Text;
using Tsinswreng.CsCfg.Json;
using Tsinswreng.CsJsonr;
using RimeTts.Infra.Cfg;

[Doc($$"""
以 jsonr 檔案為來源的**可讀可寫**配置源。

*兩個方向的格式轉換*：
讀——檔案(jsonr) → JSONC → 庫解析；
寫——庫序列化成 JSON → 改寫成 jsonr（raw string） → 原子寫入檔案。
故人手改的那份與程式寫的那份是同一種格式，都讀得下去。

*為何寫出要走「先寫內存再搬移」*：庫的寫出端是流，而我們要在寫完之後才轉格式、
才知道最終字節數；用臨時檔案會多一次 IO 與一次清理路徑，內存流更直接。
大小不成問題——配置檔是 KB 級。

*目前沒有寫入者*：本工具沒有設定界面，故這一支存在的理由是「留下唯一可寫源」的裝配，
而不是現在真的會存檔（見 {{nameof(AppCfg.Setup)}} 的說明）。
""")]
public class JsonrFileCfgAccessor : JsonCfgAccessor{

	[Doc("配置檔的絕對路徑。建構時正規化。")]
	public str Path{get;init;} = "";

	[Doc($$"""
	接上一份 jsonr 配置檔並立刻讀一次。
	#Prm[{{nameof(Path)}}][配置檔路徑；可以相對，會正規化成絕對路徑。]
	#Prm[{{nameof(Name)}}][源的技術標識，例如 `app`。]
	#Throw[{{nameof(ArgumentException)}}][路徑是空串時。]
	#Throw[{{nameof(JsonrException)}}][內容不是合法 jsonr 時。]
	""")]
	public JsonrFileCfgAccessor(str Path, str Name) : base(null, null, Name){
		if(str.IsNullOrWhiteSpace(Path)){
			throw new ArgumentException("Config file path is empty.", nameof(Path));
		}
		this.Path = System.IO.Path.GetFullPath(Path);
		Load();
	}

	[Doc("讀取：重新開檔並轉成 JSONC 流。")]
	protected override Stream? OpenInputStream(){
		return JsonrCfg.OpenReadStream(Path);
	}

	[Doc("本類自己開的讀流由自己關。")]
	protected override nil CloseInputStream(Stream Utf8){
		Utf8.Dispose();
		return NIL;
	}

	[Doc("寫出：先給庫一條內存流，等它寫完再轉格式落地。")]
	protected override Stream? OpenOutputStream(){
		return new MemoryStream();
	}

	[Doc($$"""
	寫出收尾：把內存流裡的 JSON 轉成 jsonr 並原子寫檔。
	#Prm[{{nameof(Utf8)}}][庫剛寫完的那條內存流。]
	#Prm[{{nameof(Ok)}}][是否寫成功；失敗時不落地（正式檔仍是舊的完整版）。]
	""")]
	protected override nil CloseOutputStream(Stream Utf8, bool Ok){
		try{
			if(!Ok){
				return NIL;
			}
			var JsonText = Encoding.UTF8.GetString(((MemoryStream)Utf8).ToArray());
			JsonrCfg.WriteAtomic(Path, JsonText);
			return NIL;
		}
		finally{
			Utf8.Dispose();
		}
	}
}
