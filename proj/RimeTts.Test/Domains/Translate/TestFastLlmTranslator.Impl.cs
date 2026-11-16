using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Translate;

namespace RimeTts.Test.Domains.Translate;

public partial class TestFastLlmTranslator{

	[Doc($$"""
	假 HTTP handler：每次都回同一份預設的正文，並計數被請求了幾次。
	""")]
	sealed class FakeHandler(str AssistantContent) : HttpMessageHandler{

		[Doc("被請求次數——快取定性的核心觀察量。")]
		public i32 Hits{get;private set;}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage Req, CT Ct){
			Hits++;
			// chat completions 的最小回應形狀：choices[0].message.content。
			// content 走 JsonEncodedText 編碼（正文裡可能有引號與非 ASCII）：
			// 它的 ToString() 給的是*不含界定引號*的轉義內容，故外層要自己補一對引號。
			var ContentJson = System.Text.Json.JsonEncodedText.Encode(AssistantContent).ToString();
			var Body = "{\"choices\":[{\"message\":{\"content\":\"" + ContentJson + "\"}}]}";
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){
				Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json"),
			});
		}
	}

	[Doc($$"""
	造一個被測翻譯器：模型每次都回 {{nameof(AssistantContent)}}。
	#Rtn[翻譯器、假 handler、快取本體。]
	""")]
	static (FastLlmTranslator Tr, FakeHandler Http, TranslationCache Cache) Mk(str AssistantContent){
		var Http = new FakeHandler(AssistantContent);
		var Cache = new TranslationCache(16);
		var Opt = new OptTranslator{
			ApiKey = "sk-test",
			BaseUrl = "https://example.test/v1/chat",
			Model = "test-model",
			TimeoutSec = 5,
			CacheCapacity = 16,
			DefaultSystemPrompt = "p",
		};
		var Tr = new FastLlmTranslator(Opt, new HttpClient(Http), Cache, NullLogger<FastLlmTranslator>.Instance);
		return (Tr, Http, Cache);
	}

	[Doc("造一個翻譯請求（同一句、同一語言、同一提示詞→同一個快取鍵）。")]
	static ReqTranslate MkReq(){
		return new ReqTranslate{ SourceText = "你好", TargetLanguage = "ja", SystemPrompt = "s" };
	}

	[Doc("模型回「翻不了」的 jsonr 正文。")]
	const str NullAnswer = $$"""{"UserInputLang":"zh","TargetLang":"ja","Translation":null}""";

	[Doc("模型回錯語言的 jsonr 正文（要 ja 給 en）。")]
	const str WrongLangAnswer = $$"""{"UserInputLang":"zh","TargetLang":"en","Translation":"hello"}""";

	[Doc("模型回成功譯文的 jsonr 正文。")]
	const str OkAnswer = $$"""{"UserInputLang":"zh","TargetLang":"ja","Translation":"こんにちは"}""";

	public partial void RegisterCacheSemantics(){
		this.AddTestCase(nameof(ModelNull_IsCached), ModelNull_IsCached!);
		this.AddTestCase(nameof(ParseFailure_IsNotCached), ParseFailure_IsNotCached!);
		this.AddTestCase(nameof(LangMismatch_IsNotCached), LangMismatch_IsNotCached!);
		this.AddTestCase(nameof(Success_IsCached), Success_IsCached!);
	}

	public partial async Task<nil> ModelNull_IsCached(obj? O){
		var T = Assert.IsTrue;
		var (Tr, Http, _) = Mk(NullAnswer);

		var R1 = await Tr.Translate(MkReq(), default);
		var R2 = await Tr.Translate(MkReq(), default);

		T(R1.TranslatedText is null && R2.TranslatedText is null, "兩次都應是「翻不了」");
		T(Http.Hits == 1, $"模型的 null 是確定結論，應只問一次，實際 {Http.Hits} 次");
		return NIL;
	}

	public partial async Task<nil> ParseFailure_IsNotCached(obj? O){
		var T = Assert.IsTrue;
		// 模型回了一段不是 jsonr 的東西（加了自己的花樣）。
		var (Tr, Http, _) = Mk("我想直接用純文本回答你");

		var R1 = await Tr.Translate(MkReq(), default);
		var R2 = await Tr.Translate(MkReq(), default);

		T(R1.TranslatedText is null && R2.TranslatedText is null, "兩次都翻不出");
		// BUG-3 的核心：解析失敗不是結論，第二次要真的再問（可重試）。
		T(Http.Hits == 2, $"解析失敗不進快取，第二次應再發請求，實際 {Http.Hits} 次");
		return NIL;
	}

	public partial async Task<nil> LangMismatch_IsNotCached(obj? O){
		var T = Assert.IsTrue;
		var (Tr, Http, _) = Mk(WrongLangAnswer);

		var R1 = await Tr.Translate(MkReq(), default);
		var R2 = await Tr.Translate(MkReq(), default);

		T(R1.TranslatedText is null && R2.TranslatedText is null, "兩次都翻不出");
		T(Http.Hits == 2, $"答錯語言不是結論，第二次應再發請求，實際 {Http.Hits} 次");
		return NIL;
	}

	public partial async Task<nil> Success_IsCached(obj? O){
		var T = Assert.IsTrue;
		var (Tr, Http, _) = Mk(OkAnswer);

		var R1 = await Tr.Translate(MkReq(), default);
		var R2 = await Tr.Translate(MkReq(), default);

		T(R1.TranslatedText == "こんにちは" && R2.TranslatedText == "こんにちは", "兩次都應命中同一譯文");
		T(Http.Hits == 1, $"成功譯文應進快取，只問一次，實際 {Http.Hits} 次");
		return NIL;
	}
}
