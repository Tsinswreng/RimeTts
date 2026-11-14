using Tsinswreng.CsSh;
using static Tsinswreng.CsSh.ShGlobal;

namespace RimeTts.Scripts;

// Test 命令的流程實現。聲明見 Test.cs。
internal static partial class Test{

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		// dotnet run 會自動還原並建置；輸出必須轉送終端，否則看不到測試結果。
		await Exe("dotnet", ["run", "--project", Ctx.RootDir/"proj/RimeTts.Test/RimeTts.Test.csproj"], Ct);
	}
}

// TestAotWin 命令的流程實現。聲明見 Test.cs。
internal static partial class TestAotWin{

	internal static async partial Task Main(ISCtx Ctx, CT Ct){
		var TestProject = Ctx.RootDir/"proj/RimeTts.Test/RimeTts.Test.csproj";

		// step 1: 以 Release 配置發布 win-x64 NativeAOT 測試 executable。
		await Exe("dotnet", [
			"publish", TestProject
			,"-c", "Release"
			,"-r", "win-x64"
			,"-p:AotTest=true"
		], Ct);

		// step 2: 運行 publish 目錄中的原生測試程式；目錄結構固定為 bin/Release/{Tfm}/{Rid}/publish。
		var PublishExe = Ctx.RootDir/"proj/RimeTts.Test/bin/Release"/Ctx.Tfm/"win-x64/publish/RimeTts.Test.exe";
		await Exe(PublishExe, [], Ct);
	}
}
