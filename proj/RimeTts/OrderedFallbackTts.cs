using System.IO;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class OrderedFallbackTts(
	OptTts Opt,
	GttsViaHttpTts Gtts,
	SystemSpeechTts SystemSpeech,
	ILogger<OrderedFallbackTts> Log
):ITts{
	private static readonly TimeSpan GenerateTimeout = TimeSpan.FromSeconds(12);
	private static readonly TimeSpan PlayTimeout = TimeSpan.FromSeconds(20);

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
					return await RunWithTimeout(
						engine,
						"gen-play",
						innerCt => Gtts.GenEtPlay(Req, innerCt),
						PlayTimeout,
						Ct
					);
				}

				if(engine.Equals("SystemSpeech", StringComparison.OrdinalIgnoreCase)
					|| engine.Equals("System.Speech", StringComparison.OrdinalIgnoreCase)){
					return await RunWithTimeout(
						engine,
						"gen-play",
						innerCt => SystemSpeech.GenEtPlay(Req, innerCt),
						PlayTimeout,
						Ct
					);
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
					return await RunWithTimeout(
						engine,
						"generate",
						innerCt => Gtts.GenerateAudio(Req, innerCt),
						GenerateTimeout,
						Ct
					);
				}

				if(engine.Equals("SystemSpeech", StringComparison.OrdinalIgnoreCase)
					|| engine.Equals("System.Speech", StringComparison.OrdinalIgnoreCase)){
					return await RunWithTimeout(
						engine,
						"generate",
						innerCt => SystemSpeech.GenerateAudio(Req, innerCt),
						GenerateTimeout,
						Ct
					);
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
				await RunWithTimeout(
					"gTTS",
					"play",
					innerCt => Gtts.PlayAudio(AudioFile, innerCt),
					PlayTimeout,
					Ct
				);
			} else if(ext == ".wav"){
				await RunWithTimeout(
					"SystemSpeech",
					"play",
					innerCt => SystemSpeech.PlayAudio(AudioFile, innerCt),
					PlayTimeout,
					Ct
				);
			} else {
				await RunWithTimeout(
					"gTTS",
					"play",
					innerCt => Gtts.PlayAudio(AudioFile, innerCt),
					PlayTimeout,
					Ct
				);
			}
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogWarning(ex, "play audio failed with gTTS, try system speech. file={AudioFile}", AudioFile);
			await RunWithTimeout(
				"SystemSpeech",
				"play-fallback",
				innerCt => SystemSpeech.PlayAudio(AudioFile, innerCt),
				PlayTimeout,
				Ct
			);
		}
	}

	private async Task<T> RunWithTimeout<T>(str engine, str stage, Func<CT, Task<T>> action, TimeSpan timeout, CT outerCt){
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
		cts.CancelAfter(timeout);
		try{
			return await action(cts.Token);
		}
		catch(OperationCanceledException ex) when(!outerCt.IsCancellationRequested && cts.IsCancellationRequested){
			throw new TimeoutException($"tts {stage} timed out after {timeout.TotalSeconds:0}s. engine={engine}", ex);
		}
	}

	private async Task RunWithTimeout(str engine, str stage, Func<CT, Task> action, TimeSpan timeout, CT outerCt){
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
		cts.CancelAfter(timeout);
		try{
			await action(cts.Token);
		}
		catch(OperationCanceledException ex) when(!outerCt.IsCancellationRequested && cts.IsCancellationRequested){
			throw new TimeoutException($"tts {stage} timed out after {timeout.TotalSeconds:0}s. engine={engine}", ex);
		}
	}
}
