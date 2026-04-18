using System.IO;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using System.Globalization;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace RimeTts;

public sealed class SystemSpeechTts(
	OptTts Opt,
	ILogger<SystemSpeechTts> Log
):ITts{
	private readonly SemaphoreSlim _playLock = new(1, 1);

	public async Task<IPlayState> GenEtPlay(IReqGenEtPlay Req, CT Ct){
		if(!OperatingSystem.IsWindows()){
			throw new PlatformNotSupportedException("System.Speech is implemented for Windows only");
		}

		var text = Req.Text?.Trim() ?? "";
		var lang = Req.Language?.Trim() ?? "";
		if(text.Length == 0){
			throw new ArgumentException("tts text is empty", nameof(Req));
		}

		Directory.CreateDirectory(Opt.OutputDir);
		var wavPath = Path.Combine(Opt.OutputDir, $"system_speech_{HashHex($"{lang}\n{text}")}.wav");
		if(!File.Exists(wavPath)){
			GenerateWav(text, lang, wavPath);
		}

		await _playLock.WaitAsync(Ct);
		var startAt = DateTimeOffset.UtcNow;
		try{
			await PlayWavWindowsAsync(wavPath, Ct);
		}
		finally{
			_playLock.Release();
		}
		var endAt = DateTimeOffset.UtcNow;

		//Log.LogInformation("system speech played. file={AudioFile}; durationMs={Duration}", wavPath, (endAt - startAt).TotalMilliseconds);
		return new PlayState{
			AudioFile = wavPath,
			StartedAtUtc = startAt,
			EndedAtUtc = endAt,
		};
	}

	private static str HashHex(str text){
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static void GenerateWav(str text, str language, str wavPath){
		using var synth = new SpeechSynthesizer();
		SelectVoiceByLanguageOrThrow(synth, language);
		synth.Rate = 0;
		synth.Volume = 100;
		synth.SetOutputToWaveFile(wavPath);
		synth.Speak(text);
		synth.SetOutputToNull();
	}

	private static void SelectVoiceByLanguageOrThrow(SpeechSynthesizer synth, str language){
		if(string.IsNullOrWhiteSpace(language)){
			return;
		}

		var culture = ResolveCulture(language);
		if(culture is null){
			return;
		}

		try{
			synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, culture);
			return;
		}
		catch{
			// fallback to manual scan below
		}

		foreach(var voice in synth.GetInstalledVoices()){
			var vc = voice.VoiceInfo.Culture;
			if(string.Equals(vc.Name, culture.Name, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(vc.TwoLetterISOLanguageName, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)){
				synth.SelectVoice(voice.VoiceInfo.Name);
				return;
			}
		}

		throw new InvalidOperationException($"No installed System.Speech voice for language '{language}' ({culture.Name}).");
	}

	private static CultureInfo? ResolveCulture(str language){
		var lang = language.Trim();
		if(lang.Length == 0){
			return null;
		}

		try{
			if(string.Equals(lang, "ja", StringComparison.OrdinalIgnoreCase)){
				return CultureInfo.GetCultureInfo("ja-JP");
			}
			if(string.Equals(lang, "jp", StringComparison.OrdinalIgnoreCase)){
				return CultureInfo.GetCultureInfo("ja-JP");
			}
			if(string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)){
				return CultureInfo.GetCultureInfo("en-US");
			}
			if(string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase)){
				return CultureInfo.GetCultureInfo("zh-CN");
			}

			return CultureInfo.GetCultureInfo(lang);
		}
		catch(CultureNotFoundException){
			return null;
		}
	}

	private static Task PlayWavWindowsAsync(str wavPath, CT Ct){
		return Task.Run(() => {
			Ct.ThrowIfCancellationRequested();
			using var stream = File.OpenRead(wavPath);
			using WaveStream reader = new WaveFileReader(stream);
			using var waveOut = new WaveOutEvent();
			waveOut.Init(reader);
			waveOut.Play();

			while(waveOut.PlaybackState == PlaybackState.Playing){
				Ct.ThrowIfCancellationRequested();
				Thread.Sleep(100);
			}
		}, Ct);
	}

	public Task<str> GenerateAudio(IReqGenEtPlay Req, CT Ct){
		if(!OperatingSystem.IsWindows()){
			throw new PlatformNotSupportedException("System.Speech is implemented for Windows only");
		}

		var text = Req.Text?.Trim() ?? "";
		var lang = Req.Language?.Trim() ?? "";
		if(text.Length == 0){
			throw new ArgumentException("tts text is empty", nameof(Req));
		}

		Directory.CreateDirectory(Opt.OutputDir);
		var wavPath = Path.Combine(Opt.OutputDir, $"system_speech_{HashHex($"{lang}\n{text}")}.wav");
		if(!File.Exists(wavPath)){
			GenerateWav(text, lang, wavPath);
		}
		Log.LogDebug("system speech audio generated. file={File}", wavPath);
		return Task.FromResult(wavPath);
	}

	public async Task PlayAudio(str AudioFile, CT Ct){
		if(!OperatingSystem.IsWindows()){
			throw new PlatformNotSupportedException("windows only");
		}

		if(!File.Exists(AudioFile)){
			throw new FileNotFoundException("audio file not found", AudioFile);
		}

		await _playLock.WaitAsync(Ct);
		try{
			await PlayWavWindowsAsync(AudioFile, Ct);
		}
		finally{
			_playLock.Release();
		}
		//Log.LogInformation("audio played. file={AudioFile}", AudioFile);
	}
}
