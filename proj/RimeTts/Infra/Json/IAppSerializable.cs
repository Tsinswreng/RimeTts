namespace RimeTts.Infra.Json;

[Doc($$"""
標記接口：實現它 ＝ 這個型別要進 JSON 源生成上下文（{{nameof(AppJsonCtx)}}）。

*為何要有這層標記*：NativeAOT 下沒有反射式序列化，凡是要以型別往返的 DTO
都必須在 {{nameof(AppJsonCtx)}} 上有 `[JsonSerializable]`。手寫那份清單早晚會漏
（漏了不會編譯錯，只在 AOT 執行期才炸），故改成「型別自己聲明」＋「腳本掃描生成」。

*誰在掃*：`proj/RimeTts.Scripts` 的 `GenAppJsonCtx`——用 Roslyn 載入本專案、
收集所有實現／繼承本接口的型別，產出
`proj/RimeTts/Tsinswreng.SrcGen/RimeTts.Gen.g.cs`。

*本專案只登記「自己定義、確實要以型別往返」的 DTO*：Lua 插件送來的上屏事件、
LLM 回應的內容本體。對外部 API 的**包絡**（OpenAI 風格的 choices／output）形狀是多態的，
用 `JsonDocument` 逐層取，不登記成強型別——強型別化只會變成一堆可空字段加特判。
""")]
public interface IAppSerializable{
}
