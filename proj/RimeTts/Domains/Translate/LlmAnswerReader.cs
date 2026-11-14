namespace RimeTts.Domains.Translate;

using System.Text;
using System.Text.Json;
using RimeTts.Infra.Json;

[Doc($$"""
讀 LLM 回應的兩個階段：先從別人的包絡裡挖出內容，再把我們自己格式的內容變成 {{nameof(ResLlm)}}。

*為何兩段分開*：包絡是外部協議（形狀多態、會隨供應商變），內容是我們自己的約定
（就三個鍵）。混在一起寫，改一邊就會動到另一邊；分開之後，
換供應商只需改 {{nameof(ReadContent)}}，換自己的格式只需改 {{nameof(ParseAnswer)}}。

*為何包絡用 {{nameof(JsonDocument)}} 而不是強型別*：`content` 可能是字串、
也可能是「文字積木」的陣列；根上還可能完全沒有 `choices`（Responses API 用 `output`）。
用強型別表示這種形狀只會得到一堆可空字段與特判，還拿不到 AOT 的好處。
""")]
public static class LlmAnswerReader{

	[Doc($$"""
	從回應體裡取出助手回覆的正文。
	支援三種常見形狀，依序嘗試：
	`choices[0].message.content`（字串或文字積木陣列）、`choices[0].text`、根上的 `output_text`。
	#Prm[{{nameof(ResponseJson)}}][HTTP 回應體的整段文本。]
	#Rtn[正文；取不到回 null（呼叫方據此報 {{nameof(KeysErr.Translate.ResponseEmpty)}}）。]
	#Throw[{{nameof(JsonException)}}][回應體不是合法 JSON 時。]
	""")]
	public static str? ReadContent(str ResponseJson){
		using var Doc = JsonDocument.Parse(ResponseJson);
		var Root = Doc.RootElement;

		if(Root.TryGetProperty("choices", out var Choices)
			&& Choices.ValueKind == JsonValueKind.Array
			&& Choices.GetArrayLength() > 0){
			var First = Choices[0];
			if(First.TryGetProperty("message", out var Message)
				&& TryReadContentValue(Message, out var FromMessage)){
				return FromMessage;
			}
			if(First.TryGetProperty("text", out var TextEl) && TextEl.ValueKind == JsonValueKind.String){
				return TextEl.GetString();
			}
		}

		if(Root.TryGetProperty("output_text", out var OutputText) && OutputText.ValueKind == JsonValueKind.String){
			return OutputText.GetString();
		}

		return null;
	}

	[Doc($$"""
	把助手正文解析成 {{nameof(ResLlm)}}。
	*容錯*：去掉模型常自己加的 markdown 圍欄；其餘交給 jsonr（它本身就是 JSON 的超集，
	故純 JSON 也照收）。解析不出來回 null，不拋——那是「模型沒按格式回」，屬正常失敗。
	#Prm[{{nameof(Content)}}][{{nameof(ReadContent)}} 取出的正文。]
	#Rtn[解析結果；失敗回 null。]
	""")]
	public static ResLlm? ParseAnswer(str Content){
		var Text = StripCodeFence(Content);
		if(Text.Length == 0){
			return null;
		}
		try{
			var Json = JsonrExtn.ToJsonText(Text);
			return AppJson.Read(Json, AppJsonCtx.Default.ResLlm);
		}
		catch(JsonException){
			return null;
		}
		catch(Tsinswreng.CsJsonr.JsonrException){
			return null;
		}
	}

	[Doc($$"""
	從 `{"content": …}` 形狀的對象裡讀正文；字串與「文字積木」陣列都接受。
	#Prm[{{nameof(Obj)}}][含 content 的對象。]
	#Prm[{{nameof(Text)}}][讀到的正文。]
	#Rtn[讀到非空文本回 true。]
	""")]
	static bool TryReadContentValue(JsonElement Obj, out str Text){
		Text = "";
		if(!Obj.TryGetProperty("content", out var Content)){
			return false;
		}
		if(Content.ValueKind == JsonValueKind.String){
			Text = Content.GetString() ?? "";
			return Text.Length > 0;
		}
		if(Content.ValueKind != JsonValueKind.Array){
			return false;
		}

		var Sb = new StringBuilder();
		foreach(var Part in Content.EnumerateArray()){
			// 積木陣列裡可能夾著非文字積木（圖片等）；沒有 text 就跳過。
			if(Part.ValueKind == JsonValueKind.Object
				&& Part.TryGetProperty("text", out var T)
				&& T.ValueKind == JsonValueKind.String){
				var S = T.GetString() ?? "";
				if(S.Length > 0){
					if(Sb.Length > 0){
						Sb.Append('\n');
					}
					Sb.Append(S);
				}
			}
		}
		Text = Sb.ToString().Trim();
		return Text.Length > 0;
	}

	[Doc($$"""
	去掉模型自己加的 ``` 圍欄（含語言標記）。
	*為何要容錯*：提示詞明說不要圍欄，但模型仍常加；為此讓整句翻不出來不值得。
	""")]
	static str StripCodeFence(str Content){
		var Text = Content.Trim();
		if(!Text.StartsWith("```", StringComparison.Ordinal)){
			return Text;
		}
		// 第一行是 ```jsonr 之類的標記，整行丟掉。
		var FirstNl = Text.IndexOf('\n');
		if(FirstNl >= 0){
			Text = Text[(FirstNl + 1)..].Trim();
		}
		if(Text.EndsWith("```", StringComparison.Ordinal)){
			Text = Text[..^3].Trim();
		}
		return Text;
	}
}
