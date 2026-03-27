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
	private readonly Lock _bufLock = new();
	private readonly StringBuilder _buf = new();
	private DateTimeOffset _lastCommitAtUtc = DateTimeOffset.MinValue;
	private readonly Channel<ISentence> _sentenceQ = Channel.CreateUnbounded<ISentence>();

	protected override async Task ExecuteAsync(CT StoppingToken){
		TypingListener.CommitReceived += OnCommit;
		TypingListener.KeyEventReceived += OnKeyEvent;
		await TypingListener.StartAsync(StoppingToken);

		Log.LogInformation("worker started");
		try{
			var timerTask = RunSegmentTimer(StoppingToken);
			var consumeTask = ConsumeSentenceQ(StoppingToken);
			await Task.WhenAll(timerTask, consumeTask);
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

		ConsoleColorOut.WriteLine("[上屏詞]", Commit.Text, ConsoleColor.Cyan);

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
				await ProcessSentence(sentence, Ct);
			}
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
				var audioFile = await TranslateAndGenerateAudio(source, "en", "", null, Ct);
				if(audioFile != null){
					await Tts.PlayAudio(audioFile, Ct);
				}
				return;
			}

			// 并行获取所有语言的翻译和音频
			var audioTasks = languageProfiles.Select(p =>
				TranslateAndGenerateAudio(source, p.Lang, p.Prompt, p.TtsEngines, Ct)
			).ToList();

			var audioFiles = await Task.WhenAll(audioTasks);

			// 按顺序播放音频
			foreach(var audioFile in audioFiles){
				if(audioFile != null){
					await Tts.PlayAudio(audioFile, Ct);
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

	private async Task<str?> TranslateAndGenerateAudio(str source, str lang, str prompt, List<str>? preferredEngines, CT Ct){
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

			var audioFile = await Tts.GenerateAudio(new ReqGenEtPlay{
				Text = target,
				Language = lang,
				PreferredEngines = preferredEngines,
			}, Ct);
			Log.LogDebug("audio generated. lang={Lang}; file={File}", lang, audioFile);
			return audioFile;
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogWarning(ex, "translate/generate audio failed. source={Source}; lang={Lang}", source, lang);
			return null;
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
		ConsoleColorOut.WriteLine("[成句]", text, ConsoleColor.Yellow);
	}

	private static bool IsSentenceBoundary(str text){
		if(text.Length == 0){
			return false;
		}
		var c = text[^1];
		return c is '.' or '!' or '?' or ';' or '。' or '！' or '？' or '；';
	}
}
