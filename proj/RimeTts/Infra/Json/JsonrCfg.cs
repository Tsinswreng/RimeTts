namespace RimeTts.Infra.Json;

using System.Text;
using Tsinswreng.CsCfg.Json;
using Tsinswreng.CsJsonr;

[Doc($$"""
jsonr 與 {{nameof(Tsinswreng.CsCfg)}} 之間的適配層。

*為何需要它*：`Tsinswreng.CsCfg.Json` 的檔案源只認「標準 JSON／JSONC 流」，
而本專案的配置檔是人手寫的，需要 raw string（提示詞是多行文本，不轉義才讀得下去）。
故在「開檔」與「解析」之間插一層：讀進來先經 {{nameof(JsonrExtn.ToJsonText)}} 轉成 JSONC，
寫出去則先讓庫寫成 JSON，再經 {{nameof(JsonrExtn.ToJsonrText)}} 改寫成 raw string 形態。

*為何不改庫*：那是跨專案共用的庫，格式知識留在應用這一側才對；
而庫的檔案源剛好留了 `OpenInputStream`／`OpenOutputStream` 兩個鉤子，
本層只是把它們換掉，沒有動庫一行。

*為何寫入是「先寫暫存檔再搬移」*：配置檔是唯一的一份，不能出現寫到一半的狀態；
`*.tmp` ＋ rename 在同一卷內是一次原子操作。
""")]
public static class JsonrCfg{

	[Doc($$"""
	打開一份 jsonr 配置檔並轉成可解析的 JSONC 流。
	*檔案不存在時回空流*：首次啟動的正常情形，上層得到空配置而不是例外。
	#Prm[{{nameof(Path)}}][配置檔路徑。]
	#Rtn[UTF-8 的 JSONC 流，Position 為 0。]
	#Throw[{{nameof(JsonrException)}}][jsonr 語法錯誤時。]
	""")]
	public static Stream OpenReadStream(str Path){
		if(!File.Exists(Path)){
			return new MemoryStream();
		}
		var JsonrText = File.ReadAllText(Path, Encoding.UTF8);
		var JsonText = JsonrExtn.ToJsonText(JsonrText);
		return new MemoryStream(Encoding.UTF8.GetBytes(JsonText));
	}

	[Doc($$"""
	把一段 JSON 文本以 jsonr 形態原子寫入檔案。
	#Prm[{{nameof(Path)}}][目標路徑；所在目錄不存在會建。]
	#Prm[{{nameof(JsonText)}}][合法 JSON 文本（通常是庫剛寫出來的那份）。]
	""")]
	public static void WriteAtomic(str Path, str JsonText){
		WriteTextAtomic(Path, JsonrExtn.ToJsonrText(JsonText));
	}

	[Doc($$"""
	把一段現成的文本原子寫入檔案（不再做格式轉換）。
	*用途*：寫配置樣例——樣例本身已經是 jsonr，且刻意只讓長文本用 raw string，
	不能再過一次 {{nameof(JsonrExtn.ToJsonrText)}}（那會把 `"gTTS"` 這種短值也包成 raw string）。
	#Prm[{{nameof(Path)}}][目標路徑；所在目錄不存在會建。]
	#Prm[{{nameof(Text)}}][要寫的完整文本。]
	""")]
	public static void WriteTextAtomic(str Path, str Text){
		var Dir = System.IO.Path.GetDirectoryName(Path);
		if(!str.IsNullOrWhiteSpace(Dir)){
			Directory.CreateDirectory(Dir);
		}
		var Tmp = Path + ".tmp";
		try{
			File.WriteAllText(Tmp, Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.Move(Tmp, Path, overwrite: true);
		}
		catch{
			// 失敗時清掉半份暫存檔；正式檔仍是舊的完整版。
			TryDel(Tmp);
			throw;
		}
	}

	[Doc($$"""
	從一段 jsonr 文本造一個只讀配置源。
	*用途*：測試與「配置來自別處」的場合；不必先落地成檔案。
	#Prm[{{nameof(JsonrText)}}][jsonr 文本。]
	#Prm[{{nameof(Name)}}][源的技術標識，例如 `text`。]
	#Rtn[只讀配置源。]
	""")]
	public static JsonCfgReader FromText(str JsonrText, str Name = "text"){
		var Json = JsonrExtn.ToJsonText(JsonrText);
		var Stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
		return new JsonCfgReader(Stream, Name);
	}

	[Doc("盡力刪掉殘檔；清理失敗不影響主流程。")]
	static void TryDel(str Path){
		try{
			if(File.Exists(Path)){
				File.Delete(Path);
			}
		}
		catch(IOException){
			// 殘檔留著只是礙眼。
		}
		catch(UnauthorizedAccessException){
			// 同上。
		}
	}
}
