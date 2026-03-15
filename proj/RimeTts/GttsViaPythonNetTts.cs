using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Python.Runtime;

namespace RimeTts;

public sealed class GttsViaPythonNetTts(
	OptTts Opt,
	ILogger<GttsViaPythonNetTts> Log
):ITts{
	private readonly SemaphoreSlim _playLock = new(1, 1);
	private readonly SemaphoreSlim _pythonInitLock = new(1, 1);
	private volatile bool _pythonInitialized;

	public async Task<IPlayState> GenEtPlay(IReqGenEtPlay Req, CT Ct){
		if(!OperatingSystem.IsWindows()){
			throw new PlatformNotSupportedException("gTTS playback is implemented for Windows only");
		}

		var text = Req.Text?.Trim() ?? "";
		if(text.Length == 0){
			throw new ArgumentException("tts text is empty", nameof(Req));
		}

		Directory.CreateDirectory(Opt.OutputDir);
		await EnsurePythonInitialized(Ct);

		var filePath = Path.Combine(Opt.OutputDir, $"gtts_{HashHex(text)}.mp3");
		if(!File.Exists(filePath)){
			GenerateWithGtts(text, Req.Language?.Trim() is { Length: > 0 } lang ? lang : "en", filePath);
		}

		await _playLock.WaitAsync(Ct);
		var startAt = DateTimeOffset.UtcNow;
		try{
			await PlayMp3WindowsAsync(filePath, Ct);
		}
		finally{
			_playLock.Release();
		}
		var endAt = DateTimeOffset.UtcNow;

		Log.LogInformation("tts played. file={AudioFile}; durationMs={Duration}", filePath, (endAt - startAt).TotalMilliseconds);
		return new PlayState{
			AudioFile = filePath,
			StartedAtUtc = startAt,
			EndedAtUtc = endAt,
		};
	}

	private async Task EnsurePythonInitialized(CT Ct){
		if(_pythonInitialized){
			return;
		}

		await _pythonInitLock.WaitAsync(Ct);
		try{
			if(_pythonInitialized){
				return;
			}

			if(string.IsNullOrWhiteSpace(Opt.PythonDllPath) || !File.Exists(Opt.PythonDllPath)){
				throw new InvalidOperationException("python dll path is invalid. set RIMETTS_PYTHON_DLL");
			}

			var home = Path.GetDirectoryName(Opt.PythonDllPath);
			if(!string.IsNullOrWhiteSpace(home)){
				Environment.SetEnvironmentVariable("PYTHONHOME", home);
				var oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
				if(!oldPath.Contains(home, StringComparison.OrdinalIgnoreCase)){
					Environment.SetEnvironmentVariable("PATH", home + Path.PathSeparator + oldPath);
				}
			}

			Runtime.PythonDLL = Opt.PythonDllPath;
			PythonEngine.Initialize();
			PythonEngine.BeginAllowThreads();
			_pythonInitialized = true;
			Log.LogInformation("python initialized: {PythonDll}", Opt.PythonDllPath);
		}
		finally{
			_pythonInitLock.Release();
		}
	}

	private static str HashHex(str text){
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static void GenerateWithGtts(str text, str lang, str outMp3Path){
		using var gil = Py.GIL();
		using var module = PyModule.FromString("gtts_runner", """
from gtts import gTTS

def run(text, lang, mp3_path):
	tts = gTTS(text=text, lang=lang)
	tts.save(mp3_path)
	return mp3_path
""");

		_ = module.InvokeMethod("run", new PyTuple(new PyObject[]{
			new PyString(text),
			new PyString(lang),
			new PyString(outMp3Path),
		}));
	}

	private static Task PlayMp3WindowsAsync(str mp3Path, CT Ct){
		if(!OperatingSystem.IsWindows()){
			throw new PlatformNotSupportedException("windows only");
		}

		return Task.Run(() => {
			Ct.ThrowIfCancellationRequested();
			using var stream = File.OpenRead(mp3Path);
			using WaveStream reader = new Mp3FileReader(stream);
			using var waveOut = new WaveOutEvent();
			waveOut.Init(reader);
			waveOut.Play();

			while(waveOut.PlaybackState == PlaybackState.Playing){
				Ct.ThrowIfCancellationRequested();
				Thread.Sleep(100);
			}
		}, Ct);
	}
}
