namespace RimeTts.Infra.Json;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsinswreng.CsJsonr;

[Doc($$"""
本應用的 JSON 源生成上下文（`System.Text.Json` 源生成）。

*為何一定要有它*：本專案的目標是 NativeAOT 友好，運行時反射式序列化會被裁剪掉、
直接在執行期拋例外。凡是要以型別往返的 DTO 都必須登記，否則症狀是
「本機 debug 正常、AOT 發佈後才炸」。

*登記清單不在這裡*：`[JsonSerializable]` 由 `proj/RimeTts.Scripts` 的 `GenAppJsonCtx`
掃描 {{nameof(IAppSerializable)}} 的實現者後生成到
`proj/RimeTts/Tsinswreng.SrcGen/RimeTts.Gen.g.cs`。
本檔只留「執行期選項」。
**STJ 源生成器不支援把 `[JsonSerializable]` 分散到多個 partial 宣告**，
所以那批標註必須集中在生成檔的同一個宣告上。
""")]
public partial class AppJsonCtx : JsonSerializerContext {
	[Doc($$"""
	本應用序列化一律使用的選項。

	*為何 `PropertyNamingPolicy = null`*：本專案的 JSON 有兩頭——Lua 插件送來的 DTO
	用 PascalCase、LLM 回應的鍵用 PascalCase（由我們自己的提示詞規定）。
	不轉命名最省心；真要換寫法等於改協議，兩端都要同時改。

	*為何 `DefaultIgnoreCondition = WhenWritingNull`*：可空欄位沒值時不出現在 JSON 裡，
	payload 小一點，讀的人也少一層「null 與沒這個欄位有什麼區別」的疑問。

	*為何 `Encoder = UnsafeRelaxedJsonEscaping`*：預設編碼器會把中文寫成 `\uXXXX`，
	合法但難讀、也讓 payload 變大；這裡輸出原樣 UTF-8。

	*為何 `ReadCommentHandling = Skip` 與 `AllowTrailingCommas`*：配置與 LLM 回應都先經
	{{nameof(Jsonr)}} 轉成 JSONC，註釋與尾逗號是那種格式的正常產物，不該在解析這一步被拒。

	*為何 `TypeInfoResolver = Default` 這一行不能省*：NativeAOT 下沒有反射式 resolver 兜底，
	漏了它等於整個序列化都失效。
	""")]
	public static JsonSerializerOptions WireOptions => field ??= MkWireOptions();

	[Doc($$"""
	建序列化用的那份選項。
	#Rtn[已掛好源生成 resolver 的選項。]
	#Throw[{{nameof(InvalidOperationException)}}][resolver 沒掛上時；那代表 AOT 下會靜默退化成反射。]
	""")]
	static JsonSerializerOptions MkWireOptions() {
		var Opt = new JsonSerializerOptions {
			PropertyNamingPolicy = null,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			// DTO 來自別的進程（Lua）與別人的模型（LLM），大小寫不該成為失敗原因。
			PropertyNameCaseInsensitive = true,
			// 這一行是本類存在的理由：讓「只帶 options」的序列化重載在 AOT 下仍走源生成。
			TypeInfoResolver = Default,
		};
		// 守衛：resolver 為 null 時，非 AOT 環境會「看起來正常」（退回反射），
		// AOT 環境則在第一次序列化才炸。寧可在建構期就炸，也不要留到發佈後。
		if (Opt.TypeInfoResolver is null) {
			throw new InvalidOperationException(
				$"{nameof(AppJsonCtx)}.{nameof(WireOptions)} 沒有掛上 TypeInfoResolver："
				+ "源生成 context 未就緒。這通常意味著靜態初始化順序有問題。"
			);
		}
		return Opt;
	}
}
