using Tsinswreng.CsTreeTest;
using RimeTts.Domains.Seg;

namespace RimeTts.Test.Domains.Seg;

public partial class TestSentenceBuffer{

	[Doc("預設間隔取 5 秒；測試裡把它固定下來，改預設值時這條會提醒有沒有連帶影響。")]
	const i64 GapMs = 5000;

	[Doc("造一個被測對象與它的假時鐘。")]
	static (SentenceBuffer Buffer, Fakes.FakeTimeProvider Time) Mk(){
		var Time = new Fakes.FakeTimeProvider();
		var Opt = new OptSentenceSeg{
			NoCommitGapMs = GapMs,
			SentenceTerminators = new List<str>{ "。", "！" },
		};
		return (new SentenceBuffer(Opt, Time), Time);
	}

	public partial void RegisterBuffer(){
		this.AddTestCase(nameof(Append_TerminatorFlushes), Append_TerminatorFlushes!);
		this.AddTestCase(nameof(Append_WithoutTerminatorAccumulates), Append_WithoutTerminatorAccumulates!);
		this.AddTestCase(nameof(Idle_FlushesOnlyAfterGap), Idle_FlushesOnlyAfterGap!);
		this.AddTestCase(nameof(Flush_ClearsBuffer), Flush_ClearsBuffer!);
		this.AddTestCase(nameof(Append_IgnoresBlank), Append_IgnoresBlank!);
	}

	public partial async Task<nil> Append_TerminatorFlushes(obj? O){
		var T = Assert.IsTrue;
		var (Buffer, Time) = Mk();

		T(Buffer.Append("你好") is null, "沒有終止符時不該成句");
		Time.Advance(TimeSpan.FromMilliseconds(100));
		var Sentence = Buffer.Append("世界。");

		T(Sentence == "你好世界。", $"應成句為「你好世界。」，實際「{Sentence}」");
		return NIL;
	}

	public partial async Task<nil> Append_WithoutTerminatorAccumulates(obj? O){
		var T = Assert.IsTrue;
		var (Buffer, Time) = Mk();

		T(Buffer.Append("一") is null, "單字上屏不該成句");
		Time.Advance(TimeSpan.FromMilliseconds(100));
		T(Buffer.Append("二") is null, "沒有終止符時不該成句");
		Time.Advance(TimeSpan.FromMilliseconds(100));
		T(Buffer.Append("三") is null, "沒有終止符時不該成句");

		// 累積的內容要完整（順序正確、不丟字）。
		Time.Advance(TimeSpan.FromMilliseconds(GapMs));
		var Sentence = Buffer.TryFlushByIdle();
		T(Sentence == "一二三", $"累積內容應為「一二三」，實際「{Sentence}」");
		return NIL;
	}

	public partial async Task<nil> Idle_FlushesOnlyAfterGap(obj? O){
		var T = Assert.IsTrue;
		var (Buffer, Time) = Mk();
		Buffer.Append("你好");

		Time.Advance(TimeSpan.FromMilliseconds(GapMs - 1));
		T(Buffer.TryFlushByIdle() is null, "還沒到間隔就不該成句");

		Time.Advance(TimeSpan.FromMilliseconds(1));
		var Sentence = Buffer.TryFlushByIdle();
		T(Sentence == "你好", $"到間隔應成句「你好」，實際「{Sentence}」");

		// 已經沒有內容了，再問一次不該重複成句。
		T(Buffer.TryFlushByIdle() is null, "沒有累積內容時不該成句");
		return NIL;
	}

	public partial async Task<nil> Flush_ClearsBuffer(obj? O){
		var T = Assert.IsTrue;
		var (Buffer, Time) = Mk();

		Buffer.Append("第一句。");
		Time.Advance(TimeSpan.FromMilliseconds(100));
		var Second = Buffer.Append("第二句。");

		T(Second == "第二句。", $"成句後緩衝應已清空，實際「{Second}」");
		return NIL;
	}

	public partial async Task<nil> Append_IgnoresBlank(obj? O){
		var T = Assert.IsTrue;
		var (Buffer, Time) = Mk();

		T(Buffer.Append("") is null, "空上屏不該成句");
		T(Buffer.Append("   ") is null, "純空白不該成句並成句");
		Time.Advance(TimeSpan.FromMilliseconds(GapMs));
		T(Buffer.TryFlushByIdle() is null, "只有空白時不該成句");

		T(Buffer.Append("好。") == "好。", "空白上屏不該污染累積內容");
		return NIL;
	}
}
