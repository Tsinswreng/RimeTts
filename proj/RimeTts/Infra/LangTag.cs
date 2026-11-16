namespace RimeTts.Infra;

using System.Globalization;

[Doc($$"""
語言代碼的歸一化與文化對應。

*為何要收成一處*：同一個 `jp→ja` 的別名糾正原本在兩處各寫一遍（翻譯器與流水線），
一處改了另一處就漂移；而 `System.Speech` 又要用完全不同的「文化」表示法。
三種表示法（配置裡的、LLM 回的、SAPI 要的）之間的換算集中在本類。

*為何沒有 `zh-TW → zh-Hant` 這種映射*：那是書寫系統的選擇，不是別名糾正；
本工具只認 BCP-47 語言子標籤，寫錯就照原樣傳下去，讓引擎自己失敗。
""")]
public static class LangTag{

	[Doc($$"""
	歸一化：去空白、轉小寫、糾正常見別名（`jp` → `ja`）。
	空輸入回空串（呼叫方據此判斷「沒配置」）。
	#Eg[
		```cs
		LangTag.Normalize(" JA ")  // "ja"
		LangTag.Normalize("jp")    // "ja"
		```
	]
	""")]
	public static str Normalize(str? Tag){
		if(str.IsNullOrWhiteSpace(Tag)){
			return "";
		}
		var Norm = Tag.Trim().ToLowerInvariant();
		return Norm switch{
			"jp" => "ja",
			_ => Norm,
		};
	}

	[Doc($$"""
	歸一化並保底：空輸入回 `en`。
	*為何要它*：「歸一化，空則退 en」原本在翻譯器與兩個 TTS 引擎三處各寫一遍，
	`en` 這個保底值也出現三次——一處改了其餘就漂移（2026-11-16 審查 MAINT-1 收攏）。
	#Eg[
		```cs
		LangTag.Ensure(" JP ")  // "ja"
		LangTag.Ensure("")      // "en"
		```
	]
	""")]
	public static str Ensure(str? Tag){
		return Normalize(Tag) is { Length: > 0 } Norm ? Norm : "en";
	}

	[Doc($$"""
	取 BCP-47 語言代碼對應的 {{nameof(CultureInfo)}}；取不到回 null。

	*為何幾個常見語言要寫死完整文化*：`GetCultureInfo("ja")` 得到的是中性文化，
	SAPI 選語音時要具體地區（`ja-JP`）才對得上。其餘語言交給 BCL 自己解。
	#Prm[{{nameof(Tag)}}][語言代碼，會先經過 {{nameof(Normalize)}}。]
	#Rtn[對應文化；代碼不合法時回 null，不拋。]
	""")]
	public static CultureInfo? TryCulture(str? Tag){
		var Norm = Normalize(Tag);
		if(Norm.Length == 0){
			return null;
		}
		try{
			return Norm switch{
				"ja" => CultureInfo.GetCultureInfo("ja-JP"),
				"en" => CultureInfo.GetCultureInfo("en-US"),
				"zh" => CultureInfo.GetCultureInfo("zh-CN"),
				_ => CultureInfo.GetCultureInfo(Norm),
			};
		}
		catch(CultureNotFoundException){
			return null;
		}
	}

	[Doc($$"""
	兩個語言代碼是否指同一種語言（都先歸一化）。
	#Rtn[相等回 true；其中一方空則 false。]
	""")]
	public static bool Same(str? A, str? B){
		var NA = Normalize(A);
		var NB = Normalize(B);
		return NA.Length != 0 && NB.Length != 0 && str.Equals(NA, NB, StringComparison.OrdinalIgnoreCase);
	}
}
