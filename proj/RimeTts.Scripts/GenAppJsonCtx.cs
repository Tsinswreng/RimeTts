namespace RimeTts.Scripts;

[Doc($$"""
用手寫源碼生成取代「手寫 JSON 登記清單」的腳本。

*要解決的問題*：`AppJsonCtx` 需要把所有「要以型別往返」的 DTO 登記成
`[JsonSerializable]`。手寫那份清單容易漏（漏了編譯不會錯、AOT 執行期才炸），
每個型別還要多寫一條 `IList<T>` 配對。

*怎麼做*：用 Roslyn（MSBuildWorkspace）載入 `proj/RimeTts/RimeTts.csproj` 取得完整編譯，
掃描標記接口 `RimeTts.Infra.Json.IAppSerializable` 的實現者，
產出單一檔案 `proj/RimeTts/Tsinswreng.SrcGen/RimeTts.Gen.g.cs`。

*為何是手動跑、不是構建時自動跑*：生成物要入庫（否則首次複製倉庫會編不過），
而入庫的檔案一旦由構建改寫，diff 會變得不可讀。故照模板的做法：改完型別自己跑一次。

*怎麼跑*：`dotnet run --project proj/RimeTts.Scripts -- GenAppJsonCtx`
""")]
internal static partial class GenAppJsonCtx{

	[Doc($$"""
	執行一次生成：載入專案 → 收集標記接口的實現者 → 組裝單檔 → 覆寫輸出檔。
	#Prm[{{nameof(Ctx)}}][腳本上下文；用 {nameof(ISCtx.RootDir)} 定位專案與輸出，不依賴當前目錄。]
	#Throw[{{nameof(InvalidOperationException)}}][標記接口找不到，或編譯取不到。]
	""")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}
