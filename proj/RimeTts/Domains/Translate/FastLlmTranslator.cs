namespace RimeTts.Domains.Translate;

using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RimeTts.Infra;
using RimeTts.Infra.Log;

[Doc($$"""
「快」的 LLM 翻譯器：一次請求、非流式、按提示詞規定的 jsonr 回覆。

*為何不流式*：這個工具在一次成句時要把整句翻完才拿去合成語音，
流式只能讓「文字先出現」看起來快一點，對音頻沒有幫助，反而要處理增量拼接與半截 JSON。

*為何每次請求各自計時*：{{nameof(OptTranslator.TimeoutSec)}} 是逐請求的；
共用 `HttpClient.Timeout` 會讓某一句超時變成全域故障。

*為何快取在這一層*：同一句話常被重打，快取鍵是「語言 ＋ 提示詞 ＋ 原文」。
把提示詞算進鍵是刻意的——改了提示詞就該是一次新的翻譯，而不是命中舊譯文。
""")]
public class FastLlmTranslator(
	OptTranslator Opt,
	HttpClient Http,
	TranslationCache Cache,
	ILogger<FastLlmTranslator> Log
):ITranslator{

	[Doc($$"""
	翻譯一句。
	#Prm[{{nameof(Req)}}][翻譯請求。]
	#Rtn[譯文；模型表明翻不了、或回的目標語言不對時 {{nameof(RespTranslate.TranslatedText)}} 為 null。]
	#Throw[{{nameof(TypedErr)}}][密鑰缺失、HTTP 非成功、回應無內容時。]
	""")]
	public async Task<RespTranslate> Translate(ReqTranslate Req, CT Ct){
		var Source = Req.SourceText;
		var Lang = LangTag.Normalize(Req.TargetLanguage) is { Length: > 0 } L ? L : "en";
		var Prompt = str.IsNullOrWhiteSpace(Req.SystemPrompt)
			? Opt.DefaultSystemPrompt
			: Req.SystemPrompt.Trim();

		if(Source.Length == 0){
			return new RespTranslate{ SourceText = "", TargetLanguage = Lang, TranslatedText = "" };
		}
		if(str.IsNullOrWhiteSpace(Opt.ApiKey)){
			throw KeysErr.Cfg.ApiKeyEmpty.ToErr();
		}

		var CacheKey = $"{Lang}\n{Prompt}\n{Source}";
		if(Cache.TryGet(CacheKey, out var Cached)){
			if(!str.IsNullOrWhiteSpace(Cached)){
				Log.LogTranslationCacheText(Lang, Cached);
			}
			return new RespTranslate{ SourceText = Source, TargetLanguage = Lang, TranslatedText = Cached };
		}

		var Content = await RequestContentAsync(Source, Prompt, Ct);
		var Answer = LlmAnswerReader.ParseAnswer(Content);
		var Translated = InterpretAnswer(Lang, Source, Answer, Content);

		Cache.Set(CacheKey, Translated);
		return new RespTranslate{ SourceText = Source, TargetLanguage = Lang, TranslatedText = Translated };
	}

	[Doc($$"""
	把模型的回覆判成「譯文」或「翻不了」，並留下該留的日誌。
	#Prm[{{nameof(Lang)}}][要求的目標語言。]
	#Prm[{{nameof(Source)}}][原文，僅用於日誌。]
	#Prm[{{nameof(Answer)}}][解析結果；null 表示格式沒對上。]
	#Prm[{{nameof(Content)}}][原始正文，僅在失敗時截斷記日誌。]
	#Rtn[譯文或 null。]
	""")]
	str? InterpretAnswer(str Lang, str Source, ResLlm? Answer, str Content){
		if(Answer is null){
			Log.LogWarning(
				"translator response is not valid jsonr. lang={Lang}; source={Source}; response={Response}",
				Lang, Source, Summarize(Content));
			return null;
		}
		if(!LangTag.Same(Lang, Answer.TargetLang)){
			Log.LogWarning(
				"translator target lang mismatch. expected={Expected}; actual={Actual}; source={Source}",
				Lang, Answer.TargetLang ?? "", Source);
			return null;
		}

		var Translated = Answer.Translation?.Trim();
		if(str.IsNullOrWhiteSpace(Translated) || IsNullLiteral(Translated)){
			Log.LogWarning("translation is null; skip speaking. source={Source}; lang={Lang}", Source, Lang);
			return null;
		}
		Log.LogTranslationText(Lang, Translated);
		return Translated;
	}

	[Doc($$"""
	發一次 chat completions 請求，取回助手正文。
	#Prm[{{nameof(Source)}}][原文。]
	#Prm[{{nameof(Prompt)}}][系統提示詞。]
	#Rtn[助手正文。]
	#Throw[{{nameof(TypedErr)}}][HTTP 非成功或回應無內容時。]
	""")]
	async Task<str> RequestContentAsync(str Source, str Prompt, CT Ct){
		using var Cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		Cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, Opt.TimeoutSec)));

		using var Msg = new HttpRequestMessage(HttpMethod.Post, Opt.BaseUrl){
			Content = new StringContent(MkRequestBody(Source, Prompt), Encoding.UTF8, "application/json"),
		};
		Msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Opt.ApiKey);

		using var Resp = await Http.SendAsync(Msg, HttpCompletionOption.ResponseHeadersRead, Cts.Token);
		var Body = await Resp.Content.ReadAsStringAsync(Cts.Token);
		if(!Resp.IsSuccessStatusCode){
			Log.LogError(
				"translator http failed. status={Status}; reason={Reason}; body={Body}",
				(int)Resp.StatusCode, Resp.ReasonPhrase, Summarize(Body));
			throw KeysErr.Translate.HttpFailed__.ToErr((i32)Resp.StatusCode);
		}

		var Content = LlmAnswerReader.ReadContent(Body);
		if(str.IsNullOrWhiteSpace(Content)){
			Log.LogError("translator response has no content. body={Body}", Summarize(Body));
			throw KeysErr.Translate.ResponseEmpty.ToErr();
		}
		return Content;
	}

	[Doc($$"""
	組請求體。
	*為何用 {{nameof(Utf8JsonWriter)}} 而不是匿名物件 ＋ 序列化*：只需要三個欄位，
	寫出來就是 AOT 安全的，不必為它登記一個 DTO 進源生成上下文。
	提示詞與原文會被正確轉義——這裡是必須轉義的地方。
	""")]
	str MkRequestBody(str Source, str Prompt){
		using var Buffer = new MemoryStream();
		using(var Writer = new Utf8JsonWriter(Buffer, new JsonWriterOptions{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		})){
			Writer.WriteStartObject();
			Writer.WriteString("model", Opt.Model);
			Writer.WriteStartArray("messages");
			WriteMessage(Writer, "system", Prompt);
			WriteMessage(Writer, "user", Source);
			Writer.WriteEndArray();
			Writer.WriteEndObject();
		}
		return Encoding.UTF8.GetString(Buffer.ToArray());
	}

	[Doc("寫一條 chat 訊息。")]
	static void WriteMessage(Utf8JsonWriter Writer, str Role, str Content){
		Writer.WriteStartObject();
		Writer.WriteString("role", Role);
		Writer.WriteString("content", Content);
		Writer.WriteEndObject();
	}

	[Doc("模型有時會把 null 寫成字串（`\"null\"` 或 `~`）；這兩種都當「翻不了」。")]
	static bool IsNullLiteral(str Text){
		return str.Equals(Text, "null", StringComparison.OrdinalIgnoreCase) || Text == "~";
	}

	[Doc("日誌用：太長的回應截斷，避免一次日誌把檔案撐爆。")]
	static str Summarize(str Text){
		const i32 Max = 800;
		return Text.Length <= Max ? Text : Text[..Max] + "...";
	}
}
