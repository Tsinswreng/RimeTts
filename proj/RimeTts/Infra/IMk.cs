namespace RimeTts;

[Doc($$"""
「沒有依賴注入也要能造出自己」這條約定的介面。

*為何不用 `ActivatorUtilities`*：那條路走反射，NativeAOT 裁剪後可能失效；
實作 {{nameof(Mk)}} 是編譯期就查得到的約定，也能讓「最小構造」寫在型別自己身上。
""")]
public interface IMk<T>
	where T : class
{
	[Doc("造一個最小可用的實例：不依賴容器、不讀配置。")]
	static abstract T Mk();
}
