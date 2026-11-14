namespace RimeTts.Domains.Tts;

using System.Globalization;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using Microsoft.Extensions.Logging;
using RimeTts.Infra;

[Doc($$"""
Windows 自帶的離線 TTS（SAPI）：把文本合成成 wav 檔。

*為何留著它*：gTTS 依賴網絡，而這個工具的用途是「邊打字邊聽」——網絡抽風時不能整個啞掉。
它是兜底，音色機械但一定在。

*為何每次合成都要新開一個 {{nameof(SpeechSynthesizer)}}*：SAPI 的合成器物件綁著輸出目標
（檔案／設備），而且不是線程安全的；共用一份會出現「兩個語言同時合成時輸出交錯」。
新開一個的成本很低（它就是個 COM 包裝）。

*為何要 Task.Run*：{{nameof(SpeechSynthesizer.Speak)}} 是同步阻塞的 COM 呼叫。
直接在異步方法裡呼叫會佔住調用者的線程，讓「多個語言並行合成」變成假並行。
""")]
public class SystemSpeechTts(
	OptTts Opt,
	ILogger<SystemSpeechTts> Log
):ITtsEngine{

	[Doc("配置裡用來指這個引擎的名字。")]
	public const str EngineName = "SystemSpeech";

	[Doc("配置裡指這個引擎用的名字。")]
	public str Name => EngineName;

	[Doc($$"""
	合成 wav 並回傳路徑；檔案已存在就不重合成。
	#Prm[{{nameof(Req)}}][合成請求。]
	#Rtn[wav 檔的絕對路徑。]
	""")]
	public async Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		var Text = Req.Text?.Trim() ?? "";
		if(Text.Length == 0){
			throw KeysErr.Common.ArgErr__.ToErr(nameof(Req.Text));
		}
		var Lang = LangTag.Normalize(Req.Language) is { Length: > 0 } L ? L : "en";

		Directory.CreateDirectory(Opt.OutputDir);
		// 檔名含語言：同文本不同語言是不同的音頻。
		var WavPath = Path.Combine(Opt.OutputDir, $"system_speech_{HashHex(Lang + "\n" + Text)}.wav");
		if(File.Exists(WavPath)){
			return WavPath;
		}

		await Task.Run(() => GenerateWav(Text, Lang, WavPath), Ct);
		Log.LogDebug("system speech audio generated. file={File}", WavPath);
		return WavPath;
	}

	[Doc($$"""
	同步合成：選音色 → 指定輸出檔 → 朗讀 → 解除輸出綁定。
	#Prm[{{nameof(Text)}}][要唸的文本。]
	#Prm[{{nameof(Language)}}][語言代碼，用來挑音色。]
	#Prm[{{nameof(WavPath)}}][輸出檔路徑。]
	#Throw[{{nameof(TypedErr)}}][本機沒有該語言的語音（{{nameof(KeysErr.Tts.NoVoiceForLang__)}}）時。]
	""")]
	static void GenerateWav(str Text, str Language, str WavPath){
		using var Synth = new SpeechSynthesizer();
		SelectVoice(Synth, Language);
		Synth.Rate = 0;
		Synth.Volume = 100;
		Synth.SetOutputToWaveFile(WavPath);
		try{
			Synth.Speak(Text);
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

	[Doc("文本的 SHA-256 十六進制串；用作快取檔名。")]
	static str HashHex(str Text){
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();
	}
}
