using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RimeTts;
using RimeTts.Cli;

if(!OperatingSystem.IsWindows()){
	Console.Error.WriteLine("RimeTts.Cli currently supports Windows only.");
	return;
}

AppConfig cfg;
try{
	cfg = ConfigLoader.Load();
}
catch(FileNotFoundException ex){
	Console.Error.WriteLine(ex.Message);
	return;
}

if(string.IsNullOrWhiteSpace(cfg.FileInteractor.ContentFile)
	|| string.IsNullOrWhiteSpace(cfg.FileInteractor.SignalFile)){
	Console.Error.WriteLine("config error: fileInteractor.contentFile / signalFile must be set.");
	return;
}

if(string.IsNullOrWhiteSpace(cfg.Translator.ApiKey)){
	Console.Error.WriteLine("config error: translator.apiKey must be set.");
	return;
}

if(cfg.LanguagePipeline.Languages is not { Count: > 0 }){
	Console.Error.WriteLine("config error: languagePipeline.languages must contain at least one language profile.");
	return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);  // 加这一行
builder.Services.AddSingleton<ILoggerProvider, RimeTtsConsoleLoggerProvider>();

builder.Services.AddRimeTts(
	SetFileOpt: opt => {
		opt.SignalFile = cfg.FileInteractor.SignalFile;
		opt.ContentFile = cfg.FileInteractor.ContentFile;
	},
	SetSegOpt: opt => {
		opt.NoCommitGapMs = cfg.SentenceSeg.NoCommitGapMs;
	},
	SetTranslatorOpt: opt => {
		opt.ApiKey = cfg.Translator.ApiKey;
		opt.BaseUrl = cfg.Translator.BaseUrl;
		opt.Model = cfg.Translator.Model;
		opt.TimeoutSec = cfg.Translator.TimeoutSec;
		opt.DefaultSystemPrompt = string.IsNullOrWhiteSpace(cfg.Translator.DefaultSystemPrompt)
			? opt.DefaultSystemPrompt
			: cfg.Translator.DefaultSystemPrompt;
	},
	SetTtsOpt: opt => {
		opt.OutputDir = string.IsNullOrWhiteSpace(cfg.Tts.OutputDir)
			? Path.Combine(AppContext.BaseDirectory, "tts-output")
			: cfg.Tts.OutputDir;
		opt.Engines = cfg.Tts.Engines?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
			?? new List<str>{ "gTTS", "SystemSpeech" };
	},
	SetLanguagePipelineOpt: opt => {
		opt.Languages = cfg.LanguagePipeline.Languages
			.Where(x => x is not null)
			.Select(x => (ILanguageProfile)new LanguageProfile{
				Language = (x.Language ?? "").Trim(),
				SystemPrompt = x.SystemPrompt ?? "",
				TtsEngines = x.TtsEngines?.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToList()
					?? new List<str>(),
			})
			.Where(x => !string.IsNullOrWhiteSpace(x.Language))
			.ToList();

		if(opt.Languages.Count == 0){
			opt.Languages = new List<ILanguageProfile>{
				new LanguageProfile{
					Language = "en",
					SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: en
Translation: Translated Text
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
					TtsEngines = new(){ "gTTS", "SystemSpeech" },
				},
				new LanguageProfile{
					Language = "ja",
					SystemPrompt = """
あなたは翻訳者です。
YAML のみを返してください:
UserInputLang: zh
TargetLang: ja
Translation: 翻訳文
翻訳できない場合は Translation を null にしてください。
Markdown のコードフェンスは付けないでください。
""",
					TtsEngines = new(){ "gTTS", "SystemSpeech" },
				},
				new LanguageProfile{
					Language = "es",
					SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: es
Translation: Texto traducido
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
					TtsEngines = new(){ "gTTS", "SystemSpeech" },
				},
				new LanguageProfile{
					Language = "it",
					SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: it
Translation: Testo tradotto
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
					TtsEngines = new(){ "gTTS", "SystemSpeech" },
				},
				new LanguageProfile{
					Language = "fr",
					SystemPrompt = """
You are a translator.
Return YAML only:
UserInputLang: zh
TargetLang: fr
Translation: Texte traduit
If you cannot translate, set Translation to null.
Do not wrap in markdown fences.
""",
					TtsEngines = new(){ "gTTS", "SystemSpeech" },
				}
			};
		}
	}
);

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
log.LogInformation("starting. signal={SignalFile}; model={Model}", cfg.FileInteractor.SignalFile, cfg.Translator.Model);

await host.RunAsync();
