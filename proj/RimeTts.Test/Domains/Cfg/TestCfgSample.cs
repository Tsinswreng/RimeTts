using RimeTts.Infra.Cfg;
using Tsinswreng.CsTreeTest;

namespace RimeTts.Test.Domains.Cfg;

[Doc($$"""
本分片測 {{nameof(CfgSample)}}：程式吐出來的那份樣例配置。

*為何要測生成的樣例*：舊版是手寫的 YAML 樣例，用 tab 縮進（YAML 不允許），
首次啟動必定解析失敗——而這種錯只有「真的生成一次再讀它」才抓得到。
""")]
public partial class TestCfg{

	[Doc("把本分片的用例掛到測試類的節點上。")]
	public partial void RegisterSample();

	[Doc("樣例必須是合法 jsonr，且能被解析成非空的配置樹。")]
	public partial Task<nil> Sample_Parses(obj? O);

	[Doc("樣例必須覆蓋全部配置鍵（鍵改名而樣例沒改時，這條會失敗）。")]
	public partial Task<nil> Sample_CoversAllKeys(obj? O);

	[Doc("用樣例映射出來的語言清單必須與出廠清單一致（順序、語言代碼、提示詞）。")]
	public partial Task<nil> Sample_MatchesDefaultLanguages(obj? O);

	[Doc("樣例的成句規則必須是「只按間隔」，不得偷偷打開按標點斷句。")]
	public partial Task<nil> Sample_KeepsGapOnlySegmentation(obj? O);
}
