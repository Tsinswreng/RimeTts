namespace RimeTts;

using K = Tsinswreng.CsErr.IErrNode;
using static Tsinswreng.CsErr.ErrNode;

[Doc($$"""
異常鍵族（CsErr 的 {{nameof(IErrNode)}}）。全項目拋錯一律拋這裡的鍵，不拋裸字符串。

*鍵不是文案*：鍵的用途是「可識別的失敗原因」，不是給使用者看的句子。
本專案沒有 i18n 機制，故鍵主要供日誌與「啟動失敗時給一句人話」使用。

*命名約定*：成員名結尾的 `__` 是參數佔位符，`ToErr(參數...)` 會按順序填入。
`Mk` 造出的鍵**不帶任何標籤**——這正是默認語義：可示人的業務異常。
只有「不可示人」的少數情況才自己加 {{nameof(AppErrTags.SysErr)}}（本檔用 {{nameof(MkSys)}} 統一標）。

#Eg[
	```cs
	throw KeysErr.Cfg.ApiKeyEmpty.ToErr();
	throw KeysErr.Cfg.FileMissing__.ToErr(Path);
	```
]
""")]
public static class KeysErr {
	[Doc("根節點。值來自 ErrRoot，此處只是給調用方一個好記的名字。")]
	public static K _Root = ErrRoot.Node;

	[Doc("不分模組的通用錯誤。")]
	public static class Common {
		[Doc("此組的根節點；子鍵都以它為父。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Common)]);

		[Doc($$"""
		參數不合法。佔位符填字段名。
		#Eg[`throw {{nameof(Common)}}.{{nameof(ArgErr__)}}.ToErr(nameof(Options))`]
		""")]
		public static K ArgErr__ = Mk(_R, [nameof(ArgErr__)]);

		[Doc("未預期的失敗。給使用者的回應只說「請稍後重試」，詳細原因看日誌。")]
		public static K UnknownErr = Mk(_R, [nameof(UnknownErr)]);

		[Doc("網絡不通或外部服務不可達。")]
		public static K NetWorkErr = MkSys(_R, [nameof(NetWorkErr)]);
	}

	[Doc("程式自身的啟動期不變式被破壞（不是使用者操作造成的）。")]
	public static class Sys {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Sys)]);

		[Doc("還沒有人給 Di.SvcProvider 賦值就取服務：入口漏了建立容器。")]
		public static K DiNotSetup = MkSys(_R, [nameof(DiNotSetup)]);

		[Doc("同一個行程裡建立了第二個容器：容器必須是進程唯一，否則單例會分裂成兩份。")]
		public static K DiAlreadySetup = MkSys(_R, [nameof(DiAlreadySetup)]);
	}

	[Doc("配置層的錯誤。")]
	public static class Cfg {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Cfg)]);

		[Doc("配置檔的 jsonr 語法壞掉。佔位符填「位置＋原因」。")]
		public static K ParseFailed__ = Mk(_R, [nameof(ParseFailed__)]);

		[Doc("LLM 的 ApiKey 沒填。這是部署錯誤，必須在配置檔裡補。")]
		public static K ApiKeyEmpty = Mk(_R, [nameof(ApiKeyEmpty)]);

		[Doc("引導鍵 RwCfgPath 被寫成空串：那天程式下次就找不到可寫配置檔，屬於手改錯誤。")]
		public static K RwCfgPathEmpty = MkSys(_R, [nameof(RwCfgPathEmpty)]);

		[Doc("配置裡一個語言條目都沒有：流水線無事可做。")]
		public static K NoLanguage__ = Mk(_R, [nameof(NoLanguage__)]);
	}

	[Doc("翻譯層的錯誤。")]
	public static class Translate {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Translate)]);

		[Doc("LLM 的 HTTP 回應不是成功狀態。佔位符填狀態碼。")]
		public static K HttpFailed__ = MkSys(_R, [nameof(HttpFailed__)]);

		[Doc("LLM 回應裡取不到內容（choices／output 都沒有可讀文本）。")]
		public static K ResponseEmpty = MkSys(_R, [nameof(ResponseEmpty)]);
	}

	[Doc("語音合成與播放層的錯誤。")]
	public static class Tts {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Tts)]);

		[Doc("配置裡列出的引擎全部失敗。")]
		public static K AllEnginesFailed = MkSys(_R, [nameof(AllEnginesFailed)]);

		[Doc("要播放的音頻檔不存在。佔位符填路徑。")]
		public static K AudioFileNotFound__ = MkSys(_R, [nameof(AudioFileNotFound__)]);

		[Doc("該語言在本機沒有可用的系統語音。佔位符填語言代碼。")]
		public static K NoVoiceForLang__ = Mk(_R, [nameof(NoVoiceForLang__)]);

		[Doc("本機的 SAPI 引擎不可用（NativeAOT 下它的 COM 包裝類建構子無效）。")]
		public static K SapiUnavailable = MkSys(_R, [nameof(SapiUnavailable)]);

		[Doc("MCI 打不開這個音頻檔（三種 open 寫法都失敗）。")]
		public static K MciOpenFailed__ = MkSys(_R, [nameof(MciOpenFailed__)]);

		[Doc("MCI 命令返回非 0。訊息裡帶命令與錯誤碼文本。")]
		public static K MciCommandFailed__ = MkSys(_R, [nameof(MciCommandFailed__)]);
	}

	[Doc("全域中斷熱鍵（Esc）的錯誤。")]
	public static class Hotkey {
		[Doc("此組的根節點。")]
		public static K _R = Mk(ErrRoot.Node, [nameof(Hotkey)]);

		[Doc("安裝全域鍵盤鉤子失敗。佔位符填 Win32 的錯誤訊息。")]
		public static K EscHookFailed__ = MkSys(_R, [nameof(EscHookFailed__)]);
	}

	[Doc("造一個系統級錯誤鍵：統一加上 SysErr 標籤，避免每個鍵都手寫一次標籤清單。")]
	static K MkSys(K Parent, IReadOnlyList<str> Path){
		return Mk(Parent, Path, [AppErrTags.SysErr]);
	}
}

[Doc($$"""
錯誤鍵樹的根。**刻意獨立成一個類型**，而不是 {{nameof(KeysErr)}} 的成員。

*為何*：分組類（{{nameof(KeysErr)}}.Cfg 等）的鍵要以根為父。若根長在外層 {{nameof(KeysErr)}} 上，
分組類初始化時讀根就會觸發 {{nameof(KeysErr)}} 初始化，而外層類初始化又要碰分組成員——
此時分組類**正在**初始化中，CLR 不允許重入，成員仍是 null。根搬到獨立類型後，兩邊互不牽扯。
""")]
internal static class ErrRoot {
	[Doc("根鍵，路徑 `Error`。")]
	public static K Node = Mk(null, ["Error"]);
}
