namespace RimeTts.Infra.Json;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Tsinswreng.CsJson;

[Doc($$"""
全應用共用的 JSON 序列化門面。

*為何要有它*：業務層到處都有靜態類與不經容器的場合，沒辦法在建構子注入
{{nameof(Tsinswreng.CsJson.IJsonSerializer)}}；若各處自己 `new JsonSerializer(options)`，
配置就散了。故這裡給一個「不方便注入時直接用」的入口，而容器註冊的是*同一個實例*
（見 {{nameof(DiCore)}}），兩條路徑不會拿到兩份不一樣的選項。

*為何不直接調 `System.Text.Json.JsonSerializer` 靜態類*：那條路徑只吃 options，
在 NativeAOT 下必須 options 自帶源生成 resolver 才安全。門面把這件事收在一處。

#Eg[
	```cs
	// 靜態場合
	var Json = AppJson.Inst.Serialize(Foo);

	// 能注入的場合：注入 IJsonSerializer，拿到的是同一份
	public class SvcSome(IJsonSerializer Json) { /* … */ }
	```
]
""")]
public static class AppJson {
	[Doc($$"""
	全局唯一的 {{nameof(Tsinswreng.CsJson.IJsonSerializer)}}；選項固定用 {{nameof(AppJsonCtx.WireOptions)}}。

	*為何延遲建立*：{{nameof(AppJsonCtx)}} 的選項本身也要先組好，
	用 `field ??=` 讓它第一次被用到時才建，就沒有「誰先誰後」的問題。
	""")]
	public static IJsonSerializer Inst => field ??= new Tsinswreng.CsJson.JsonSerializer(AppJsonCtx.WireOptions);

	[Doc($$"""
	以型別元資料讀一段 JSON 文本。

	*為何要收 {{nameof(JsonTypeInfo)}} 而不是直接 `Read<T>`*：只帶 options 的重載在 AOT 下走反射，
	編譯期就報 IL2026／IL3050。收型別元資料等於強迫呼叫方指出「用生成檔裡哪一個」，
	於是 AOT 安全是編譯期保證的，而不是靠自覺。

	#Prm[{{nameof(JsonText)}}][UTF-8 或轉義後的 JSON 文本。]
	#Prm[{{nameof(Info)}}][如 `{{nameof(AppJsonCtx)}}.Default.ResLlm`。]
	#Rtn[反序列化結果；文本是 `null` 時回 null。]
	""")]
	public static T? Read<T>(str JsonText, JsonTypeInfo<T> Info){
		return Inst.Deserialize(JsonText, Info);
	}
}
