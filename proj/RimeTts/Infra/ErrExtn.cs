namespace RimeTts.Infra;

[Doc($$"""
錯誤鍵的便捷擴展。

*為何要有 {{nameof(WithMsg)}}*：{{nameof(Tsinswreng.CsErr.ErrNodeExtn.ToErr)}} 造出的
{{nameof(TypedErr)}} 只有鍵、沒有訊息，於是 `ex.Message` 會是 .NET 的預設那句
「Exception of type ... was thrown」——那不是給人看的東西。
啟動期的配置錯誤要能直接告訴使用者「哪個鍵沒填」，故在這裡補一句人話。

*為何不每個拋錯點自己 `new TypedErr(...)`*：那會把「造錯誤」這件事散開，
而標籤、DebugArgs 的約定就會各寫各的。收成一個擴展之後，鍵仍然是鍵，訊息只是附加說明。

#Eg[
	```cs
	throw KeysErr.Cfg.ApiKeyEmpty.WithMsg(Todo.I18n("配置缺少 Translator.ApiKey。"));
	```
]
""")]
public static class ErrExtn{

	[Doc($$"""
	以鍵造錯誤，並附一句可示人的說明。
	#Prm[{{nameof(z)}}][錯誤鍵。]
	#Prm[{{nameof(Message)}}][給人看的說明；寫進 {{nameof(Exception.Message)}}，不影響鍵與標籤。]
	#Rtn[可直接 `throw` 的 {{nameof(TypedErr)}}。]
	""")]
	public static TypedErr WithMsg(this IErrNode z, str Message){
		return new TypedErr(Message, null){
			Type = z,
		};
	}
}
