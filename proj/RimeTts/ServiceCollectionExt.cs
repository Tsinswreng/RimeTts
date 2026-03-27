using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RimeTts;

public static class ServiceCollectionExt{
	public static IServiceCollection AddRimeTts(this IServiceCollection Services, Action<OptFileInteractor> SetFileOpt, Action<OptSentenceSeg>? SetSegOpt = null, Action<OptTranslator>? SetTranslatorOpt = null, Action<OptTts>? SetTtsOpt = null, Action<OptLanguagePipeline>? SetLanguagePipelineOpt = null){
		var fileOpt = new OptFileInteractor();
		SetFileOpt(fileOpt);
		if(string.IsNullOrWhiteSpace(fileOpt.ContentFile) || string.IsNullOrWhiteSpace(fileOpt.SignalFile)){
			throw new InvalidOperationException("file interactor options are required");
		}

		var segOpt = new OptSentenceSeg();
		SetSegOpt?.Invoke(segOpt);

		var trOpt = new OptTranslator();
		SetTranslatorOpt?.Invoke(trOpt);

		var ttsOpt = new OptTts();
		SetTtsOpt?.Invoke(ttsOpt);

		var langPipelineOpt = new OptLanguagePipeline();
		SetLanguagePipelineOpt?.Invoke(langPipelineOpt);

		Services.AddSingleton<IOptFileInteractor>(fileOpt);
		Services.AddSingleton<IOptSentenceSeg>(segOpt);
		Services.AddSingleton<IOptLanguagePipeline>(langPipelineOpt);
		Services.AddSingleton(trOpt);
		Services.AddSingleton(ttsOpt);

		Services.AddSingleton<ITypingListener, FileTypingListener>();
		Services.AddSingleton<ITranslator, FastLlmTranslator>();
		Services.AddSingleton<GttsViaHttpTts>();
		Services.AddSingleton<SystemSpeechTts>();
		Services.AddSingleton<ITts, OrderedFallbackTts>();
		Services.AddHttpClient<FastLlmTranslator>();
		Services.AddHttpClient<GttsViaHttpTts>();
		Services.AddHostedService<RimeTtsWorker>();

		return Services;
	}

	public static bool IsWindowsOnlyReady(this IHostEnvironment Env){
		_ = Env;
		return OperatingSystem.IsWindows();
	}
}
