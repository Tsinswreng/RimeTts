namespace RimeTts.Domains.Seg;

using System.Text;

[Doc($$"""
把逐次上屏的文字累積成一句。

*為何需要「累積」這一步*：使用者是逐字詞上屏的，一次上屏往往只是一個詞。
若逐詞送進翻譯，譯文會碎成一地、朗讀也斷斷續續；故先攢著，等「夠了」才成句。

*「夠了」的兩個條件*（見 {{nameof(OptSentenceSeg)}}）：
上屏以終止符結尾 → 立刻成句；或距上次上屏超過 {{nameof(OptSentenceSeg.NoCommitGapMs)}} → 成句。

*為何要注入 {{nameof(TimeProvider)}}*：成句與否取決於時間，而測試不能真的等五秒。
注入時鐘之後，「經過了五秒」是一個可造的事實，而不是一次 sleep。
""")]
public interface ISentenceBuffer{

	[Doc($$"""
	收到一次上屏。
	#Prm[{{nameof(Text)}}][這次上屏的字；空白會被忽略。]
	#Rtn[若因此成句則回該句，否則 null。]
	""")]
	public str? Append(str Text);

	[Doc($$"""
	若「距上次上屏已超過配置的間隔」則成句。
	*何時呼叫*：由流水線的定時器週期性呼叫（本方法自己不等待）。
	#Rtn[成句的文字，否則 null。]
	""")]
	public str? TryFlushByIdle();

	[Doc("丟棄緩衝（停止時用；不產生句子）。")]
	public void Clear();
}

[Doc($$"""
{{nameof(ISentenceBuffer)}} 的實現。

*為何要鎖*：上屏回呼來自檔案監看器的線程池線程，定時器來自另一條。
兩個入口都會讀寫同一段緩衝，不加鎖會丟字或重複成句。
""")]
public class SentenceBuffer : ISentenceBuffer{

	[Doc("劃分規則（終止符清單與間隔）。")]
	readonly OptSentenceSeg Opt;

	[Doc("時鐘。測試注入假時鐘以造出「已經過了一段時間」。")]
	readonly TimeProvider Time;

	[Doc("累積中的文字。")]
	readonly StringBuilder Buf = new();

	[Doc("保護 {{nameof(Buf)}} 與 {{nameof(LastCommitAt)}}。")]
	readonly Lock Gate = new();

	[Doc("最後一次上屏的時刻；null 表示目前沒有累積內容。")]
	DateTimeOffset? LastCommitAt;

	[Doc($$"""
	接上劃分規則與時鐘。
	#Prm[{{nameof(Opt)}}][終止符清單與間隔。]
	#Prm[{{nameof(Time)}}][時鐘；省略時用系統時鐘。]
	""")]
	public SentenceBuffer(OptSentenceSeg Opt, TimeProvider? Time = null){
		this.Opt = Opt;
		this.Time = Time ?? TimeProvider.System;
	}

	[Doc($$"""
	收到一次上屏；以終止符結尾就立刻成句。
	#Prm[{{nameof(Text)}}][上屏字。]
	#Rtn[成句的文字，否則 null。]
	""")]
	public str? Append(str Text){
		if(str.IsNullOrWhiteSpace(Text)){
			return null;
		}
		lock(Gate){
			Buf.Append(Text);
			LastCommitAt = Time.GetUtcNow();
			return IsTerminated(Text) ? TakeUnsafe() : null;
		}
	}

	[Doc($$"""
	超過間隔就成句。
	*為何超過間隔時從「最後一次上屏」起算*：使用者可能連續打了十幾個詞，
	每次都更新時間戳，於是只有真的停下來才會成句。
	#Rtn[成句的文字，否則 null。]
	""")]
	public str? TryFlushByIdle(){
		lock(Gate){
			if(Buf.Length == 0 || LastCommitAt is null){
				return null;
			}
			var Elapsed = Time.GetUtcNow() - LastCommitAt.Value;
			if(Elapsed.TotalMilliseconds < Opt.NoCommitGapMs){
				return null;
			}
			return TakeUnsafe();
		}
	}

	[Doc("丟棄緩衝。")]
	public void Clear(){
		lock(Gate){
			Buf.Clear();
			LastCommitAt = null;
		}
	}

	[Doc($$"""
	這次上屏是否以終止符結尾。
	*為何只看最後一個字符*：一次上屏可能是一整句（貼上、整句候選上屏），
	用「包含」會讓句中的逗號也觸發成句，把一句話切成好幾段。
	""")]
	bool IsTerminated(str Text){
		if(Opt.SentenceTerminators is not { Count: > 0 }){
			return false;
		}
		var Last = Text[^1];
		foreach(var Terminator in Opt.SentenceTerminators){
			// 逐個字符的終止符是常態；多字符的條目沒有意義，直接忽略。
			if(Terminator.Length == 1 && Terminator[0] == Last){
				return true;
			}
		}
		return false;
	}

	[Doc("取出並清空緩衝；空白內容視為沒有句子。")]
	str? TakeUnsafe(){
		var Text = Buf.ToString().Trim();
		Buf.Clear();
		LastCommitAt = null;
		return Text.Length == 0 ? null : Text;
	}
}
