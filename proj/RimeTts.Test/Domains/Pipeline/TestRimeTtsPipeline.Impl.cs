using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Pipeline;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Translate;

namespace RimeTts.Test.Domains.Pipeline;

public partial class TestRimeTtsPipeline{

	[Doc($$"""
	造一條完整的假流水線：假監聽、真成句緩衝（假時鐘）、真扇出（假翻譯＋假合成）、真隊列（假播放）。
	#Rtn[各部件；測試自己決定怎麼驅動它。]
	""")]
	static (RimeTtsPipeline Svc, Fakes.FakeTypingListener Listener, Fakes.FakeTimeProvider Time, Fakes.FakeAudioPlayer Player, Fakes.FakeTranslator Tr) Mk(
		params str[] Langs
	){
		var Listener = new Fakes.FakeTypingListener();
		var Time = new Fakes.FakeTimeProvider();
		var Buffer = new SentenceBuffer(new OptSentenceSeg{
			NoCommitGapMs = 5000,
			SentenceTerminators = new List<str>{ "。" },
		}, Time);
		var Tr = new Fakes.FakeTranslator();
		var Tts = new Fakes.FakeTts();
		var Player = new Fakes.FakeAudioPlayer();
		var OptPipe = new OptPipeline{
			GenerateTimeoutSec = 5,
			MinPlayTimeoutSec = 5,
			PlayGraceSec = 1,
		};
		var Playlist = new AudioPlaylist(Player, OptPipe, NullLogger<AudioPlaylist>.Instance);
		var Profiles = Langs.Select(L => new LanguageProfile{ Language = L }).ToList();
		var Fanout = new SentenceFanout(
			new OptLanguagePipeline{ Languages = Profiles }
			,OptPipe
			,Tr
			,Tts
			,Playlist
			,NullLogger<SentenceFanout>.Instance
		);
		var Hotkey = new Fakes.FakeCancelHotkey();
		var Svc = new RimeTtsPipeline(
			Listener, Buffer, Fanout, Playlist, Hotkey, NullLogger<RimeTtsPipeline>.Instance
		);
		return (Svc, Listener, Time, Player, Tr);
	}

	public partial void RegisterPipeline(){
		this.AddTestCase(nameof(EndToEnd_PlaysInLanguageOrder), EndToEnd_PlaysInLanguageOrder!);
		this.AddTestCase(nameof(DeadConsumerLoop_TearsDownWholePipeline), DeadConsumerLoop_TearsDownWholePipeline!);
	}

	public partial async Task<nil> EndToEnd_PlaysInLanguageOrder(obj? O){
		var T = Assert.IsTrue;
		var (Svc, Listener, Time, Player, _) = Mk("en", "ja");
		using var Cts = new CancellationTokenSource();

		var Run = Svc.RunAsync(Cts.Token);
		// 打字、以終止符成句。
		Listener.EmitCommit("你好");
		Time.Advance(TimeSpan.FromMilliseconds(100));
		Listener.EmitCommit("世界。");

		// 給消費循環與播放一點時間（假播放器即刻完成）。
		await Task.Delay(300);
		Cts.Cancel();
		await Run;

		T(Player.Played.Count == 2,
			$"en 與 ja 各一段，實際 {Player.Played.Count} 段 [{string.Join(",", Player.Played)}]");
		// 成句＝片段以空格連接（你好＋世界。 → 「你好 世界。」）。
		T(Player.Played[0] == "en.en:你好 世界。.mp3", $"第一段應是 en，實際 {Player.Played[0]}");
		T(Player.Played[1] == "ja.ja:你好 世界。.mp3", $"第二段應是 ja，實際 {Player.Played[1]}");
		return NIL;
	}

	public partial async Task<nil> DeadConsumerLoop_TearsDownWholePipeline(obj? O){
		var T = Assert.IsTrue;
		// 監視器驗證（行為面）：全局取消後 RunAsync 必須在有界時間內返回——
		// 舊代碼的三循環 WhenAny/WhenAll 結構下，這正是殭屍態的判定動作（等不到返回）。
		var (Svc, Listener, Time, Player, _) = Mk("en");
		using var Cts = new CancellationTokenSource();

		var Run = Svc.RunAsync(Cts.Token);
		Listener.EmitCommit("你好。");
		await Task.Delay(200);
		// 正常關停：RunAsync 必須在有界時間內返回（監視器不該讓它掛着）。
		Cts.Cancel();
		var Finished = await Task.WhenAny(Run, Task.Delay(TimeSpan.FromSeconds(10)));
		T(ReferenceEquals(Finished, Run), "全局取消後 RunAsync 應在有界時間內返回（不得殭屍）");
		T(Listener.StopCount == 1, $"關停時應停監聽一次，實際 {Listener.StopCount} 次");
		return NIL;
	}
}
