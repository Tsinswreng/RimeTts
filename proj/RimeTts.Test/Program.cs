using System.Globalization;
using System.Speech.Synthesis;
using Python.Runtime;

internal static class Program
{
	private static readonly string SampleText = "Hello, this is a test for English text to speech from C sharp and Python.";
	private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "tts-output");

	// 你说已经写好了 python.dll 路径，这里直接用你给的路径。
	private static readonly string PythonDllPath = @"d:\ENV\python312\python312.dll";
	private static readonly string? PythonExePath = ResolvePythonExePath();

	private static void Main()
	{
		Directory.CreateDirectory(OutputDir);

		Console.WriteLine("=== TTS Demo (C# + Python.NET) ===");
		Console.WriteLine($"Output: {OutputDir}");
		Console.WriteLine();

		RunSafely("[1] C# System.Speech (离线)", TestSystemSpeech);

		if (InitializePython())
		{
			RunSafely("[2] Python pyttsx3 (离线, 通过 python.net)", () =>
			{
				EnsurePythonModule("pyttsx3");
				TestPyttsx3ViaPythonNet();
			});

			RunSafely("[3] Python gTTS (联网, 通过 python.net)", () =>
			{
				EnsurePythonModule("gtts");
				TestGttsViaPythonNet();
			});

			RunSafely("[4] Python Coqui TTS (可选, 通过 python.net)", () =>
			{
				EnsurePythonModule("TTS");
				TestCoquiViaPythonNet();
			});

			ShutdownPythonSafely();
		}

		Console.WriteLine();
		Console.WriteLine("完成。请检查输出文件并主观对比效果。");
	}

	private static void RunSafely(string title, Action action)
	{
		Console.WriteLine(title);
		try
		{
			action();
			Console.WriteLine("  ✅ 成功");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"  ⚠️ 失败: {ex.GetType().Name} - {ex.Message}");
		}

		Console.WriteLine();
	}

	private static void TestSystemSpeech()
	{
		var wavPath = Path.Combine(OutputDir, "csharp_system_speech.wav");

		using var synth = new SpeechSynthesizer();
		synth.Rate = 0;
		synth.Volume = 100;

		try
		{
			synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new CultureInfo("en-US"));
		}
		catch
		{
			// 如果本机无匹配语音，保持默认语音。
		}

		synth.SetOutputToWaveFile(wavPath);
		synth.Speak(SampleText);
		synth.SetOutputToDefaultAudioDevice();

		Console.WriteLine($"  已生成: {wavPath}");
	}

	private static bool InitializePython()
	{
		if (!File.Exists(PythonDllPath))
		{
			Console.WriteLine($"Python 初始化失败: 找不到 DLL -> {PythonDllPath}");
			return false;
		}

		var pythonHome = Path.GetDirectoryName(PythonDllPath);
		if (!string.IsNullOrWhiteSpace(pythonHome))
		{
			Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
			var oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
			if (!oldPath.Contains(pythonHome, StringComparison.OrdinalIgnoreCase))
			{
				Environment.SetEnvironmentVariable("PATH", pythonHome + Path.PathSeparator + oldPath);
			}
		}

		Runtime.PythonDLL = PythonDllPath;
		PythonEngine.Initialize();
		PythonEngine.BeginAllowThreads();
		Console.WriteLine($"Python 初始化完成: {PythonDllPath}");
		if (!string.IsNullOrWhiteSpace(PythonExePath) && File.Exists(PythonExePath))
		{
			Console.WriteLine($"Python 可执行文件: {PythonExePath}");
		}
		Console.WriteLine("提示: 如缺包，请安装 pyttsx3 / gTTS / TTS。");
		Console.WriteLine();
		return true;
	}

	private static void ShutdownPythonSafely()
	{
		if (!PythonEngine.IsInitialized)
		{
			return;
		}

		// pythonnet 在 .NET 9/10 上调用 Shutdown 可能触发 BinaryFormatter 相关异常。
		// 控制台程序即将退出时可安全跳过，避免崩溃。
		Console.WriteLine("已跳过 PythonEngine.Shutdown()（避免 .NET 10 下已知兼容性问题）。");
	}

	private static void EnsurePythonModule(string moduleName)
	{
		using var gil = Py.GIL();
		using var importlib = Py.Import("importlib.util");
		using var findSpec = importlib.GetAttr("find_spec");
		using var spec = findSpec.Invoke(new PyString(moduleName));

		if (spec.IsNone())
		{
			var installHint = File.Exists(PythonExePath)
				? $"\n  安装示例: {PythonExePath} -m pip install {moduleName}"
				: $"\n  安装示例: python -m pip install {moduleName}";

			throw new InvalidOperationException($"缺少 Python 包: {moduleName}.{installHint}");
		}
	}

	private static void TestPyttsx3ViaPythonNet()
	{
		var wavPath = Path.Combine(OutputDir, "python_pyttsx3.wav");
		string selectedVoice = "default";

        RunOnStaThread(() =>
        {
            using var gil = Py.GIL();
            using var module = PyModule.FromString("pyttsx3_runner", """
import pyttsx3

def run(text, wav_path):
	engine = pyttsx3.init()
	voices = engine.getProperty('voices')

	selected = None
	for v in voices:
		name = (getattr(v, 'name', '') or '').lower()
		vid = (getattr(v, 'id', '') or '').lower()
		if 'english' in name or 'en' in vid:
			selected = v.id
			break

	if selected:
		engine.setProperty('voice', selected)

	engine.setProperty('rate', 160)
	engine.setProperty('volume', 0.9)
	engine.save_to_file(text, wav_path)
	engine.runAndWait()
	return selected or 'default'
""");

			using var selectedVoicePy = module.InvokeMethod("run", new PyTuple(new PyObject[]
			{
				new PyString(SampleText),
				new PyString(wavPath)
			}));

			selectedVoice = selectedVoicePy.ToString() ?? "default";
        });

		Console.WriteLine($"  pyttsx3 voice: {selectedVoice}");
		Console.WriteLine($"  已生成: {wavPath}");
	}

	private static void RunOnStaThread(Action action)
	{
		Exception? captured = null;
		var thread = new Thread(() =>
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				captured = ex;
			}
		});

		if (OperatingSystem.IsWindows())
		{
			thread.SetApartmentState(ApartmentState.STA);
		}
		else
		{
			throw new PlatformNotSupportedException("pyttsx3 on this demo requires Windows STA thread.");
		}
		thread.Start();
		thread.Join();

		if (captured is not null)
		{
			throw captured;
		}
	}

	private static string? ResolvePythonExePath()
	{
		var dir = Path.GetDirectoryName(PythonDllPath);
		if (string.IsNullOrWhiteSpace(dir))
		{
			return null;
		}

		var pythonExe = Path.Combine(dir, "python.exe");
		if (File.Exists(pythonExe))
		{
			return pythonExe;
		}

		var dllName = Path.GetFileNameWithoutExtension(PythonDllPath);
		var altExe = Path.Combine(dir, dllName + ".exe");
		return File.Exists(altExe) ? altExe : null;
	}

	private static void TestGttsViaPythonNet()
	{
		var mp3Path = Path.Combine(OutputDir, "python_gtts.mp3");

		using var gil = Py.GIL();
		using var module = PyModule.FromString("gtts_runner", """
from gtts import gTTS

def run(text, mp3_path):
	tts = gTTS(text=text, lang='en')
	tts.save(mp3_path)
	return mp3_path
""");

		_ = module.InvokeMethod("run", new PyTuple(new PyObject[]
		{
			new PyString(SampleText),
			new PyString(mp3Path)
		}));

		Console.WriteLine($"  已生成: {mp3Path}");
	}

	private static void TestCoquiViaPythonNet()
	{
		var wavPath = Path.Combine(OutputDir, "python_coqui_tts.wav");

		using var gil = Py.GIL();
		using var module = PyModule.FromString("coqui_runner", """
from TTS.api import TTS

def run(text, wav_path):
	tts = TTS(model_name='tts_models/en/ljspeech/tacotron2-DDC', progress_bar=False, gpu=False)
	tts.tts_to_file(text=text, file_path=wav_path)
	return wav_path
""");

		_ = module.InvokeMethod("run", new PyTuple(new PyObject[]
		{
			new PyString(SampleText),
			new PyString(wavPath)
		}));

		Console.WriteLine($"  已生成: {wavPath}");
	}
}


