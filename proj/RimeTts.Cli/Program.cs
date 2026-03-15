using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RimeTts;

if(!OperatingSystem.IsWindows()){
	Console.Error.WriteLine("RimeTts.Cli currently supports Windows only.");
	return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opt => {
	opt.SingleLine = true;
	opt.TimestampFormat = "HH:mm:ss ";
});

var signalFile = GetRequiredEnv("RIMETTS_SIGNAL_FILE");
var contentFile = GetRequiredEnv("RIMETTS_CONTENT_FILE");
var pythonDll = GetRequiredEnv("RIMETTS_PYTHON_DLL");
var llmApiKey = GetRequiredEnv("RIMETTS_LLM_API_KEY");

builder.Services.AddRimeTts(
	SetFileOpt: opt => {
		opt.SignalFile = signalFile;
		opt.ContentFile = contentFile;
	},
	SetSegOpt: opt => {
		opt.NoCommitGapMs = GetEnvI64("RIMETTS_NO_COMMIT_GAP_MS", 5000);
	},
	SetTranslatorOpt: opt => {
		opt.ApiKey = llmApiKey;
		opt.BaseUrl = GetEnvOrDefault("RIMETTS_LLM_BASE_URL", "https://api.openai.com/v1/chat/completions");
		opt.Model = GetEnvOrDefault("RIMETTS_LLM_MODEL", "gpt-4o-mini");
		opt.TimeoutSec = (int)GetEnvI64("RIMETTS_LLM_TIMEOUT_SEC", 20);
	},
	SetTtsOpt: opt => {
		opt.PythonDllPath = pythonDll;
		opt.OutputDir = GetEnvOrDefault("RIMETTS_AUDIO_OUTPUT_DIR", Path.Combine(AppContext.BaseDirectory, "tts-output"));
	}
);

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
log.LogInformation("starting RimeTts worker. translation=LLM zh->en; tts=gTTS; windows-only=true");
await host.RunAsync();

static str GetRequiredEnv(str key){
	var value = Environment.GetEnvironmentVariable(key);
	if(string.IsNullOrWhiteSpace(value)){
		throw new InvalidOperationException($"missing env: {key}");
	}
	return value;
}

static str GetEnvOrDefault(str key, str defaultValue){
	var value = Environment.GetEnvironmentVariable(key);
	return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
}

static i64 GetEnvI64(str key, i64 defaultValue){
	var value = Environment.GetEnvironmentVariable(key);
	if(string.IsNullOrWhiteSpace(value)){
		return defaultValue;
	}
	return i64.TryParse(value, out var n) ? n : defaultValue;
}
