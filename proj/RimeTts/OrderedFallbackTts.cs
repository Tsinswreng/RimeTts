using System.IO;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class OrderedFallbackTts(
	OptTts Opt,
	GttsViaHttpTts Gtts,
	SystemSpeechTts SystemSpeech,
	ILogger<OrderedFallbackTts> Log
):ITts{
	public async Task<IPlayState> GenEtPlay(IReqGenEtPlay Req, CT Ct){
		var sourceEngines = Req.PreferredEngines is { Count: > 0 }
			? Req.PreferredEngines
			: (Opt.Engines is { Count: > 0 } ? Opt.Engines : new List<str>{ "gTTS", "SystemSpeech" });

		var engines = sourceEngines
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var errors = new List<Exception>();
		foreach(var engine in engines){
			Ct.ThrowIfCancellationRequested();
			try{
				if(engine.Equals("gTTS", StringComparison.OrdinalIgnoreCase)){
					//Log.LogInformation("tts try engine={Engine}", engine);
					return await Gtts.GenEtPlay(Req, Ct);
				}

				if(engine.Equals("SystemSpeech", StringComparison.OrdinalIgnoreCase)
					|| engine.Equals("System.Speech", StringComparison.OrdinalIgnoreCase)){
					//Log.LogInformation("tts try engine={Engine}", engine);
					return await SystemSpeech.GenEtPlay(Req, Ct);
				}

				//Log.LogWarning("unknown tts engine ignored: {Engine}", engine);
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

	public async Task<str> GenerateAudio(IReqGenEtPlay Req, CT Ct){
		var sourceEngines = Req.PreferredEngines is { Count: > 0 }
			? Req.PreferredEngines
			: (Opt.Engines is { Count: > 0 } ? Opt.Engines : new List<str>{ "gTTS", "SystemSpeech" });

		var engines = sourceEngines
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var errors = new List<Exception>();
		foreach(var engine in engines){
			Ct.ThrowIfCancellationRequested();
			try{
				if(engine.Equals("gTTS", StringComparison.OrdinalIgnoreCase)){
					//Log.LogInformation("tts generate try engine={Engine}", engine);
					return await Gtts.GenerateAudio(Req, Ct);
				}

				if(engine.Equals("SystemSpeech", StringComparison.OrdinalIgnoreCase)
					|| engine.Equals("System.Speech", StringComparison.OrdinalIgnoreCase)){
					//Log.LogInformation("tts generate try engine={Engine}", engine);
					return await SystemSpeech.GenerateAudio(Req, Ct);
				}

				Log.LogWarning("unknown tts engine ignored: {Engine}", engine);
			}
			catch(OperationCanceledException){
				throw;
			}
			catch(Exception ex){
				errors.Add(ex);
				Log.LogWarning(ex, "tts generate failed: {Engine}", engine);
			}
		}

		throw new AggregateException("all configured TTS engines failed", errors);
	}

	public async Task PlayAudio(str AudioFile, CT Ct){
		// 根据文件扩展名判断使用哪个引擎播放
		var ext = Path.GetExtension(AudioFile).ToLowerInvariant();
		try{
			if(ext == ".mp3"){
				await Gtts.PlayAudio(AudioFile, Ct);
			} else if(ext == ".wav"){
				await SystemSpeech.PlayAudio(AudioFile, Ct);
			} else {
				// 尝试用gTTS播放
				await Gtts.PlayAudio(AudioFile, Ct);
			}
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogWarning(ex, "play audio failed with gTTS, try system speech. file={AudioFile}", AudioFile);
			await SystemSpeech.PlayAudio(AudioFile, Ct);
		}
	}
}
