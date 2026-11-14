namespace RimeTts.Test.Fakes;

[Doc($$"""
可造時間的 {{nameof(TimeProvider)}}。

*為何要它*：成句判斷是「距上次上屏超過 N 毫秒」；真等五秒會讓測試變慢且不穩。
把時鐘換掉之後，「過了五秒」是一個可以隨手造的事實。
""")]
public class FakeTimeProvider : TimeProvider{

	[Doc("當前時間；測試自己往前推。")]
	public DateTimeOffset Now{get;set;} = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	[Doc("推進時鐘。")]
	public void Advance(TimeSpan By){
		Now += By;
	}

	[Doc("取當前時間。")]
	public override DateTimeOffset GetUtcNow(){
		return Now;
	}
}
