namespace RimeTts;

using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Tsinswreng.CsJson;
using RimeTts.Domains.Pipeline;
using RimeTts.Domains.Seg;
using RimeTts.Domains.Translate;
using RimeTts.Domains.Tts;
using RimeTts.Domains.Typing;
using RimeTts.Infra;
using RimeTts.Infra.Cfg;
using RimeTts.Infra.Json;
using RimeTts.Infra.Os;

[Doc($$"""
業務程序集的依賴注入裝配。

*命名與寫法照文檔約定*：每個需要配 DI 的程序集各有 `Di<程序集>` 類，
裡面是 `Setup<程序集>(this IServiceCollection z)`；這裡是 `RimeTts` 本體，故叫 {{nameof(DiCore)}}。

*放什麼*：本程序集提供的全部服務——配置映射出來的選項、監聽器、翻譯器、合成與播放、
流水線。宿主專屬的東西（儲存區、日誌出口）由 `DiCli` 註冊。

*選項為何在建容器時就映射好*：配置錯了（缺密鑰、路徑為空）要在啟動時就報，
而不是等到使用者打了第一句話才在背景任務裡拋。故本方法收的是已經接好源的門面，
並立刻讀出所有選項。
""")]
public static class DiCore{

	[Doc($$"""
	註冊本程序集的全部服務。
	#Prm[{{nameof(BaseDir)}}][應用儲存區；用來把「空輸出目錄」解析成具體路徑。]
	#Rtn[同一個 {{nameof(IServiceCollection)}}，便於串接。]
	#Throw[{{nameof(TypedErr)}}][配置檔內容壞掉（{{nameof(KeysErr.Cfg.ParseFailed__)}}）時。]
	""")]
	public static IServiceCollection SetupCore(this IServiceCollection z, I_GetBaseDir BaseDir){
		var Cfg = AppCfg.Inst;

		// 1: 選項。全部由配置映射而來，默認值在 KeysAppCfg。
		//    先映射、後驗證、再註冊：配置缺東西要在啟動時就報，而不是等第一句話進來。
		//    這三條驗證都用 WithMsg 附一句人話，因為它們是使用者唯一能自己修好的錯。
		var FileOpt = CfgMapper.MkFileInteractor(Cfg);
		if(str.IsNullOrWhiteSpace(FileOpt.ContentFile) || str.IsNullOrWhiteSpace(FileOpt.SignalFile)){
			throw KeysErr.Common.ArgErr__.WithMsg(
				Todo.I18n("配置缺少 FileInteractor.ContentFile / FileInteractor.SignalFile：那兩個檔要與 Rime 的 Lua 插件約定一致。"));
		}
		var TrOpt = CfgMapper.MkTranslator(Cfg);
		if(str.IsNullOrWhiteSpace(TrOpt.ApiKey)){
			throw KeysErr.Cfg.ApiKeyEmpty.WithMsg(
				Todo.I18n("配置缺少 Translator.ApiKey：請在 rimetts.jsonr 裡填上 LLM 接口密鑰。"));
		}
		var LangOpt = CfgMapper.MkLanguagePipeline(Cfg);
		if(LangOpt.Languages is not { Count: > 0 }){
			throw KeysErr.Cfg.NoLanguage__.WithMsg(
				Todo.I18n("配置裡一個語言條目都沒有：LanguagePipeline.Languages 至少要有一項。"));
		}

		z.AddSingleton(FileOpt);
		z.AddSingleton(CfgMapper.MkSentenceSeg(Cfg));
		z.AddSingleton(TrOpt);
		z.AddSingleton(CfgMapper.MkTts(Cfg, BaseDir));
		z.AddSingleton(LangOpt);

		// 2: JSON 門面。注入的就是 AppJson.Inst 那一個，靜態與注入兩條路徑共用同一份選項。
		z.AddSingleton<IJsonSerializer>(AppJson.Inst);

		// 3: 時鐘。成句判斷要用它；注入之後測試才不必真的等五秒。
		z.AddSingleton(TimeProvider.System);

		// 4: HTTP 客戶端。整條流水線共用一條連線池：
		//    每次請求各自計時（見 FastLlmTranslator），故這裡不設 Timeout，
		//    否則某一句超時會變成全域故障。
		z.AddSingleton(_ => MkHttpClient());

		// 5: 譯文快取的容量取自配置，故用工廠而不是型別註冊。
		z.AddSingleton(sp => new TranslationCache(sp.GetRequiredService<OptTranslator>().CacheCapacity));

		// 6: 輸入側（Lua 插件 → 檔案 → 事件）。
		z.AddSingleton<ITypingListener, FileTypingListener>();

		// 7: 翻譯與合成。引擎以 ITtsEngine 註冊，FallbackTts 收 IEnumerable<ITtsEngine>：
		//    新增引擎只要多一行 AddSingleton<ITtsEngine, 新引擎>()，不必改 FallbackTts。
		z.AddSingleton<ITranslator, FastLlmTranslator>();
		z.AddSingleton<ITtsEngine, GttsViaHttpTts>();
		z.AddSingleton<ITtsEngine, SystemSpeechTts>();
		z.AddSingleton<ITts, FallbackTts>();

		// 8: 播放與編排。
		z.AddSingleton<IAudioPlayer, AudioPlayer>();
		z.AddSingleton<IAudioPlaylist, AudioPlaylist>();
		z.AddSingleton<ISentenceBuffer, SentenceBuffer>();
		z.AddSingleton<ISentenceFanout, SentenceFanout>();
		z.AddSingleton<IGlobalCancelHotkey, GlobalEscHook>();
		z.AddSingleton<ISvcRimeTts, RimeTtsPipeline>();

		return z;
	}

	[Doc($$"""
	造本應用的 HTTP 客戶端。
	*為何不用 `AddHttpClient`*：那套東西是為「每個服務不同基底位址、不同處理器」設計的；
	本專案只有兩個端點、共用一條連線池，故一個單例就夠，也少一個套件依賴。
	*為何 `Timeout` 是無限*：超時逐請求算（見 {{nameof(OptTranslator.TimeoutSec)}}），
	全域超時只會讓「某一句超時」變成「這一分鐘全部失敗」。
	""")]
	static HttpClient MkHttpClient(){
		var Handler = new SocketsHttpHandler{
			// 連線池不永久保留：DNS 變了或對端重啟時，舊連線會一直失敗。
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
			ConnectTimeout = TimeSpan.FromSeconds(15),
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
			UseCookies = false,
		};
		return new HttpClient(Handler, disposeHandler: true){
			Timeout = Timeout.InfiniteTimeSpan,
		};
	}
}
