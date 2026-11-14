namespace RimeTts.Test;

using Tsinswreng.CsTreeTest;

[Doc($$"""
測試入口：建樹 → 跑 → 退出碼。

*為何沒有 DI 裝配*：本套用例的被測對象都是純函數或「收介面」的類，
測試自己傳入假實現（見 `Fakes/`）。不建容器就少一份「測試自己的裝配」要維護。
""")]
internal static class Program{

	[Doc("跑完整棵測試樹；有失敗用例時退出碼為 1。")]
	public static async Task<i32> Main(){
		var Tree = RimeTtsTest.MkTree();
		var Summary = await new TreeTestExecutor().RunEtPrint(Tree);
		return Summary.Failed > 0 ? 1 : 0;
	}
}
