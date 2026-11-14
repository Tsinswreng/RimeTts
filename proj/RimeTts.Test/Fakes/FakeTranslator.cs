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

	[Doc("翻譯：把結果寫成 `<語言>:<原文>`。")]
	public async Task<RespTranslate> Translate(ReqTranslate Req, CT Ct){
		RequestedLangs.Add(Req.TargetLanguage);
		if(DelayMs > 0){
			await Task.Delay(DelayMs, Ct);
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
