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
		if(source.Length == 0){
			return new RespTranslate{ SourceText = "", TranslatedText = "" };
		}

		lock(_lock){
			if(_cache.TryGetValue(source, out var cached)){
				ConsoleColorOut.WriteLine("[AI翻譯][Cache]", cached, ConsoleColor.Green);
				return new RespTranslate{ SourceText = source, TranslatedText = cached };
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
				new{ role = "system", content = Opt.SystemPrompt },
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
			_cache[source] = translated;
		}

		Log.LogDebug("llm translated. sourceLen={SourceLen}; translatedLen={TranslatedLen}", source.Length, translated.Length);
		ConsoleColorOut.WriteLine("[AI翻譯]", translated, ConsoleColor.Green);
		return new RespTranslate{ SourceText = source, TranslatedText = translated };
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
