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

		var source = Req.SourceText?.Trim() ?? "";
		var targetLang = NormalizeTargetLang(Req.TargetLanguage);
		var systemPrompt = (Req.SystemPrompt?.Trim() ?? "") is { Length: > 0 } reqPrompt
			? reqPrompt
			: (string.IsNullOrWhiteSpace(Opt.DefaultSystemPrompt)
				? "You are a fast translator. Translate source text to target language only. Return only translation text."
				: Opt.DefaultSystemPrompt);
		if(source.Length == 0){
			return new RespTranslate{ SourceText = "", TargetLanguage = targetLang, TranslatedText = "" };
		}

		var cacheKey = $"{targetLang}\n{systemPrompt}\n{source}";

		lock(_lock){
			if(_cache.TryGetValue(cacheKey, out var cached)){
				ConsoleColorOut.WriteLine("[AI翻譯][Cache]", cached, ConsoleColor.Green);
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
			temperature = 0.1,
			messages = new object[]{
				new{ role = "system", content = systemPrompt },
				new{ role = "user", content = source },
			},
		});
		reqMsg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, Opt.TimeoutSec)));

		using var resp = await Http.SendAsync(reqMsg, cts.Token);
		resp.EnsureSuccessStatusCode();

		var json = await resp.Content.ReadAsStringAsync(cts.Token);
		var translated = ExtractContent(json).Trim();
		if(translated.Length == 0){
			throw new InvalidOperationException("translator response content is empty");
		}

		lock(_lock){
			_cache[cacheKey] = translated;
		}

		Log.LogDebug("llm translated. target={Target}; sourceLen={SourceLen}; translatedLen={TranslatedLen}", targetLang, source.Length, translated.Length);
		ConsoleColorOut.WriteLine("[AI翻譯]", translated, ConsoleColor.Green);
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
