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

*為何合成也要並行、而播放不能*：合成是 CPU／網絡，彼此獨立；播放共用一張音效卡，必須排隊。
故各語言的合成在入隊前就啟動，{{nameof(AudioPlaylist)}} 只負責按順序等它、播它。

*為何失敗只影響一個語言*：某語言翻不出來或沒有語音，其他語言照樣該唸。
故每一格都自己 catch 並回 null，而不是讓整句話失敗。
""")]
public interface ISentenceFanout{

	[Doc($$"""
	處理一句：並行翻譯各語言，並按順序把生成任務交給播放隊列。
	*本方法會等「所有翻譯都回來」，但不等音頻生成*. 故「句子的處理」是逐句的，
	而「合成與播放」與下一句的翻譯重疊。
	#Prm[{{nameof(Sentence)}}][已成句的文字。]
	""")]
	public Task RunAsync(str Sentence, CT Ct);
}

[Doc($$"""
{{nameof(ISentenceFanout)}} 的實現。
#See[{{nameof(ISentenceFanout)}}][契約與理由。]
""")]
public class SentenceFanout : ISentenceFanout{

	[Doc("單段音頻生成的超時。超過就當這個語言失敗——讓整句話卡在一個引擎上不值得。")]
	public static readonly TimeSpan GenerateTimeout = TimeSpan.FromSeconds(40);

	[Doc("語言流水線設定（順序、提示詞、各語言引擎）。")]
	readonly OptLanguagePipeline Pipeline;

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
	#Prm[{{nameof(Translator)}}][翻譯器。]
	#Prm[{{nameof(Tts)}}][合成服務。]
	#Prm[{{nameof(Playlist)}}][播放隊列。]
	#Prm[{{nameof(Log)}}][日誌。]
	""")]
	public SentenceFanout(
		OptLanguagePipeline Pipeline
		,ITranslator Translator
		,ITts Tts
		,IAudioPlaylist Playlist
		,ILogger<SentenceFanout> Log
	){
		this.Pipeline = Pipeline;
		this.Translator = Translator;
		this.Tts = Tts;
		this.Playlist = Playlist;
		this.Log = Log;
	}

	[Doc("處理一句：先並行翻譯，再按順序把各語言的合成任務入隊。")]
	public async Task RunAsync(str Sentence, CT Ct){
		var Profiles = NormalizeProfiles();
		if(Profiles.Count == 0){
			Log.LogWarning("no language profile configured; fallback to en");
			Profiles = [new LanguageProfile{ Language = "en", SystemPrompt = "", TtsEngines = [] }];
		}

		// step 1: 並行翻譯。各語言互不依賴，等最慢的那個即可。
		var TranslateTasks = Profiles.Select(P => TranslateSafeAsync(Sentence, P, Ct)).ToList();
		var Translated = await Task.WhenAll(TranslateTasks);

		// step 2: 按配置順序入隊。合成任務在這裡就啟動（不是等到播放才做）。
		for(var I = 0; I < Profiles.Count; I++){
			var Text = Translated[I];
			if(str.IsNullOrWhiteSpace(Text)){
				continue;
			}
			var Profile = Profiles[I];
			Playlist.Add(new AudioJob{
				Language = Profile.Language,
				GenTask = GenerateSafeAsync(new ReqGenEtPlay{
					Text = Text,
					Language = Profile.Language,
					PreferredEngines = Profile.TtsEngines,
				}, Ct),
			});
		}
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
		catch(OperationCanceledException){
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
		using var Cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		Cts.CancelAfter(GenerateTimeout);
		try{
			return await Tts.GenerateAudio(Req, Cts.Token);
		}
		catch(OperationCanceledException) when(!Ct.IsCancellationRequested){
			Log.LogWarning("audio generate timed out. lang={Lang}; timeoutSec={Timeout}",
				Req.Language, GenerateTimeout.TotalSeconds);
			return null;
		}
		catch(Exception Ex){
			Log.LogWarning(Ex, "audio generate failed. lang={Lang}", Req.Language);
			return null;
		}
	}
}
