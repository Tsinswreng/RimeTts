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

if(string.IsNullOrWhiteSpace(cfg.Tts.PythonDllPath)){
	Console.Error.WriteLine("config error: tts.pythonDllPath must be set.");
	return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opt => {
	opt.SingleLine = true;
	opt.TimestampFormat = "HH:mm:ss ";
});

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
		opt.SystemPrompt = cfg.Translator.SystemPrompt;
	},
	SetTtsOpt: opt => {
		opt.PythonDllPath = cfg.Tts.PythonDllPath;
		opt.OutputDir = string.IsNullOrWhiteSpace(cfg.Tts.OutputDir)
			? Path.Combine(AppContext.BaseDirectory, "tts-output")
			: cfg.Tts.OutputDir;
	}
);

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
log.LogInformation("starting. signal={SignalFile}; model={Model}", cfg.FileInteractor.SignalFile, cfg.Translator.Model);
await host.RunAsync();
