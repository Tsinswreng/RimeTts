namespace RimeTts.Domains.Pipeline;

using Microsoft.Extensions.Logging;
using RimeTts.Domains.Tts;
using RimeTts.Domains.Translate;
using RimeTts.Infra;

[Doc($$"""
把一句話變成「按配置順序、正在生成的若干段音頻」。

*為何翻譯要並行*：五個語言就是五次 LLM 往返。串行做的話第一個語言的聲音要等四五倍的時間
才出來；並行之後整句話的「到第一段可播」時間約等於最慢的那一次。
（原本的版本是串行翻譯、再串行合成，這正是先前被列為缺陷的地方。）

*為何「先佔位、後等待」而不是「等全部翻完再入隊」*：本方法曾經用
`Task.WhenAll` 等全部翻譯回來才把合成任務入隊（一道屏障）。
那道屏障有兩個代價：某個語言懸住時，*已經翻好的*語言也被扣住不播；
屏障本身的異常語義還把「單一語言超時」放大成整條流水線死亡
（2026-11-16 審查 BUG-1／BUG-2，已修）。
現在每個語言在{{nameof(RunAsync)}}裏**立刻**按序入隊一個
「翻譯 → 合成」的完整任務鏈（{{nameof(AudioJob.GenTask)}}）；
播放隊列天生支持先入隊後等待，於是壞語言最多拖住它自己那一格，
而各語言的合成也從「全句翻完才開始」提前到「自己翻完就開始」。

*為何失敗只影響一個語言*：某語言翻不出來或沒有語音，其他語言照樣該唸。
故每一格的任務鏈都自己 catch 並回 null，而不是讓整句話失敗。

*順序保證*：入隊順序就是語言順序（本方法同步循環 `Add`）；
句子之間的順序由唯一的句子消費循環保證（見 `RimeTtsPipeline`）。

*併發上界*：一句話 N 個語言就是 N 條並行鏈；相鄰句子的鏈也可能重疊
（上一句還在翻、下一句已經開始）。句子由使用者打字速率決定（間隔成句），
實務上同時在途的請求個位數；若日後要做節流，加在翻譯器那一層，
不要把屏障加回這裏。
""")]
public interface ISentenceFanout{

	[Doc($$"""
	處理一句：為每個配置的語言按序入隊一個「翻譯 → 合成」任務鏈。
	*本方法入隊完成即返回*——不等翻譯、也不等合成；
	「等」由播放隊列按入隊順序自己做。故上一句還在生成時，
	下一句的翻譯已經可以開始（重疊是刻意的，見本接口說明）。
	#Prm[{{nameof(Sentence)}}][已成句的文字。]
	#Throw[{{nameof(OperationCanceledException)}}][僅當{{nameof(Ct)}}已被取消（全局關停）時。]
	""")]
	public Task<nil> RunAsync(str Sentence, CT Ct);
}

[Doc($$"""
{{nameof(ISentenceFanout)}} 的實現。
#See[{{nameof(ISentenceFanout)}}][契約與理由。]
""")]
public class SentenceFanout : ISentenceFanout{

	[Doc("語言流水線設定（順序、提示詞、各語言引擎）。")]
	readonly OptLanguagePipeline Pipeline;

	[Doc("編排層超時設定（生成超時）。默認值在 KeysAppCfg.Pipeline。")]
	readonly OptPipeline Opt;

	[Doc("翻譯器。")]
	readonly ITranslator Translator;

	[Doc("合成服務。")]
	readonly ITts Tts;

	[Doc("播放隊列；本類只往裡放，不負責播。")]
	readonly IAudioPlaylist Playlist;

	[Doc("日誌。")]
	readonly ILogger<SentenceFanout> Log;

	[Doc($$"""
	接上各依賴。
	#Prm[{{nameof(Pipeline)}}][語言流水線設定。]
	#Prm[{{nameof(Opt)}}][編排層超時設定。]
	#Prm[{{nameof(Translator)}}][翻譯器。]
	#Prm[{{nameof(Tts)}}][合成服務。]
	#Prm[{{nameof(Playlist)}}][播放隊列。]
	#Prm[{{nameof(Log)}}][日誌。]
	""")]
	public SentenceFanout(
		OptLanguagePipeline Pipeline
		,OptPipeline Opt
		,ITranslator Translator
		,ITts Tts
		,IAudioPlaylist Playlist
		,ILogger<SentenceFanout> Log
	){
		this.Pipeline = Pipeline;
		this.Opt = Opt;
		this.Translator = Translator;
		this.Tts = Tts;
		this.Playlist = Playlist;
		this.Log = Log;
	}

	[Doc($$"""
	處理一句：歸一化語言清單，然後為每個語言**立刻**入隊它的任務鏈。
	空清單只記警告並跳過——不再有「偷偷退化成只翻英文」的第三層兜底
	（啟動驗證已在 `DiCore` 擋掉空配置，走到這裏的空清單只可能來自
	「條目全部無效」，那是配置錯誤，該被看見而不是被兜底蓋住；
	見 2026-11-16 審查 BUG-4）。
	""")]
	public async Task<nil> RunAsync(str Sentence, CT Ct){
		Ct.ThrowIfCancellationRequested();
		var Profiles = NormalizeProfiles();
		if(Profiles.Count == 0){
			Log.LogWarning("no valid language profile after normalization; sentence dropped. sentence={Sentence}", Sentence);
			return NIL;
		}

		// 每個語言立刻入隊它的「翻譯 → 合成」鏈：
		// Add 是同步的、按語言順序呼叫，故隊列順序就是配置順序；
		// 鏈在 Add 之前就已經開始跑（任務是熱的），不需要本方法去等它。
		foreach(var Profile in Profiles){
			var Lang = Profile.Language;
			Playlist.Add(new AudioJob{
				Language = Lang,
				GenTask = ProcessLangAsync(Sentence, Profile, Ct),
			});
		}
		return NIL;
	}

