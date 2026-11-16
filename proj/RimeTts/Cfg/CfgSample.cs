namespace RimeTts.Cfg;

using RimeTts.Infra;

using System.Text;
using RimeTts.Domains.Translate;
using RimeTts.Infra.Json;

[Doc($$"""
配置樣例的生成。

*為何要自動生成*：首次啟動時沒有配置檔，什麼都不能做；
讓程式自己吐一份可編輯的樣例，比讓使用者去讀文檔手寫一份快得多。
（原本的 YAML 樣例是壞的：它用 tab 縮進，而 YAML 不允許 tab —— 首次啟動必然解析失敗。
這裡改用 jsonr，且由本檔的模板產生，故樣例本身一定有測試盯著。）

*為何是模板而不是「把默認值序列化」*：序列化出來的樣例裡，每個字串都會被改寫成
raw string（連 `"gTTS"` 這種一詞值也是），可讀性反而變差。
故樣例是手寫的模板：**短值用普通引號、長文本（提示詞）用 raw string**，
提示詞的內容則直接取自 {{nameof(DfltLanguageProfiles)}}，不另抄一份。

*樣例的值不是默認值*：樣例給的是「常見部署長什麼樣」（例如 Rime 的用戶資料路徑），
默認值在 {{nameof(KeysAppCfg)}}；兩者刻意不同，故樣例不參與默認值計算。
測試只保證「樣例裡宣告的鍵覆蓋了全部鍵」。
""")]
public static class CfgSample{

	[Doc($$"""
	確保出廠配置檔存在；不存在就寫一份樣例。
	#Prm[{{nameof(BaseDir)}}][應用儲存區根目錄。]
	#Prm[{{nameof(Path)}}][出廠配置檔的實際路徑。]
	#Rtn[這次是否真的寫了檔案（true＝剛剛才生成）。]
	""")]
	public static bool EnsureExists(I_GetBaseDir BaseDir, out str Path){
		Path = BaseDir.Combine(AppCfg.RoCfgFileName);
		if(File.Exists(Path)){
			return false;
		}
		JsonrCfg.WriteTextAtomic(Path, MkDefaultText());
		return true;
	}

	[Doc($$"""
	造一份完整的樣例文本（jsonr）。
	#Rtn[可以直接寫成 `{{nameof(AppCfg.RoCfgFileName)}}` 的文本。]
	""")]
	public static str MkDefaultText(){
		var Langs = DfltLanguageProfiles.MkAll();
		var Blocks = string.Join(",\n", Langs.Select(MkLangBlock));
		return $$"""
// RimeTts 配置檔（jsonr 格式：JSON ＋ // 註釋 ＋ R"詞(…)詞" 原生字串）
// 文字值用 R"詞(…)詞" 包起來的話，引號與換行都不必轉義；短值也可以照常用普通引號。
// 改完存檔、重新啟動程式即生效。

{
	// 程式寫的那份配置檔（本工具目前沒有寫入者，保留這個位置給日後記住運行期發現的事實）
	"RwCfgPath": "rimetts.Rw.jsonr",

	"FileInteractor": {
		// Lua 插件把上屏事件寫進這個檔（整份覆寫）
		"ContentFile": R"P(D:/Program Files/Rime/User_Data/rimetts_content.json)P",
		// 本端監看這個檔；它一變就去讀上面的內容檔
		"SignalFile": R"P(D:/Program Files/Rime/User_Data/rimetts_signal)P"
	},

	"SentenceSeg": {
		// 最後一次上屏後多少毫秒成句。本工具預設只靠這個間隔斷句，不按標點。
		"NoCommitGapMs": 5000,
		// 可選，預設留空（＝不啟用）：上屏以其中之一結尾就立刻成句，不等那 5 秒。
		// 要的話填例如 ["。", "！", "？", ".", "!", "?"]
		"SentenceTerminators": []
	},

	"Translator": {
		// LLM 接口密鑰；不填程式會在啟動時報錯
		"ApiKey": "",
		"BaseUrl": "https://api.deepseek.com/chat/completions",
		"Model": "deepseek-v4-flash",
		// 單次請求超時（秒）
		"TimeoutSec": 20,
		// 譯文快取條目上限；0＝不快取
		"CacheCapacity": 256,
		// 語言條目沒寫提示詞時用它
		"DefaultSystemPrompt": R"PROMPT({{Langs[0].SystemPrompt}})PROMPT"
	},

	"Tts": {
		// 空字串＝執行檔目錄下的 tts-output
		"OutputDir": "",
		// 全域引擎優先級：gTTS（線上，音質好）→ SystemSpeech（離線，一定有）
		"Engines": ["gTTS", "SystemSpeech"]
	},

	"LanguagePipeline": {
		// 順序即播放順序：先英、再日、再西、義、法
		"Languages": [
{{Blocks}}
		]
	},

	"Pipeline": {
		// 單段音頻生成的超時（秒）；超過就當這個語言失敗
		"GenerateTimeoutSec": 40,
		// 極短音頻也至少給這麼多播放超時（秒）
		"MinPlayTimeoutSec": 20,
		// 播放超時在音頻真實長度之外多給的餘量（秒）
		"PlayGraceSec": 10
	}
}
""";
	}

	[Doc($$"""
	造一個語言條目。
	*為何用 `R"PROMPT(…)PROMPT"` 而不是自動挑詞*：樣例是給人看的，定界詞固定才好讀；
	提示詞裡不可能出現 `)PROMPT"`，故不會碰撞。
	#Prm[{{nameof(Profile)}}][語言條目。]
	""")]
	static str MkLangBlock(LanguageProfile Profile){
		return $$"""
			{
				"Language": "{{Profile.Language}}",
				"SystemPrompt": R"PROMPT({{Profile.SystemPrompt}})PROMPT",
				// 空清單＝用上面的全域引擎順序
				"TtsEngines": []
			}
""";
	}
}
