using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class FastLlmTranslator(
	OptTranslator Opt,
	ILogger<FastLlmTranslator> Log
):ITranslator{
	private readonly Dictionary<str, str> _cache = new();
	private readonly Lock _lock = new();
	private readonly SemaphoreSlim _translateGate = new(1, 1);

	public async Task<IRespTranslate> Translate(IReqTranslate Req, CT Ct){
		if(Req is null){
			throw new ArgumentNullException(nameof(Req));
		}
		var systemPrompt = Req.SystemPrompt;
		var source = Req.SourceText;

		var targetLang = NormalizeTargetLang(Req.TargetLanguage);

		if(source.Length == 0){
			return new RespTranslate{ SourceText = "", TargetLanguage = targetLang, TranslatedText = "" };
		}

		var cacheKey = $"{targetLang}\n{systemPrompt}\n{source}";

		lock(_lock){
			if(_cache.TryGetValue(cacheKey, out var cached)){
				Log.LogTranslationCacheText(cached);
				return new RespTranslate{ SourceText = source, TargetLanguage = targetLang, TranslatedText = cached };
			}
		}

		if(string.IsNullOrWhiteSpace(Opt.ApiKey)){
			throw new InvalidOperationException("translator api key is empty. set RIMETTS_LLM_API_KEY");
		}

		await _translateGate.WaitAsync(Ct);
		try{
			using var http = CreateIsolatedHttpClient();
			using var reqMsg = new HttpRequestMessage(HttpMethod.Post, Opt.BaseUrl);
			reqMsg.Version = HttpVersion.Version11;
			reqMsg.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
			reqMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Opt.ApiKey);
			reqMsg.Headers.ConnectionClose = true;
			var payload = JsonSerializer.Serialize(new{
				model = Opt.Model,
				messages = new object[]{
					new{ role = "system", content = systemPrompt },
					new{ role = "user", content = source },
				},
			});
			reqMsg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
			cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, Opt.TimeoutSec)));

			using var resp = await http.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			if (!resp.IsSuccessStatusCode){
				var errorContent = await resp.Content.ReadAsStringAsync();
				Log.LogError("translator http failed. statusCode={StatusCode}; reason={ReasonPhrase}; body={Body}; source={Source}; lang={Lang}", (int)resp.StatusCode, resp.ReasonPhrase, errorContent, source, targetLang);
				throw new HttpRequestException($"Response status code does not indicate success: {(int)resp.StatusCode} ({resp.ReasonPhrase}). Details: {errorContent}");
			}
			resp.EnsureSuccessStatusCode();

			var json = await resp.Content.ReadAsStringAsync(cts.Token);
			var translated = ExtractContent(json).Trim();
			if(translated.Length == 0){
				Log.LogError("translator response content empty. source={Source}; lang={Lang}; response={Response}", source, targetLang, SummarizeJson(json));
				throw new InvalidOperationException("translator response content is empty");
			}

			lock(_lock){
				_cache[cacheKey] = translated;
			}
			return new RespTranslate{ SourceText = source, TargetLanguage = targetLang, TranslatedText = translated };
		}
		finally{
			_translateGate.Release();
		}
	}

	private static HttpClient CreateIsolatedHttpClient(){
		var handler = new SocketsHttpHandler{
			PooledConnectionLifetime = TimeSpan.Zero,
			PooledConnectionIdleTimeout = TimeSpan.Zero,
			MaxConnectionsPerServer = 1,
			EnableMultipleHttp2Connections = false,
			UseCookies = false,
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
		};
		return new HttpClient(handler, disposeHandler: true){
			Timeout = Timeout.InfiniteTimeSpan,
			DefaultRequestVersion = HttpVersion.Version11,
			DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
		};
	}

	private static str NormalizeTargetLang(str? targetLang){
		if(string.IsNullOrWhiteSpace(targetLang)){
			return "en";
		}
		var norm = targetLang.Trim().ToLowerInvariant();
		return norm switch{
			"jp" => "ja",
			_ => norm,
		};
	}

	private static str ExtractContent(str Json){
		using var doc = JsonDocument.Parse(Json);
		if(TryExtractFromChoices(doc.RootElement, out var fromChoices)){
			return fromChoices;
		}
		if(TryExtractRootOutputText(doc.RootElement, out var fromRootOutput)){
			return fromRootOutput;
		}
		return "";
	}

	private static bool TryExtractFromChoices(JsonElement root, out str text){
		text = "";
		if(!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0){
			return false;
		}

		var first = choices[0];
		if(first.TryGetProperty("message", out var message) && TryExtractMessageContent(message, out text)){
			return true;
		}
		if(first.TryGetProperty("delta", out var delta) && TryExtractMessageContent(delta, out text)){
			return true;
		}
		if(first.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String){
			text = textEl.GetString() ?? "";
			return text.Length > 0;
		}
		return false;
	}

	private static bool TryExtractRootOutputText(JsonElement root, out str text){
		text = "";
		if(root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String){
			text = outputText.GetString() ?? "";
			return text.Length > 0;
		}
		if(root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array){
			var sb = new StringBuilder();
			foreach(var item in output.EnumerateArray()){
				if(item.TryGetProperty("content", out var content) && TryExtractContentValue(content, out var part) && part.Length > 0){
					if(sb.Length > 0){
						sb.Append('\n');
					}
					sb.Append(part);
				}
			}
			text = sb.ToString().Trim();
			return text.Length > 0;
		}
		return false;
	}

	private static bool TryExtractMessageContent(JsonElement message, out str text){
		text = "";
		if(!message.TryGetProperty("content", out var contentEl)){
			return false;
		}
		return TryExtractContentValue(contentEl, out text);
	}

	private static bool TryExtractContentValue(JsonElement contentEl, out str text){
		text = "";
		if(contentEl.ValueKind == JsonValueKind.String){
			text = contentEl.GetString() ?? "";
			return text.Length > 0;
		}
		if(contentEl.ValueKind != JsonValueKind.Array){
			return false;
		}

		var sb = new StringBuilder();
		foreach(var item in contentEl.EnumerateArray()){
			if(item.ValueKind == JsonValueKind.String){
				var s = item.GetString() ?? "";
				if(s.Length > 0){
					if(sb.Length > 0){
						sb.Append('\n');
					}
					sb.Append(s);
				}
				continue;
			}
			if(item.ValueKind == JsonValueKind.Object){
				var part = ExtractTextFromContentObject(item);
				if(part.Length > 0){
					if(sb.Length > 0){
						sb.Append('\n');
					}
					sb.Append(part);
				}
			}
		}

		text = sb.ToString().Trim();
		return text.Length > 0;
	}

	private static str ExtractTextFromContentObject(JsonElement item){
		if(item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String){
			return textEl.GetString() ?? "";
		}
		if(item.TryGetProperty("output_text", out var outputTextEl) && outputTextEl.ValueKind == JsonValueKind.String){
			return outputTextEl.GetString() ?? "";
		}
		if(item.TryGetProperty("content", out var nestedContent) && nestedContent.ValueKind == JsonValueKind.String){
			return nestedContent.GetString() ?? "";
		}
		return "";
	}

	private static str SummarizeJson(str json){
		const int maxLen = 800;
		if(json.Length <= maxLen){
			return json;
		}
		return json[..maxLen] + "...";
	}
}
