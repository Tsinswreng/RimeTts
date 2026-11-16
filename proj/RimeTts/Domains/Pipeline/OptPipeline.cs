namespace RimeTts.Domains.Pipeline;

[Doc($$"""
編排層（扇出與播放隊列）的超時設定。

*為何與其他 Opt 一樣不帶業務默認值*：默認值的唯一出處是
`RimeTts.Cfg.KeysAppCfg.Pipeline`；本類的屬性一律是中性值（0），
由 `CfgMapper.MkPipeline` 從配置映射賦值。
直接 `new` 只用於測試——那時呼叫方自己給明確值，
0 會在啟動驗證（`DiCore.SetupCore`）被擋下，不會悄悄生效。

*為何這三個值要進配置*：它們與 `Translator.TimeoutSec` 是同一類旋鈕
（「等多久算放棄」），原本一半可配、一半寫死在 `static readonly`，
維護心智分裂（2026-11-16 審查 MAINT-3）。
""")]
public class OptPipeline{

	[Doc("單段音頻生成的超時秒數；超過就把這個語言當失敗。0＝未映射（啟動驗證會擋）。")]
	public i32 GenerateTimeoutSec{get;set;} = 0;

	[Doc("播放不足這個秒數時仍給這麼多超時；避免極短音頻因設備延遲被誤判為卡住。0＝未映射。")]
	public i32 MinPlayTimeoutSec{get;set;} = 0;

	[Doc("在音頻真實長度之外多給的餘量秒數：設備啟動、緩衝、系統繁忙。")]
	public i32 PlayGraceSec{get;set;} = 0;
}
