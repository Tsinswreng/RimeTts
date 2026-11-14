namespace RimeTts.Infra.Json;

using System.Text.Json;
using Tsinswreng.CsJsonr;

[Doc($$"""
{{nameof(Jsonr)}} 的字符串便捷層。

*為何要有這一層*：{{nameof(IJsonr)}} 的門面只認 `Stream`——那是為了大檔與真正流式。
本專案的兩處用法（配置檔、LLM 回應）都是「手上一段小文本」，每處都自己開兩條
MemoryStream 再包 StreamReader 會把呼叫點淹掉。這裡把它收成三個方法。

*jsonr 是甚麼*：在 JSON 基底上加三類擴展——`//` 與 `/* */` 註釋、
bash 風格 heredoc、C++ 風格行內 raw string（`R"d(…)d"`）。
轉成標準 JSONC 之後才交給 `JsonDocument`／`System.Text.Json`。
故本專案**寫出去的配置與提示詞都不需要轉義**，讀進來也容忍尾逗號。
""")]
public static class JsonrExtn{
	[Doc($$"""
	jsonr 文本 → 標準 JSONC 文本（註釋原樣保留，文本塊轉成普通 JSON 字符串）。
	#Prm[{{nameof(JsonrText)}}][jsonr 源文本。]
	#Rtn[JSONC 文本。]
	#Throw[{{nameof(JsonrException)}}][詞法錯誤（字符串／文本塊未閉合等）時；異常裡帶行列號。]
	""")]
	public static str ToJsonText(str JsonrText){
		using var In = Jsonr.StrToUtf8Stream(JsonrText);
		using var Out = new MemoryStream();
		Jsonr.Inst.ToJson(In, Out);
		Out.Position = 0;
		return Jsonr.Utf8StreamToStr(Out);
	}

	[Doc($$"""
	jsonr 文本 → 可直接查詢的 {{nameof(JsonDocument)}}。
	#Prm[{{nameof(JsonrText)}}][jsonr 源文本。]
	#Rtn[解析結果；呼叫方負責 Dispose。]
	#Throw[{{nameof(JsonrException)}}][詞法錯誤時。]
	#Throw[{{nameof(JsonException)}}][語法錯誤時（jsonr 轉完仍是壞 JSON）。]
	""")]
	public static JsonDocument Parse(str JsonrText){
		return JsonDocument.Parse(ToJsonText(JsonrText));
	}

	[Doc($$"""
	標準 JSON 文本 → jsonr 文本：把每個字符串值改寫成 C++ 風格 raw string。
	只改值、不改鍵；空串保持原樣（`R"d()d"` 比 `""` 更長更難讀）。

	*用途*：寫出配置檔時，提示詞那種多行文本就不會變成一整行帶 `\n` 的轉義串。
	#Prm[{{nameof(JsonText)}}][合法 JSON／JSONC 文本。]
	#Rtn[jsonr 文本；改寫失敗（指定定界詞衝突，本處不可能發生）時原樣返回輸入。]
	#Throw[{{nameof(JsonrException)}}][詞法錯誤時。]
	""")]
	public static str ToJsonrText(str JsonText){
		using var In = Jsonr.StrToUtf8Stream(JsonText);
		using var Out = new MemoryStream();
		// 定界詞交 null：由轉寫器逐值挑一個不衝突的詞，故永不碰撞。
		var Res = Jsonr.Inst.ToJsonr(In, Out, new OptToJsonr(SyntaxKind: ESyntaxKind.Cxx));
		Out.Position = 0;
		return Res.IsOk ? Jsonr.Utf8StreamToStr(Out) : JsonText;
	}
}
