namespace RimeTts.Domains.Tts;

using System.Globalization;
using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using RimeTts.Infra;

[Doc($$"""
Windows 自帶的離線 TTS（SAPI）：把文本合成成 wav 檔。

*為何留著它*：gTTS 依賴網絡，而這個工具的用途是「邊打字邊聽」——網絡抽風時不能整個啞掉。
它是兜底，音色機械但一定在。

*為何每次合成都要新開一個 {{nameof(SpeechSynthesizer)}}*：SAPI 的合成器物件綁著輸出目標
（檔案／設備），而且不是線程安全的；共用一份會出現「兩個語言同時合成時輸出交錯」。
新開一個的成本很低（它就是個 COM 包裝）。

*為何要 Task.Run*：{{nameof(SpeechSynthesizer)}} 的呼叫都會佔住調用者的線程
（COM 同步呼叫），直接在異步方法裡呼叫會讓「多個語言並行合成」變成假並行。

*取消是本引擎的老大難（2026-11-16 審查 BUG-5 的修復）*：{{nameof(SpeechSynthesizer.Speak)}}
一旦開唸就**不可中斷**——把取消令牌傳給 `Task.Run` 只能攔下「還沒開始」的，
開唸之後全域 Esc 對它無效，只能等它唸完。故這裡改用
{{nameof(SpeechSynthesizer.SpeakAsync)}} ＋ 完成事件等通知；取消令牌一取消就
{{nameof(SpeechSynthesizer.SpeakAsyncCancel)}} 掐掉正在唸的那段，被掐斷的半個 wav
當場刪除（殘檔會被快取判定誤當完整檔案，見 {{nameof(TtsCacheFile.TryDel)}}）。
""")]
public class SystemSpeechTts(
	OptTts Opt,
	ILogger<SystemSpeechTts> Log
):ITtsEngine{

	[Doc("配置裡用來指這個引擎的名字。")]
	public const str EngineName = "SystemSpeech";

	[Doc("配置裡指這個引擎用的名字。")]
	public str Name => EngineName;

	[Doc("探測結果的鎖；探測只做一次。")]
	readonly Lock ProbeGate = new();

	[Doc("null＝還沒探測過；true／false＝探測結果。")]
	bool? Probed;

	[Doc($$"""
	本機上 SAPI 現在能不能用。

	*為何要探測*：SAPI 是 COM。NativeAOT 下它的 COM 包裝類（`SpObjectTokenCategory` 等）
	建構子無效（ILC 會明確報告），所以**同一份代碼在 JIT 下可用、在 AOT 下必定抛**。
	不先問一句，每個語言每一句都要白抛一次、日誌被灌滿。故第一次詢問時真的建一個
	{{nameof(SpeechSynthesizer)}} 試試，結果快取；失敗只記一條警告（含原因）。

	*為何不直接把這個引擎刪掉*：JIT 版（日常用 `dotnet run`）仍需要「網絡斷了就讀本機語音」
	這條退路。探測讓同一份代碼在兩種運行方式下都表現合理。
	""")]
	public bool IsAvailable{
		get{
			lock(ProbeGate){
				if(Probed is null){
					Probed = Probe();
				}
				return Probed.Value;
			}
		}
	}

	[Doc("真的建一次合成器；成功回 true，失敗記一條警告回 false。")]
	bool Probe(){
		try{
			using var Synth = new SpeechSynthesizer();
			_ = Synth.GetInstalledVoices().Count;
			return true;
		}
		catch(Exception Ex){
			// AOT 下典型是 TypeLoadException／InvalidProgramException 或 COM 註冊失敗。
			Log.LogWarning(Ex, "system speech engine is unavailable in this runtime; it will be skipped");
			return false;
		}
	}

	[Doc($$"""
	合成 wav 並回傳路徑；檔案已存在就不重合成。
	#Prm[{{nameof(Req)}}][合成請求。]
	#Rtn[wav 檔的絕對路徑。]
	#Throw[{{nameof(TypedErr)}}][本機 SAPI 不可用（{{nameof(KeysErr.Tts.SapiUnavailable)}}）時。]
	""")]
	public async Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		if(!IsAvailable){
			throw KeysErr.Tts.SapiUnavailable.ToErr();
		}
		// Text 在 DTO 上非空（默認 ""），驗的是「有沒有給東西」，不必 `?.`（2026-11-16 審查清掉）。
		var Text = Req.Text.Trim();
		if(Text.Length == 0){
			throw KeysErr.Common.ArgErr__.ToErr(nameof(Req.Text));
		}
		var Lang = LangTag.Ensure(Req.Language);

		Directory.CreateDirectory(Opt.OutputDir);
		// 命名規則（含語言段）見 TtsCacheFile——兩個引擎必須共用同一份。
		var WavPath = TtsCacheFile.MkPath(Opt.OutputDir, "system_speech", Lang, Text, ".wav");
		if(File.Exists(WavPath)){
			return WavPath;
		}

		try{
			// CancellationToken.None：工作項本身一定要進去（進去之後才有「可掐斷」可言），
			// 真正的取消發生在裡面的 SpeakAsyncCancel。
			await Task.Run(() => GenerateWav(Text, Lang, WavPath, Ct), CancellationToken.None);
		}
		catch(OperationCanceledException){
			// 被掐斷的 wav 是半個檔案：不刪就會被下一輪的「檔案存在＝已快取」誤播。
			TtsCacheFile.TryDel(WavPath);
			throw;
		}
		Log.LogDebug("system speech audio generated. file={File}", WavPath);
		return WavPath;
	}

	[Doc($$"""
	同步合成：選音色 → 指定輸出檔 → 朗讀（可被掐斷）→ 解除輸出綁定。
	#Prm[{{nameof(Text)}}][要唸的文本。]
	#Prm[{{nameof(Language)}}][語言代碼，用來挑音色。]
	#Prm[{{nameof(WavPath)}}][輸出檔路徑。]
	#Prm[{{nameof(Ct)}}][取消令牌：取消時掐斷朗讀並拋 {{nameof(OperationCanceledException)}}。]
	#Throw[{{nameof(TypedErr)}}][本機沒有該語言的語音（{{nameof(KeysErr.Tts.NoVoiceForLang__)}}）時。]
	#Throw[{{nameof(OperationCanceledException)}}][{{nameof(Ct)}} 被取消（朗讀已掐斷）時。]
	""")]
	static void GenerateWav(str Text, str Language, str WavPath, CT Ct){
		using var Synth = new SpeechSynthesizer();
		SelectVoice(Synth, Language);
		Synth.Rate = 0;
		Synth.Volume = 100;
		Synth.SetOutputToWaveFile(WavPath);
		// RunContinuationsAsynchronously：完成事件來自 SAPI 的工作線程，
		// 不能讓它的延續（本方法的收尾）反向佔住那一條。
		var Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Synth.SpeakCompleted += (_, _) => Done.TrySetResult();
		try{
			var Prompt = Synth.SpeakAsync(Text);
			// 取消＝掐掉這一段：SpeakAsyncCancel 會讓 SpeakCompleted 立即觸發，等待因此有界。
			using var Reg = Ct.Register(() => Synth.SpeakAsyncCancel(Prompt));
			Done.Task.GetAwaiter().GetResult();
			Ct.ThrowIfCancellationRequested();
		}
		finally{
			// 不解除綁定就 Dispose 會留下半個 wav 檔，下一輪會被當成「已快取」。
			Synth.SetOutputToNull();
		}
	}

	[Doc($$"""
	挑一個能唸該語言的音色；挑不到直接拋。
	*為何先試 {{nameof(SpeechSynthesizer.SelectVoiceByHints)}} 再手動掃*：前者要具體文化，
	而使用者裝的音色未必對得上地區（`ja-JP` vs `ja`），故再按兩字母語言名掃一遍。
	""")]
	static void SelectVoice(SpeechSynthesizer Synth, str Language){
		var Culture = LangTag.TryCulture(Language);
		if(Culture is not null){
			try{
				Synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, Culture);
				return;
			}
			catch(ArgumentException){
				// 沒有符合文化的音色：落到下面的手動掃描。
			}
		}

		foreach(var Voice in Synth.GetInstalledVoices()){
			var Info = Voice.VoiceInfo;
			if(Culture is not null
				&&(str.Equals(Info.Culture.Name, Culture.Name, StringComparison.OrdinalIgnoreCase)
					|| str.Equals(Info.Culture.TwoLetterISOLanguageName, Culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))){
				Synth.SelectVoice(Info.Name);
				return;
			}
		}

		throw KeysErr.Tts.NoVoiceForLang__.ToErr(Language);
	}
}
