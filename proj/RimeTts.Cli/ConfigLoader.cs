using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RimeTts.Cli;

public static class ConfigLoader{
	private const str FileName = "rimetts.yaml";

	public static AppConfig Load(){
		var dir = Path.GetDirectoryName(Environment.ProcessPath)
			?? AppContext.BaseDirectory;
		var path = Path.Combine(dir, FileName);

		if(!File.Exists(path)){
			var sample = BuildSample();
			File.WriteAllText(path, sample);
			throw new FileNotFoundException(
				$"config file not found, a sample has been generated at: {path}\n"
				+ "fill in the required fields and restart.",
				path
			);
		}

		var yaml = File.ReadAllText(path);
		var deserializer = new DeserializerBuilder()
			.WithNamingConvention(NullNamingConvention.Instance)
			.IgnoreUnmatchedProperties()
			.Build();

		return deserializer.Deserialize<AppConfig>(yaml) ?? new AppConfig();
	}

	private static str BuildSample(){
		return """
# RimeTts 配置文件
# 配置文件必须与 EXE 在同一目录下

FileInteractor:
  # Lua 侧写入内容的 JSON 文件路径
	ContentFile: "C:\\tmp\\rimetts_content.json"
  # Lua 侧触发信号的文件路径（C# 监听此文件变化）
	SignalFile: "C:\\tmp\\rimetts_signal"

SentenceSeg:
  # 最后一次上屏后多少毫秒认为一个句子结束
	NoCommitGapMs: 5000

Translator:
  # LLM API Key
	ApiKey: ""
  # API 地址（兼容 OpenAI 协议的接口均可）
	BaseUrl: "https://api.openai.com/v1/chat/completions"
  # 模型名（推荐使用响应速度快的，如 gpt-4o-mini）
	Model: "gpt-4o-mini"
  # 请求超时秒数
	TimeoutSec: 20
  # 默认翻译系统提示词（当语言条目未显式设置时使用）
	DefaultSystemPrompt: "You are a fast translator. Translate source text to target language only. Return only translation text."

Tts:
	# TTS 引擎优先级，越靠前优先级越高。默认先 gTTS 再 SystemSpeech
	Engines: ["gTTS", "SystemSpeech"]
	# 生成的音频文件输出目录（留空则使用 EXE 目录下 tts-output）
	OutputDir: ""

LanguagePipeline:
	# 語言順序即播放順序，按列表從上到下依次翻譯並朗讀
	Languages:
		- Language: "en"
			SystemPrompt: "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text."
			TtsEngines: ["gTTS", "SystemSpeech"]
		- Language: "ja"
			SystemPrompt: "あなたは高速な翻訳者です。中国語を自然で簡潔な日本語に翻訳してください。翻訳結果の本文のみを返してください。"
			TtsEngines: ["gTTS", "SystemSpeech"]
""";
	}
}
