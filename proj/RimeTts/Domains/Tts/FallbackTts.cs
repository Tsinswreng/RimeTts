namespace RimeTts.Domains.Tts;

using Microsoft.Extensions.Logging;

[Doc($$"""
按配置的優先級逐個試引擎，第一個成功的就用。

*為何要這一層*：gTTS 依賴網絡、SAPI 依賴本機音色；兩者的失敗條件互不相關。
把「試下一個」寫在這裡，呼叫方（流水線）就只看見一個「一定成功或全部失敗」的合成服務。

*為何不帶超時*：超時屬於「這一句話等了多久」的判斷，那是流水線的職責；
這裡只負責換引擎。舊版在這一層又包一層 12 秒／5 分鐘的超時，
結果外層還有一個 40 秒的，三層互相打架，取消起來誰也說不準。

*為何引擎用名字查而不是型別*：名字來自配置檔，使用者寫的是字串。
註冊表集中在這裡，未知名字只記一條警告並跳過——配置寫錯不該讓整個工具死掉。
""")]
public class FallbackTts : ITts{

	[Doc("引擎名（小寫）→ 實現。建構時由注入的引擎清單建起來，故新增引擎不必改本類。")]
	readonly Dictionary<str, ITtsEngine> EnginesByName;

	[Doc("全局引擎順序；語言的引擎清單為空時用它。")]
	readonly OptTts Opt;

	[Doc("逐次嘗試的日誌；哪個引擎失敗、為什麼失敗都要留痕，否則只看到「全部失敗」。")]
	readonly ILogger<FallbackTts> Log;

	[Doc($$"""
	建引擎註冊表。
	#Prm[{{nameof(Engines)}}][本應用註冊的全部引擎；由容器以 `IEnumerable<ITtsEngine>` 注入。]
	#Prm[{{nameof(Opt)}}][全局引擎順序。]
	#Prm[{{nameof(Log)}}][日誌。]
	""")]
	public FallbackTts(
		IEnumerable<ITtsEngine> Engines
		,OptTts Opt
		,ILogger<FallbackTts> Log
	){
		this.Opt = Opt;
		this.Log = Log;
		EnginesByName = new Dictionary<str, ITtsEngine>(StringComparer.OrdinalIgnoreCase);
		foreach(var Engine in Engines){
			EnginesByName[Engine.Name] = Engine;
		}
	}

	[Doc($$"""
	按優先級合成：第一個成功的引擎的結果就返回。
	#Prm[{{nameof(Req)}}][合成請求；它自己的 {{nameof(ReqGenEtPlay.PreferredEngines)}} 優先於全局設定。]
	#Rtn[音頻檔路徑。]
	#Throw[{{nameof(TypedErr)}}][所有引擎都失敗（{{nameof(KeysErr.Tts.AllEnginesFailed)}}）時。]
	""")]
	public async Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		var Names = ResolveEngineNames(Req);
		var Errors = new List<Exception>();
		foreach(var Name in Names){
			Ct.ThrowIfCancellationRequested();
			if(!EnginesByName.TryGetValue(Name, out var Engine)){
				Log.LogWarning("unknown tts engine ignored: {Engine}", Name);
				continue;
			}
			try{
				return await Engine.GenerateAudio(Req, Ct);
			}
			catch(OperationCanceledException){
				throw;
			}
			catch(Exception Ex){
				Errors.Add(Ex);
				Log.LogWarning(Ex, "tts engine failed: {Engine}; lang={Lang}", Name, Req.Language);
			}
		}

		var Err = new TypedErr("all configured tts engines failed", new AggregateException(Errors)){
			Type = KeysErr.Tts.AllEnginesFailed,
		};
		Err.AddDebugArgs($"lang={Req.Language}", $"engines={string.Join(',', Names)}");
		throw Err;
	}

	[Doc($$"""
	決定這次要試哪些引擎（去空白、去重、保持順序）。
	語言條目自己的清單優先；它為空才用全局設定；兩者都空時用內建順序。
	""")]
	IList<str> ResolveEngineNames(ReqGenEtPlay Req){
		var Src = Req.PreferredEngines is { Count: > 0 }
			? Req.PreferredEngines
			: (Opt.Engines is { Count: > 0 } ? Opt.Engines : [GttsViaHttpTts.EngineName, SystemSpeechTts.EngineName]);
		return Src
			.Where(X => !str.IsNullOrWhiteSpace(X))
			.Select(X => X.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}
}