	[Doc($$"""
	一個語言的完整任務鏈：翻譯 → 合成。任何一步失敗都回 null（只影響這一格）。
	*超時的分工*：翻譯的超時屬於「這一次請求」，在翻譯器內部
	（`FastLlmTranslator` 按配置的 `Translator.TimeoutSec` 各自計時）；
	合成的超時屬於「這一段音頻」，由本類按 `Pipeline.GenerateTimeoutSec` 包住。
	每一層管好自己的上限，不疊加一個寫死的總上限——
	疊加只會讓三層超時互相打架（舊版正是這樣）。
	""")]
	async Task<str?> ProcessLangAsync(str Sentence, LanguageProfile Profile, CT Ct){
		var Text = await TranslateSafeAsync(Sentence, Profile, Ct);
		if(str.IsNullOrWhiteSpace(Text)){
			return null;
		}
		return await GenerateSafeAsync(new ReqGenEtPlay{
			Text = Text,
			Language = Profile.Language,
			PreferredEngines = Profile.TtsEngines,
		}, Ct);
	}

	[Doc($$"""
	去掉無效的語言條目並歸一化語言代碼（順序不變）。
	*為何要歸一化*：配置裡可能寫 `JP`，而引擎與提示詞都按 `ja` 走；
	在這裡統一，下游就不必各自糾正。
	""")]
	IList<LanguageProfile> NormalizeProfiles(){
		var Result = new List<LanguageProfile>();
		foreach(var P in Pipeline.Languages){
			if(P is null){
				continue;
			}
			var Lang = LangTag.Normalize(P.Language);
			if(Lang.Length == 0){
				continue;
			}
			Result.Add(new LanguageProfile{
				Language = Lang,
				SystemPrompt = P.SystemPrompt ?? "",
				TtsEngines = P.TtsEngines ?? [],
			});
		}
		return Result;
	}

	[Doc($$"""
	翻一個語言；失敗回 null（只影響這一個語言）。

	*取消的三段過濾是本方法的核心（2026-11-16 審查 BUG-1 的修復點）*：
	`OperationCanceledException` 有兩種來源——全局關停（{{nameof(Ct)}} 已取消）與
	「這一次請求超時」（翻譯器內部的鏈接式 CTS 到點，例如 `HttpClient` 拋的
	`TaskCanceledException`）。舊寫法無過濾地把兩種都上拋，
	於是一次普通的單語言超時被當成關停，殺死了唯一的句子消費循環，
	整個進程從此不再翻譯（日誌一片空白、監聽器照常、關停時還偽裝成正常退出）。
	現在：非全局的取消與其他異常一樣，記警告、回 null，這個語言跳過、其他語言照常。
	#Prm[{{nameof(Sentence)}}][原文。]
	#Prm[{{nameof(Profile)}}][目標語言條目。]
	#Rtn[譯文；翻不了或出錯時 null。]
	""")]
	async Task<str?> TranslateSafeAsync(str Sentence, LanguageProfile Profile, CT Ct){
		try{
			var Resp = await Translator.Translate(new ReqTranslate{
				SourceText = Sentence,
				TargetLanguage = Profile.Language,
				SystemPrompt = Profile.SystemPrompt,
			}, Ct);
			return Resp.TranslatedText;
		}
		catch(OperationCanceledException) when(!Ct.IsCancellationRequested){
			// 單次請求超時／取消（不是全局關停）：只放棄這個語言。
			Log.LogWarning("translate timed out or canceled per request. lang={Lang}; sentence={Sentence}",
				Profile.Language, Sentence);
			return null;
		}
		catch(OperationCanceledException){
			// 全局關停：如實上拋，讓下遊按關停收尾。
			throw;
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "translate failed. lang={Lang}; sentence={Sentence}", Profile.Language, Sentence);
			return null;
		}
	}

	[Doc($$"""
	合成一段；失敗或超時回 null（只影響這一段）。
	*為何超時從這裡起算*：它要包住「這次合成的全部時間」；
	若等到播放隊列輪到它才算，前面排隊的時間也會被計進去。
	""")]
	async Task<str?> GenerateSafeAsync(ReqGenEtPlay Req, CT Ct){
		var Timeout = TimeSpan.FromSeconds(Math.Max(1, Opt.GenerateTimeoutSec));
		using var Cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		Cts.CancelAfter(Timeout);
		try{
			return await Tts.GenerateAudio(Req, Cts.Token);
		}
		catch(OperationCanceledException) when(!Ct.IsCancellationRequested){
			Log.LogWarning("audio generate timed out. lang={Lang}; timeoutSec={Timeout}",
				Req.Language, Timeout.TotalSeconds);
			return null;
		}
		catch(OperationCanceledException){
			// 全局關停：如實上拋（舊寫法落到 catch(Exception) 把關停吞成 null，語義不對）。
			throw;
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "audio generate failed. lang={Lang}", Req.Language);
			return null;
		}
	}
}
