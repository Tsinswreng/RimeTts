namespace RimeTts;

using Microsoft.Extensions.DependencyInjection;

[Doc($$"""
進程唯一的服務容器，以及兩個取用入口。

*為何要有它*：不方便建構子注入的場合（靜態工具、文件日誌器的延遲路徑解析、
啟動早期的日誌寫入）也要拿到依賴。全局門面讓那些地方寫一行 {{nameof(Di)}}.GetRSvc<T>()，
而不必到處傳 {{nameof(IServiceProvider)}}。

*為何未設定時拋而不是回 null*：取不到容器是啟動順序寫錯（入口忘了賦值），
那不是可恢復狀態；回 null 只會讓錯誤延到某個更遠的地方才爆。

#Eg[
	```cs
	// 入口
	Di.SvcProvider = MkServices();

	// 任何地方
	var Tts = Di.GetRSvc<ITts>();
	```
]
""")]
public class Di{

	[Doc("容器的後備欄位；null 表示還沒有人設定。")]
	static IServiceProvider? ProviderField;

	[Doc($$"""
	進程唯一的容器。未設定時取用會拋。
	#Throw[{{nameof(TypedErr)}}][容器尚未建立（鍵為 {{nameof(KeysErr.Sys.DiNotSetup)}}）時。]
	""")]
	public static IServiceProvider SvcProvider{
		get{
			return ProviderField ?? throw KeysErr.Sys.DiNotSetup.ToErr();
		}
		set{
			ProviderField = value ?? throw new ArgumentNullException(nameof(value));
		}
	}

	[Doc($$"""
	容器不存在時回 null 而不是拋。

	*為何需要它*：測試與「宿主還沒建容器」的場合會用到；
	那種場合應該走型別自己的最小構造，而不是讓整個流程炸在解析上。
	""")]
	public static IServiceProvider? SvcProviderOrNull => ProviderField;

	[Doc($$"""
	設定進程唯一的容器；**重複設定直接拋**。

	*為何要守衛*：允許覆蓋的話，舊物件會綁在舊容器的單例上、新物件綁在新容器上，
	同一份狀態分裂成兩份，而且症狀離成因很遠。容器是進程級的，第二次設定一定是生命週期寫錯。
	#Prm[{{nameof(Provider)}}][已建好的容器。]
	#Throw[{{nameof(TypedErr)}}][容器已存在（鍵為 {{nameof(KeysErr.Sys.DiAlreadySetup)}}）時。]
	""")]
	public static void SetSvcProvider(IServiceProvider Provider){
		if(SvcProviderOrNull is not null){
			throw KeysErr.Sys.DiAlreadySetup.ToErr();
		}
		SvcProvider = Provider;
	}

	[Doc($$"""
	取一個已註冊的服務；沒註冊或容器未建立就拋。
	#Prm[{{nameof(T)}}][要解析的服務型別。]
	#Rtn[容器裡的實例。]
	""")]
	public static T GetRSvc<T>()
		where T : class
	{
		return SvcProvider.GetRequiredService<T>();
	}

	[Doc($$"""
	優先取容器中的註冊，沒有就用型別自己宣告的最小構造方式。

	*為何不用 `ActivatorUtilities`*：那個走反射，AOT 裁剪後可能失效；
	{{nameof(IMk<T>)}} 是編譯期就查得到的約定。
	""")]
	public static T DiOrMk<T>()
		where T : class, IMk<T>
	{
		var Svc = SvcProviderOrNull?.GetService<T>();
		return Svc ?? T.Mk();
	}
}
