using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Pipeline;
using RimeTts.Domains.Translate;

namespace RimeTts.Test.Domains.Pipeline;

public partial class TestSentenceFanout{

	[Doc($$"""
	造一個被測對象：語言清單由測試給；超時給一個「測試裡等得到」的值。
	#Rtn[扇出、假翻譯、假合成、播放隊列本體與假播放器。]
	""")]
	static (SentenceFanout Fanout, Fakes.FakeTranslator Tr, Fakes.FakeTts Tts, AudioPlaylist Playlist, Fakes.FakeAudioPlayer Player) Mk(
		params str[] Langs
	){
		var Tr = new Fakes.FakeTranslator();
		var Tts = new Fakes.FakeTts();
		var Player = new Fakes.FakeAudioPlayer();
		var OptPipe = new OptPipeline{
			GenerateTimeoutSec = 1,
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
		return (Fanout, Tr, Tts, Playlist, Player);
	}

	public partial void RegisterFanout(){
		this.AddTestCase(nameof(EnqueuesPerLanguage_InConfigOrder), EnqueuesPerLanguage_InConfigOrder!);
		this.AddTestCase(nameof(TranslateFailure_IsolatesItsOwnSlot), TranslateFailure_IsolatesItsOwnSlot!);
		this.AddTestCase(nameof(PerRequestCancel_DoesNotKillPipeline), PerRequestCancel_DoesNotKillPipeline!);
		this.AddTestCase(nameof(GenerateTimeout_AbortsOnlyThatLanguage), GenerateTimeout_AbortsOnlyThatLanguage!);
		this.AddTestCase(nameof(EmptyProfiles_DropsSentence_NoEnFallback), EmptyProfiles_DropsSentence_NoEnFallback!);
		this.AddTestCase(nameof(NormalizesLanguageTags), NormalizesLanguageTags!);
	}

	public partial async Task<nil> EnqueuesPerLanguage_InConfigOrder(obj? O){
		var T = Assert.IsTrue;
		var (Fanout, Tr, _, Playlist, _) = Mk("en", "ja", "fr");

		await Fanout.RunAsync("你好", default);

		// 入隊完成即返回：此時翻譯已經被請求（鏈是熱的），順序即配置順序。
		T(Tr.RequestedLangs.Count == 3, $"三個語言都該被請求翻譯，實際 {Tr.RequestedLangs.Count} 個");
		T(string.Join(",", Tr.RequestedLangs) == "en,ja,fr",
			$"翻譯請求順序應為 en,ja,fr，實際 {string.Join(",", Tr.RequestedLangs)}");
		Playlist.Complete();
		await Playlist.RunAsync(default);
		return NIL;
	}

	public partial async Task<nil> TranslateFailure_IsolatesItsOwnSlot(obj? O){
		var T = Assert.IsTrue;
		var (Fanout, Tr, _, Playlist, Player) = Mk("en", "ja");
		// ja 翻譯直接拋異常（例如 HTTP 失敗）。
		Tr.FailByLang["ja"] = new InvalidOperationException("fake http failure");

		await Fanout.RunAsync("你好", default);
		Playlist.Complete();
		await Playlist.RunAsync(default);

		T(Player.Played.Count == 1 && Player.Played[0] == "en.en:你好.mp3",
			$"ja 失敗該被跳過、en 照常播，實際播了 [{string.Join(",", Player.Played)}]");
		return NIL;
	}

	public partial async Task<nil> PerRequestCancel_DoesNotKillPipeline(obj? O){
		var T = Assert.IsTrue;
		var (Fanout, Tr, _, Playlist, Player) = Mk("en", "ja");
		// ja 的翻譯掛在「內部令牌」上懸住，然後內部取消——
		// 模擬翻譯器內部 CTS 到點時 HttpClient 拋的 OCE（外部令牌從未取消）。BUG-1 的場景。
		var InternalCts = new CancellationTokenSource();
		Tr.InternalCancelByLang["ja"] = InternalCts;

		await Fanout.RunAsync("第一句", default);
		// 等到 ja 的翻譯真的進入懸住之後再內部取消它。
		await Task.Delay(50);
		InternalCts.Cancel();
		Playlist.Complete();
		await Playlist.RunAsync(default);

		// 核心斷言：外部令牌從未取消，故這不是關停；en 必須照常播，RunAsync 不得上拋。
		T(Player.Played.Count == 1 && Player.Played[0] == "en.en:第一句.mp3",
			$"ja 的請求級取消不該影響 en，實際播了 [{string.Join(",", Player.Played)}]");
		return NIL;
	}

	public partial async Task<nil> GenerateTimeout_AbortsOnlyThatLanguage(obj? O){
		var T = Assert.IsTrue;
		var (Fanout, _, Tts, Playlist, Player) = Mk("en", "ja");
		// 合成延遲 60 秒（延遲期間會回應取消——兩個真實引擎都如此），
		// 而 GenerateTimeoutSec=1：扇出層應在一秒後放棄這一段（回 null），而不是等足 60 秒。
		Tts.Delay = TimeSpan.FromSeconds(60);

		var Sw = System.Diagnostics.Stopwatch.StartNew();
		await Fanout.RunAsync("你好", default);
		Playlist.Complete();
		await Playlist.RunAsync(default);
		// 播放循環結束＝所有鏈都定型；耗時應接近超時（1s）而不是延遲（60s）。
		T(Sw.Elapsed < TimeSpan.FromSeconds(10),
			$"生成超時應及時放棄（1s 量級），實際等了 {Sw.Elapsed.TotalSeconds:F1}s");
		T(Player.Played.Count == 0, $"超時的段不該被播，實際播了 {Player.Played.Count} 段");
		return NIL;
	}

	public partial async Task<nil> EmptyProfiles_DropsSentence_NoEnFallback(obj? O){
		var T = Assert.IsTrue;
		// 語言條目全部無效（空字串）：歸一化後清單為空。
		var (Fanout, Tr, _, Playlist, Player) = Mk("", "");

		await Fanout.RunAsync("你好", default);
		Playlist.Complete();
		await Playlist.RunAsync(default);

		T(Tr.RequestedLangs.Count == 0,
			$"空清單不該觸發任何翻譯（更不該兜底翻英文），實際被請求 {Tr.RequestedLangs.Count} 次");
		T(Player.Played.Count == 0, "空清單不該有任何播放");
		return NIL;
	}

	public partial async Task<nil> NormalizesLanguageTags(obj? O){
		var T = Assert.IsTrue;
		var (Fanout, Tr, _, Playlist, _) = Mk("JP");

		await Fanout.RunAsync("你好", default);
		Playlist.Complete();
		await Playlist.RunAsync(default);

		T(Tr.RequestedLangs.Count == 1 && Tr.RequestedLangs[0] == "ja",
			$"JP 應歸一化成 ja 再翻譯，實際 {string.Join(",", Tr.RequestedLangs)}");
		return NIL;
	}
}
