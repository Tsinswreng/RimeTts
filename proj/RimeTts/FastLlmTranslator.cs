using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class FastLlmTranslator(
	HttpClient Http,
	OptTranslator Opt,
	ILogger<FastLlmTranslator> Log
):ITranslator{
	private readonly Dictionary<str, str> _cache = new();
	private readonly Lock _lock = new();

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

		using var reqMsg = new HttpRequestMessage(HttpMethod.Post, Opt.BaseUrl);
		reqMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Opt.ApiKey);
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

		using var resp = await Http.SendAsync(reqMsg, cts.Token);
		if (!resp.IsSuccessStatusCode){
			var errorContent = await resp.Content.ReadAsStringAsync();
			Log.LogError("translator http failed. statusCode={StatusCode}; reason={ReasonPhrase}; body={Body}; source={Source}; lang={Lang}", (int)resp.StatusCode, resp.ReasonPhrase, errorContent, source, targetLang);
			throw new HttpRequestException($"Response status code does not indicate success: {(int)resp.StatusCode} ({resp.ReasonPhrase}). Details: {errorContent}");
		}
		resp.EnsureSuccessStatusCode();

		var json = await resp.Content.ReadAsStringAsync(cts.Token);
		var translated = ExtractContent(json).Trim();
		if(translated.Length == 0){
			throw new InvalidOperationException("translator response content is empty");
		}

		lock(_lock){
			_cache[cacheKey] = translated;
		}
		return new RespTranslate{ SourceText = source, TargetLanguage = targetLang, TranslatedText = translated };
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
		if(!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0){
			return "";
		}
		var first = choices[0];
		if(!first.TryGetProperty("message", out var message)){
			return "";
		}
		if(!message.TryGetProperty("content", out var contentEl)){
			return "";
		}
		return contentEl.GetString() ?? "";
	}
}
