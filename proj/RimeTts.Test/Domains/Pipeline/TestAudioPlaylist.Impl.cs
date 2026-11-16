using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Pipeline;

namespace RimeTts.Test.Domains.Pipeline;

public partial class TestAudioPlaylist{

	[Doc("造一個被測對象與它的假播放器；超時設定給足，避免測試被誤判為卡住。")]
	static (AudioPlaylist Playlist, Fakes.FakeAudioPlayer Player) Mk(){
		var Player = new Fakes.FakeAudioPlayer();
		var Opt = new OptPipeline{
			GenerateTimeoutSec = 5,
			MinPlayTimeoutSec = 5,
			PlayGraceSec = 1,
		};
		return (new AudioPlaylist(Player, Opt, NullLogger<AudioPlaylist>.Instance), Player);
	}

	public partial void RegisterPlaylist(){
		this.AddTestCase(nameof(PlaysInAddOrder_EvenIfLaterReadyFirst), PlaysInAddOrder_EvenIfLaterReadyFirst!);
		this.AddTestCase(nameof(SkipsFailedGeneration), SkipsFailedGeneration!);
		this.AddTestCase(nameof(Complete_DrainsRemainingJobs), Complete_DrainsRemainingJobs!);
	}

	public partial async Task<nil> PlaysInAddOrder_EvenIfLaterReadyFirst(obj? O){
		var T = Assert.IsTrue;
		var (Playlist, Player) = Mk();

		// 第一項的音頻「還沒生成好」，第二項已經好了。
		var Slow = new TaskCompletionSource<str?>();
		Playlist.Add(new AudioJob{ Language = "en", GenTask = Slow.Task });
		Playlist.Add(new AudioJob{ Language = "ja", GenTask = Task.FromResult<str?>("ja.mp3") });

		var Run = Playlist.RunAsync(default);
		// 給循環一點時間：它應該卡在第一項的生成任務上，而不是先播第二項。
		await Task.Delay(100);
		T(Player.Played.Count == 0, $"前一段還沒好時不該播後一段，實際已播 {Player.Played.Count} 段");

		Slow.SetResult("en.mp3");
		Playlist.Complete();
		await Run;

		T(Player.Played.Count == 2, $"應播兩段，實際 {Player.Played.Count} 段");
		T(string.Join(",", Player.Played) == "en.mp3,ja.mp3", $"播放順序應為 en,ja，實際 {string.Join(",", Player.Played)}");
		return NIL;
	}

	public partial async Task<nil> SkipsFailedGeneration(obj? O){
		var T = Assert.IsTrue;
		var (Playlist, Player) = Mk();

		Playlist.Add(new AudioJob{ Language = "en", GenTask = Task.FromResult<str?>(null) });
		Playlist.Add(new AudioJob{ Language = "ja", GenTask = Task.FromResult<str?>("ja.mp3") });

		Playlist.Complete();
		await Playlist.RunAsync(default);

		T(Player.Played.Count == 1 && Player.Played[0] == "ja.mp3",
			$"生成失敗的項應被跳過，實際播了 [{string.Join(",", Player.Played)}]");
		return NIL;
	}

	public partial async Task<nil> Complete_DrainsRemainingJobs(obj? O){
		var T = Assert.IsTrue;
		var (Playlist, Player) = Mk();

		Playlist.Add(new AudioJob{ Language = "en", GenTask = Task.FromResult<str?>("1.mp3") });
		Playlist.Add(new AudioJob{ Language = "ja", GenTask = Task.FromResult<str?>("2.mp3") });
		Playlist.Add(new AudioJob{ Language = "fr", GenTask = Task.FromResult<str?>("3.mp3") });

		// 先宣告結束、再開始跑：已入隊的項仍要全部播完（不是「關門就丟」）。
		Playlist.Complete();
		await Playlist.RunAsync(default);

		T(Player.Played.Count == 3, $"應播完 3 段，實際 {Player.Played.Count} 段");
		T(string.Join(",", Player.Played) == "1.mp3,2.mp3,3.mp3",
			$"播放順序不符，實際 {string.Join(",", Player.Played)}");
		return NIL;
	}
}
