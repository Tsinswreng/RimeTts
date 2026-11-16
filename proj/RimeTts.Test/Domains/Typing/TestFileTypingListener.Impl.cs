using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Typing;

namespace RimeTts.Test.Domains.Typing;

public partial class TestFileTypingListener{

	[Doc($$"""
	在工作區內造一對臨時的內容／信號檔與被測對象。
	#Rtn[被測對象、內容檔路徑、信號檔路徑、收到的上屏清單。]
	""")]
	static (FileTypingListener L, str Content, str Signal, IList<str> Got) Mk(){
		// 測試資料一律放工作區內（規矩見 Delivery）：用測試程序集輸出目錄下的子目錄，
		// 每個用例一個（隨機名），用後刪。
		var Dir = Path.Combine(AppContext.BaseDirectory, "test-listener", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Dir);
		var Content = Path.Combine(Dir, "content.json");
		var Signal = Path.Combine(Dir, "signal.touch");
		File.WriteAllText(Content, "");
		File.WriteAllText(Signal, "");
		var Got = new List<str>();
		var L = new FileTypingListener(
			new OptFileInteractor{ ContentFile = Content, SignalFile = Signal }
			,NullLogger<FileTypingListener>.Instance
		);
		L.CommitReceived += C => Got.Add(C.Text);
		return (L, Content, Signal, Got);
	}

	[Doc("碰一下信號檔（改寫時間戳），模擬 Lua 側的「寫完內容後摸信號」。")]
	static async Task TouchSignal(str Signal){
		File.SetLastWriteTimeUtc(Signal, DateTime.UtcNow);
		// 監看事件非同步到達＋內容讀取有重試間隔；輪詢等到事件派發或超時。
		await Task.Delay(50);
	}

	public partial void RegisterListener(){
		this.AddTestCase(nameof(SignalTouch_DispatchesCommit), SignalTouch_DispatchesCommit!);
		this.AddTestCase(nameof(GarbageContent_IsIgnored), GarbageContent_IsIgnored!);
	}

	public partial async Task<nil> SignalTouch_DispatchesCommit(obj? O){
		var T = Assert.IsTrue;
		var (L, Content, Signal, Got) = Mk();
		await L.StartAsync(default);
		try{
			File.WriteAllText(Content, $$"""{"Type":"Commit","Text":"你好"}""");
			// 監看事件非同步到達＋內容讀取有重試間隔：輪詢直到派發或超時（約 2 秒上限）。
			for(var I = 0; I < 40 && Got.Count == 0; I++){
				await TouchSignal(Signal);
			}
			T(Got.Count >= 1 && Got[0] == "你好",
				$"信號觸發後應派發上屏「你好」，實際 [{string.Join(",", Got)}]");
		}
		finally{
			await L.StopAsync(default);
		}
		return NIL;
	}

	public partial async Task<nil> GarbageContent_IsIgnored(obj? O){
		var T = Assert.IsTrue;
		var (L, Content, Signal, Got) = Mk();
		await L.StartAsync(default);
		try{
			// 半截 JSON：讀取重試用盡後應安靜放棄（記警告），不派發、不拋。
			File.WriteAllText(Content, $$"""{"Type":"Comm""");
			for(var I = 0; I < 40; I++){
				await TouchSignal(Signal);
			}
			T(Got.Count == 0, $"半截內容不該派發，實際派發了 {Got.Count} 次");
		}
		finally{
			await L.StopAsync(default);
		}
		return NIL;
	}
}
