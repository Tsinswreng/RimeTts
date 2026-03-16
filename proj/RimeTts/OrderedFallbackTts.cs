using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class OrderedFallbackTts(
	OptTts Opt,
	GttsViaHttpTts Gtts,
	SystemSpeechTts SystemSpeech,
	ILogger<OrderedFallbackTts> Log
):ITts{
	public async Task<IPlayState> GenEtPlay(IReqGenEtPlay Req, CT Ct){
		var engines = (Opt.Engines is { Count: > 0 } ? Opt.Engines : new List<str>{ "gTTS", "SystemSpeech" })
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var errors = new List<Exception>();
		foreach(var engine in engines){
			Ct.ThrowIfCancellationRequested();
			try{
				if(engine.Equals("gTTS", StringComparison.OrdinalIgnoreCase)){
					Log.LogInformation("tts try engine={Engine}", engine);
					return await Gtts.GenEtPlay(Req, Ct);
				}

				if(engine.Equals("SystemSpeech", StringComparison.OrdinalIgnoreCase)
					|| engine.Equals("System.Speech", StringComparison.OrdinalIgnoreCase)){
					Log.LogInformation("tts try engine={Engine}", engine);
					return await SystemSpeech.GenEtPlay(Req, Ct);
				}

				Log.LogWarning("unknown tts engine ignored: {Engine}", engine);
			}
			catch(OperationCanceledException){
				throw;
			}
			catch(Exception ex){
				errors.Add(ex);
				Log.LogWarning(ex, "tts engine failed: {Engine}", engine);
			}
		}

		throw new AggregateException("all configured TTS engines failed", errors);
	}
}