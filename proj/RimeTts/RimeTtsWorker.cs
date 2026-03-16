using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class RimeTtsWorker(
	ITypingListener TypingListener,
	IOptSentenceSeg SegOpt,
	ITranslator Translator,
	ITts Tts,
	ILogger<RimeTtsWorker> Log
):BackgroundService{
	private static readonly Lock ConsoleLock = new();

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

		WriteColorLine("[上屏詞]", Commit.Text, ConsoleColor.Cyan);

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
			var tr = await Translator.Translate(new ReqTranslate{ SourceText = source }, Ct);
			var target = tr.TranslatedText.Trim();
			if(target.Length == 0){
				Log.LogWarning("translation empty. source={Source}", source);
				return;
			}

			_ = await Tts.GenEtPlay(new ReqGenEtPlay{
				Text = target,
				Language = "en",
			}, Ct);
		}
		catch(OperationCanceledException){
			throw;
		}
		catch(Exception ex){
			Log.LogError(ex, "process sentence failed. text={Sentence}", source);
		}
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
		WriteColorLine("[成句]", text, ConsoleColor.Yellow);
	}

	private static void WriteColorLine(str tag, str text, ConsoleColor color){
		lock(ConsoleLock){
			var old = Console.ForegroundColor;
			try{
				Console.ForegroundColor = color;
				Console.WriteLine($"{DateTime.Now:HH:mm:ss} {tag} {text}");
			}
			finally{
				Console.ForegroundColor = old;
			}
		}
	}

	private static bool IsSentenceBoundary(str text){
		if(text.Length == 0){
			return false;
		}
		var c = text[^1];
		return c is '.' or '!' or '?' or ';' or '。' or '！' or '？' or '；';
	}
}
