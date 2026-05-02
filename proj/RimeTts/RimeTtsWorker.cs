using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class RimeTtsWorker(
	ITypingListener TypingListener,
	IOptSentenceSeg SegOpt,
	IOptLanguagePipeline LangPipelineOpt,
	ITranslator Translator,
	ITts Tts,
	ILogger<RimeTtsWorker> Log
):BackgroundService{
	private static readonly TimeSpan PerTranslationTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan PerAudioGenerateTimeout = TimeSpan.FromSeconds(40);
	private static readonly TimeSpan PerAudioPlayTimeout = TimeSpan.FromSeconds(20);
	private static readonly SemaphoreSlim SentenceGate = new(2, 2);
	private readonly Lock _bufLock = new();
	private readonly StringBuilder _buf = new();
	private DateTimeOffset _lastCommitAtUtc = DateTimeOffset.MinValue;
	private readonly Channel<ISentence> _sentenceQ = Channel.CreateUnbounded<ISentence>();
	private readonly Channel<AudioJob> _audioQ = Channel.CreateUnbounded<AudioJob>();

	protected override async Task ExecuteAsync(CT StoppingToken){
		TypingListener.CommitReceived += OnCommit;
		TypingListener.KeyEventReceived += OnKeyEvent;
		await TypingListener.StartAsync(StoppingToken);

		//Log.LogInformation("worker started");
		try{
			var timerTask = RunSegmentTimer(StoppingToken);
			var consumeTask = ConsumeSentenceQ(StoppingToken);
			var audioTask = ConsumeAudioQ(StoppingToken);
			await Task.WhenAll(timerTask, consumeTask, audioTask);
		}
		finally{
			TypingListener.CommitReceived -= OnCommit;
			TypingListener.KeyEventReceived -= OnKeyEvent;
			await TypingListener.StopAsync(CancellationToken.None);
		}
	}

	private void OnCommit(IDtoCommit Commit){
		if(string.IsNullOrWhiteSpace(Commit.Text)){
			return;
		}

		Log.LogCommitText(Commit.Text);

		lock(_bufLock){
			_buf.Append(Commit.Text);
			_lastCommitAtUtc = DateTimeOffset.UtcNow;
			if(IsSentenceBoundary(Commit.Text)){
				FlushSentenceUnsafe();
			}
		}
	}

	private void OnKeyEvent(IDtoKeyEvent KeyEvent){
		_ = KeyEvent;
	}

	private async Task RunSegmentTimer(CT Ct){
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
		while(await timer.WaitForNextTickAsync(Ct)){
			var now = DateTimeOffset.UtcNow;
			lock(_bufLock){
				if(_buf.Length == 0 || _lastCommitAtUtc == DateTimeOffset.MinValue){
					continue;
				}
				var gapMs = (now - _lastCommitAtUtc).TotalMilliseconds;
				if(gapMs >= SegOpt.NoCommitGapMs){
					FlushSentenceUnsafe();
				}
			}
		}
	}

	private async Task ConsumeSentenceQ(CT Ct){
		while(await _sentenceQ.Reader.WaitToReadAsync(Ct)){
			while(_sentenceQ.Reader.TryRead(out var sentence)){
				_ = Task.Run(() => ProcessSentenceDetached(sentence, Ct), Ct);
			}
		}
	}

	private async Task ProcessSentenceDetached(ISentence sentence, CT Ct){
		await SentenceGate.WaitAsync(Ct);
		try{
			await ProcessSentence(sentence, Ct);
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogError(ex, "sentence task failed. text={Sentence}", sentence.Text);
		}
		finally{
			SentenceGate.Release();
		}
	}

	private async Task ProcessSentence(ISentence sentence, CT Ct){
		var source = sentence.Text.Trim();
		if(source.Length == 0){
			return;
		}

		try{
			var languageProfiles = LangPipelineOpt.Languages
				.Where(x => x is not null)
				.Select(x => new{
					Lang = NormalizeLang(x.Language),
					Prompt = x.SystemPrompt?.Trim() ?? "",
					TtsEngines = x.TtsEngines?.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToList() ?? new List<str>(),
				})
				.Where(x => x.Lang.Length > 0)
				.ToList();

			if(languageProfiles.Count == 0){
				Log.LogWarning("no language profiles configured, fallback to en");
				var translation = await TranslateWithTimeout(source, "en", "", Ct);
				if(translation != null){
					await EnqueueAudioJob(new AudioJob{
						Source = source,
						Language = "en",
						TranslatedText = translation,
						PreferredEngines = null,
					}, Ct);
				}
				return;
			}

			foreach(var profile in languageProfiles){
				var translation = await TranslateWithTimeout(
					source,
					profile.Lang,
					profile.Prompt,
					Ct
				);
				if(translation != null){
					await EnqueueAudioJob(new AudioJob{
						Source = source,
						Language = profile.Lang,
						TranslatedText = translation,
						PreferredEngines = profile.TtsEngines,
					}, Ct);
				}
			}
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogError(ex, "process sentence failed. text={Sentence}", source);
		}
	}

	private async Task<str?> TranslateWithTimeout(str source, str lang, str prompt, CT outerCt){
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
		cts.CancelAfter(PerTranslationTimeout);
		try{
			return await TranslateOnly(source, lang, prompt, cts.Token);
		}
		catch(OperationCanceledException) when(!outerCt.IsCancellationRequested && cts.IsCancellationRequested){
			Log.LogWarning("translation timed out. source={Source}; lang={Lang}; timeoutSec={TimeoutSec}", source, lang, PerTranslationTimeout.TotalSeconds);
			return null;
		}
	}

	private async Task<str?> TranslateOnly(str source, str lang, str prompt, CT Ct){
		try{
			var tr = await Translator.Translate(new ReqTranslate{
				SourceText = source,
				TargetLanguage = lang,
				SystemPrompt = prompt,
			}, Ct);
			var target = tr.TranslatedText.Trim();
			if(target.Length == 0){
				Log.LogWarning("translation empty. source={Source}; lang={Lang}", source, lang);
				return null;
			}
			Log.LogTranslationText(target);
			return target;
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogWarning(ex, "translation failed. source={Source}; lang={Lang}", source, lang);
			return null;
		}
	}

	private async Task EnqueueAudioJob(AudioJob job, CT Ct){
		await _audioQ.Writer.WriteAsync(job, Ct);
	}

	private async Task ConsumeAudioQ(CT Ct){
		while(await _audioQ.Reader.WaitToReadAsync(Ct)){
			while(_audioQ.Reader.TryRead(out var job)){
				await GenerateAndPlayAudio(job, Ct);
			}
		}
	}

	private async Task GenerateAndPlayAudio(AudioJob job, CT Ct){
		try{
			var audioFile = await GenerateAudioWithTimeout(job, Ct);
			if(audioFile is null){
				return;
			}
			await PlayAudioWithTimeout(audioFile, job.Language, Ct);
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogWarning(ex, "audio pipeline failed. source={Source}; lang={Lang}", job.Source, job.Language);
		}
	}

	private async Task<str?> GenerateAudioWithTimeout(AudioJob job, CT outerCt){
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
		cts.CancelAfter(PerAudioGenerateTimeout);
		try{
			var audioFile = await Tts.GenerateAudio(new ReqGenEtPlay{
				Text = job.TranslatedText,
				Language = job.Language,
				PreferredEngines = job.PreferredEngines,
			}, cts.Token);
			Log.LogDebug("audio generated. lang={Lang}; file={File}", job.Language, audioFile);
			return audioFile;
		}
		catch(OperationCanceledException) when(!outerCt.IsCancellationRequested && cts.IsCancellationRequested){
			Log.LogWarning("audio generate timed out. source={Source}; lang={Lang}; timeoutSec={TimeoutSec}", job.Source, job.Language, PerAudioGenerateTimeout.TotalSeconds);
			return null;
		}
	}

	private async Task PlayAudioWithTimeout(str audioFile, str lang, CT outerCt){
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
		cts.CancelAfter(PerAudioPlayTimeout);
		try{
			await Tts.PlayAudio(audioFile, cts.Token);
		}
		catch(OperationCanceledException) when(!outerCt.IsCancellationRequested && cts.IsCancellationRequested){
			Log.LogWarning("audio play timed out. lang={Lang}; file={AudioFile}; timeoutSec={TimeoutSec}", lang, audioFile, PerAudioPlayTimeout.TotalSeconds);
		}
	}

	private static str NormalizeLang(str? lang){
		if(string.IsNullOrWhiteSpace(lang)){
			return "";
		}
		var norm = lang.Trim().ToLowerInvariant();
		return norm switch{
			"jp" => "ja",
			_ => norm,
		};
	}

	private void FlushSentenceUnsafe(){
		var text = _buf.ToString().Trim();
		_buf.Clear();
		if(text.Length == 0){
			return;
		}
		_sentenceQ.Writer.TryWrite(new Sentence{
			Text = text,
		});
		Log.LogSentenceText(text);
	}

	private bool IsSentenceBoundary(str text){
		if(text.Length == 0){
			return false;
		}

		// 如果配置了终止符，使用配置的终止符
		if(SegOpt.SentenceTerminators != null && SegOpt.SentenceTerminators.Count > 0){
			var c = text[^1];
			foreach(var terminator in SegOpt.SentenceTerminators){
				if(terminator.Length == 1 && c == terminator[0]){
					return true;
				}
			}
			return false;
		}

		// 没配置终止符就不使用终止符，只依靠时间间隔
		return false;
	}

	private sealed class AudioJob{
		public str Source{get;set;} = "";
		public str Language{get;set;} = "";
		public str TranslatedText{get;set;} = "";
		public List<str>? PreferredEngines{get;set;}
	}
}
