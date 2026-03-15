using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RimeTts;

public static class ServiceCollectionExt{
	public static IServiceCollection AddRimeTts(this IServiceCollection Services, Action<OptFileInteractor> SetFileOpt, Action<OptSentenceSeg>? SetSegOpt = null, Action<OptTranslator>? SetTranslatorOpt = null, Action<OptTts>? SetTtsOpt = null){
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

		Services.AddSingleton<IOptFileInteractor>(fileOpt);
		Services.AddSingleton<IOptSentenceSeg>(segOpt);
		Services.AddSingleton(trOpt);
		Services.AddSingleton(ttsOpt);

		Services.AddSingleton<ITypingListener, FileTypingListener>();
		Services.AddSingleton<ITranslator, FastLlmTranslator>();
		Services.AddSingleton<ITts, GttsViaPythonNetTts>();
		Services.AddHttpClient<FastLlmTranslator>();
		Services.AddHostedService<RimeTtsWorker>();

		return Services;
	}

	public static bool IsWindowsOnlyReady(this IHostEnvironment Env){
		_ = Env;
		return OperatingSystem.IsWindows();
	}
}
