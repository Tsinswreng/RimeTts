namespace RimeTts.Scripts;

[Doc($$"""
以 `dotnet run` 直接運行測試專案（非 AOT 的快速驗證）。
""")]
internal static partial class Test{

	[Doc("執行測試；失敗時以非零退出碼結束。")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}

[Doc($$"""
以 win-x64 NativeAOT 發布測試專案並運行發布物，驗證剪裁後測試仍全部通過。

*為何測的是測試專案而不是宿主*：宿主會拖進 NAudio 與 System.Speech（Windows COM），
那兩者能否 AOT 不是本專案能決定的；而測試專案涵蓋了業務程序集裡所有不含原生的邏輯，
足以回答「業務層有沒有引入反射式序列化／動態代碼」這個問題。

*為何發布命令要顯式帶 `-p:PublishAot=true`*：`proj/Directory.Build.props` 全域關掉了 AOT
（理由見該檔），故 AOT 只在這一條命令上開啟。
""")]
internal static partial class TestAotWin{

	[Doc("執行：publish → 運行 publish 目錄下的測試 exe。")]
	internal static partial Task Main(ISCtx Ctx, CT Ct);
}
