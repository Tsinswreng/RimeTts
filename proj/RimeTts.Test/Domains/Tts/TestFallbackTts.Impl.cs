using Tsinswreng.CsKeyNode;
using Microsoft.Extensions.Logging.Abstractions;
using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Tts;

namespace RimeTts.Test.Domains.Tts;

public partial class TestFallbackTts{

	[Doc("造一個被測對象：引擎清單由測試給，全局順序也由測試給。")]
	static FallbackTts Mk(IEnumerable<ITtsEngine> Engines, params str[] GlobalOrder){
		return new FallbackTts(
			Engines
			,new OptTts{ Engines = GlobalOrder.ToList() }
			,NullLogger<FallbackTts>.Instance
		);
	}

	[Doc("造一個合成請求。")]
	static ReqGenEtPlay MkReq(IList<str>? Preferred = null){
		return new ReqGenEtPlay{ Text = "hello", Language = "en", PreferredEngines = Preferred };
	}

	public partial void RegisterFallback(){
		this.AddTestCase(nameof(UsesFirstSuccessfulEngine), UsesFirstSuccessfulEngine!);
		this.AddTestCase(nameof(FallsThroughToNextEngine), FallsThroughToNextEngine!);
		this.AddTestCase(nameof(RequestEnginesOverrideGlobal), RequestEnginesOverrideGlobal!);
		this.AddTestCase(nameof(ThrowsWhenAllFail), ThrowsWhenAllFail!);
		this.AddTestCase(nameof(IgnoresUnknownEngine), IgnoresUnknownEngine!);
	}

	public partial async Task<nil> UsesFirstSuccessfulEngine(obj? O){
		var T = Assert.IsTrue;
		var First = new Fakes.FakeTtsEngine("a");
		var Second = new Fakes.FakeTtsEngine("b");
		var Tts = Mk([First, Second], "a", "b");

		var File = await Tts.GenerateAudio(MkReq(), default);

		T(File == "a.en.mp3", $"應由第一個引擎產出，實際 {File}");
		T(Second.Calls == 0, $"第一個成功之後不該再叫第二個，實際被叫 {Second.Calls} 次");
		return NIL;
	}

	public partial async Task<nil> FallsThroughToNextEngine(obj? O){
		var T = Assert.IsTrue;
		var First = new Fakes.FakeTtsEngine("a", Fail: true);
		var Second = new Fakes.FakeTtsEngine("b");
		var Tts = Mk([First, Second], "a", "b");

		var File = await Tts.GenerateAudio(MkReq(), default);

		T(First.Calls == 1, $"第一個應被試過一次，實際 {First.Calls} 次");
		T(File == "b.en.mp3", $"應換手到第二個引擎，實際 {File}");
		return NIL;
	}

	public partial async Task<nil> RequestEnginesOverrideGlobal(obj? O){
		var T = Assert.IsTrue;
		var First = new Fakes.FakeTtsEngine("a");
		var Second = new Fakes.FakeTtsEngine("b");
		// 全局順序是 a 先，但請求指定只用 b。
		var Tts = Mk([First, Second], "a", "b");

		var File = await Tts.GenerateAudio(MkReq(["b"]), default);

		T(File == "b.en.mp3", $"應聽請求的引擎清單，實際 {File}");
		T(First.Calls == 0, $"請求沒列 a，不該叫它，實際被叫 {First.Calls} 次");
		return NIL;
	}

	public partial async Task<nil> ThrowsWhenAllFail(obj? O){
		var T = Assert.IsTrue;
		var Tts = Mk(
			[new Fakes.FakeTtsEngine("a", Fail: true), new Fakes.FakeTtsEngine("b", Fail: true)]
			,"a", "b"
		);

		var Key = "";
		try{
			await Tts.GenerateAudio(MkReq(), default);
		}
		catch(TypedErr Ex){
			Key = Ex.Type.FullPath;
		}
		var Expect = KeysErr.Tts.AllEnginesFailed.FullPath;
		T(Key == Expect, $"應拋 {Expect}，實際 {Key}");
		return NIL;
	}

	public partial async Task<nil> IgnoresUnknownEngine(obj? O){
		var T = Assert.IsTrue;
		var Real = new Fakes.FakeTtsEngine("b");
		var Tts = Mk([Real], "nope", "b");

		var File = await Tts.GenerateAudio(MkReq(), default);

		T(File == "b.en.mp3", $"不認識的引擎應被跳過，實際 {File}");
		return NIL;
	}
}
