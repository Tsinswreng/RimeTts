namespace RimeTts.Test.Fakes;

using RimeTts.Domains.Translate;
using RimeTts.Domains.Pipeline;

[Doc($$"""
假翻譯器：不發請求，直接把「語言:原文」當譯文回傳；可指定某語言翻不了（回 null）。

*為何需要*：{{nameof(SentenceFanout)}} 的行為是「並行翻各語言、按順序入隊」，
用假翻譯器才能在不碰網絡的前提下驗順序與失敗隔離。
""")]
public class FakeTranslator : ITranslator{

	[Doc("被請求過的目標語言，依呼叫順序。")]
	public IList<str> RequestedLangs{get;} = new List<str>();

	[Doc("哪些語言要回「翻不了」（TranslatedText = null）。")]
	public ISet<str> NullLangs{get;} = new HashSet<str>();

	[Doc("人為延遲（毫秒）；用來造出「後面的語言先翻完」這種順序。")]
	public i32 DelayMs{get;set;}

	[Doc("按語言指定延遲（毫秒）；設了就蓋過全體的 DelayMs，用來造「某語言懸住」。")]
	public IDictionary<str, i32> DelayMsByLang{get;} = new Dictionary<str, i32>();

	[Doc("按語言指定要拋的異常；用來測單一語言失敗的隔離。")]
	public IDictionary<str, Exception> FailByLang{get;} = new Dictionary<str, Exception>();

	[Doc($$"""
	按語言指定「懸住用的取消源」：設了之後，該語言的延遲改掛在這個*內部*令牌上。
	用來模擬翻譯器內部的請求級超時——`HttpClient` 在內部 CTS 到點時拋的
	`OperationCanceledException` 正是這種：內部令牌已取消、外部（全局）令牌從未取消。
	""")]
	public IDictionary<str, CancellationTokenSource> InternalCancelByLang{get;} = new Dictionary<str, CancellationTokenSource>();

	[Doc("翻譯：把結果寫成 `<語言>:<原文>`。")]
	public async Task<RespTranslate> Translate(ReqTranslate Req, CT Ct){
		RequestedLangs.Add(Req.TargetLanguage);
		if(FailByLang.TryGetValue(Req.TargetLanguage, out var Ex)){
			throw Ex;
		}
		if(InternalCancelByLang.TryGetValue(Req.TargetLanguage, out var InternalCts)){
			// 掛在內部令牌上：它取消時拋的 OCE 不帶外部令牌的印記，
			// 正是扇出層必須過濾掉的那一種（BUG-1 的場景）。
			await Task.Delay(Timeout.Infinite, InternalCts.Token);
			throw new OperationCanceledException(InternalCts.Token);
		}
		var Delay = DelayMsByLang.TryGetValue(Req.TargetLanguage, out var D) ? D : DelayMs;
		if(Delay > 0){
			await Task.Delay(Delay, Ct);
		}
		var Translated = NullLangs.Contains(Req.TargetLanguage)
			? null
			: $"{Req.TargetLanguage}:{Req.SourceText}";
		return new RespTranslate{
			SourceText = Req.SourceText,
			TargetLanguage = Req.TargetLanguage,
			TranslatedText = Translated,
		};
	}
}
