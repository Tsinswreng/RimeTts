#import "@preview/tsinswreng-auto-heading:0.1.0": auto-heading
#let H = auto-heading

#H[這是什麼][
	RimeTts 是一個「打字即朗讀」的工具。

	Rime 輸入法的 Lua 插件把每次上屏的字寫進檔案；本程式監看那個檔案，
	把逐字逐詞的上屏累積成句、翻成若干種語言、合成語音並依序播放。

	它自己沒有界面：一個常駐的命令列進程，配上日誌檔與音頻快取目錄。
]

#H[運作方式][
	+ Lua 側：每次上屏把 `{ "Type": "Commit", "Text": "上屏的字" }` 整份覆寫進
	`ContentFile`，然後改寫 `SignalFile`（時間戳或大小變了就行，內容無意義）。
	+ 本端：`FileSystemWatcher` 監看 `SignalFile`；一有變動就去讀 `ContentFile`。
	讀到的是半截 JSON 時會重試幾次——Lua 那邊不是原子寫入，這是協議的一部分。
	+ 累積：上屏逐次追加到緩衝。上屏以終止符結尾，或距上次上屏超過
	`SentenceSeg.NoCommitGapMs`，就把緩衝當成一句。
	+ 翻譯：一句話同時送給所有配置的語言（並行），每個語言用自己的提示詞。
	+ 合成：各語言的音頻也並行生成；生成失敗或超時只影響那一個語言。
	+ 播放：嚴格依配置順序、一段播完才播下一段（音效卡是獨佔資源）。
	全程序按 `Esc` 可打斷當前播放。
]

#H[配置檔][
	檔名 `rimetts.jsonr`，與 `RimeTts.Cli.exe` 同目錄。首次啟動若不存在，
	程式會自己生成一份可編輯的樣例然後退出——填好 `Translator.ApiKey` 再啟動即可。

	格式是 *jsonr*：JSON 加上 `//` 註釋、加上 C++ 風格的生字串 `R"詞(…)詞"`。
	提示詞那種多行文本用生字串包起來，引號與換行都不必轉義：

	```jsonr
	"LanguagePipeline": {
		"Languages": [
			{
				"Language": "en",
				"SystemPrompt": R"PROMPT(You are a fast translator.

Return jsonr only. Pick a random delimiter for the raw string in every answer.)PROMPT",
				"TtsEngines": []
			}
		]
	}
	```

	*鍵都在 `proj/RimeTts/Infra/Cfg/KeysAppCfg.cs`*：那裡有每個鍵的型別、默認值與說明，
	是默認值的唯一出處。配置裡沒寫的鍵一律回鍵上的默認值，所以最小的配置只要填
	`FileInteractor` 兩個路徑與 `Translator.ApiKey`。

	#H[兩份配置][
		出廠那份 `rimetts.jsonr` 是手寫的、只讀，可以帶註釋；
		另有一份程式寫的 `rimetts.Rw.jsonr`（位置由鍵 `RwCfgPath` 決定），是唯一可寫源。

		*目前沒有寫入者*：本工具沒有設定界面，故那一份現階段不會被建立。
		保留它是為了日後要記住「運行期發現的事實」時有地方落盤，
		而不必去改那份手寫的配置。
	]
]

#H[LLM 回覆格式][
	提示詞要模型回 jsonr，形狀固定為三個鍵：

	```jsonr
	{
		"UserInputLang": "zh",
		"TargetLang": "en",
		"Translation": R"q7Zx(Translated Text)q7Zx"
	}
	```

	+ `TargetLang` 與要求的語言不一致時，這次翻譯算失敗（只跳過那個語言）。
	+ `Translation` 為 `null` 表示「翻不了」，同樣只跳過那個語言。
	+ 模型自己加的 markdown 圍欄會被剝掉；回一堆廢話則解析失敗，記日誌、不朗讀。

	*為何要生字串*：譯文裡常有引號與換行。用 `R"詞(…)詞"` 包起來，
	模型不必轉義、我們也不必在解析時還原轉義，兩邊都少一類錯誤。

	*定界詞必須每次由模型自己造*：生字串的結束條件是「遇到 `)詞"`」，
	所以定界詞若寫死（例如大家都愛用的 `EOF`），譯文裡只要出現 `)EOF"`——
	翻的是一段程式碼、或原文本身就含這種字串——解析會在那裡**提前收尾**，
	剩下的內容全變成垃圾，這個語言直接翻不出來。
	故出廠提示詞明確要求：每次回覆新造一個英數字定界詞、不得重用範例或前一次的詞、
	且該詞不得出現在譯文裡。範例給的是 `q7Zx` 這種顯然是隨機的詞，
	免得模型照抄一個「看起來像固定用法」的 `EOF`。

	提示詞本身可以直接改（`Translator.DefaultSystemPrompt` 與每個語言條目的
	`SystemPrompt`）。程式不會替你拼接任何固定提示詞——那幾段文字就是完整的系統提示詞。
	出廠的六份提示詞集中在 `proj/RimeTts/Domains/Translate/LlmPrompts.cs`（英文規則與日文規則各一份），
	改格式規則只改那一處。
]

#H[操作][
	+ 啟動：`RimeTts.Cli.exe`（或 `dotnet run --project proj/RimeTts.Cli`）。
	+ 停止：`Ctrl+C`。
	+ 打斷當前朗讀：全域 `Esc`（低級鍵盤鉤子，不需要視窗有焦點；它只旁聽，不改按鍵行為）。
	+ 日誌：`logs/RimeTts.log`（一直追加、不輪替）。
	*主控台只印重要日誌*（上屏詞、成句、譯文），其餘（HTTP、失敗堆棧、除錯）
	只寫檔案——這是「邊打字邊聽」時不想被淹掉的那一半。
]

#H[專案結構][
	+ `proj/RimeTts`：業務程序集。根目錄放 `Di`／`AppLog`／`KeysErr`／`AppErrTags`／`Todo`
	（免 using 的符號）；`Infra/` 是程序集地基（`Cfg`／`Json`／`Log`／`Os`）；
	`Domains/` 是業務功能（`Typing`／`Seg`／`Translate`／`Tts`／`Pipeline`）。
	+ `proj/RimeTts.Cli`：宿主。只做「建容器、跑流水線」；裝配在 `DiCli`。
	+ `proj/RimeTts.Test`：樹型測試（CsTreeTest）。
	+ `proj/RimeTts.Scripts`：維護腳本（`Test`／`TestAotWin`／`GenAppJsonCtx`／`PubWinAot`）。

	目錄判據照 `TswgCsTpl_App`：業務功能進 `Domains/`、程序集地基進 `Infra/`、
	要免 using 的符號放根命名空間。
]

#H[建置與驗證][
	```sh
	cd RimeTts
	dotnet build proj/RimeTts.Cli/RimeTts.Cli.csproj -m:1
	dotnet run --project proj/RimeTts.Test
	dotnet run --project proj/RimeTts.Scripts -- Test
	dotnet run --project proj/RimeTts.Scripts -- TestAotWin
	dotnet run --project proj/RimeTts.Scripts -- GenAppJsonCtx
	dotnet run --project proj/RimeTts.Scripts -- PubWinAot
	```

	+ `GenAppJsonCtx`：改了「要以型別往返的 DTO」之後跑一次，它會重寫
	`proj/RimeTts/Tsinswreng.SrcGen/RimeTts.Gen.g.cs`。生成物要入庫。
	+ `TestAotWin`：以 NativeAOT 發布測試專案再跑一次，用來檢查有沒有偷偷依賴反射式序列化。
	2026-11-14 實測：發布成功，發布物上 34 個用例全過（唯一警告是測試自己用反射列舉鍵，
	那屬測試專屬、可接受）。
	+ `PubWinAot`：發布**要交出去的那一份**——`dist/win-x64/` 的自包含目錄（實測 43 MiB），
	目標機器不必裝 .NET。選項：`-Out <目錄>`、`-NoAot`（逃生門：改發 framework-dependent）、
	`-DryRun`（只印命令）。首次執行會在該目錄生成 `rimetts.jsonr`。

	#H[AOT 的三個開關與一個坑][
		`PublishAot` 一律由專案內的條件區塊打開、由各自的屬性名控制：
		`-p:AotTest=true`（測試專案）、`-p:AotPub=true`（宿主）。**不要**用全域
		`-p:PublishAot=true`——那是命令列全域屬性，會傳染給被引用的 CsCore，
		而它有一個 netstandard2.0 目標，那個目標不支援 AOT，整個發布會在
		`TargetFramework=netstandard2.0` 那裡以 NETSDK1207 失敗。

		#H[已知限制：離線 SAPI 引擎在 AOT 版上很可能不可用][
			宿主 AOT 發布時，ILC 明確報告 `System.Speech` 的三個 SAPI COM 包裝類
			（`SpObjectTokenCategory`、`SpDataKey`、`SpObjectToken`）「建構子永遠拋：
			Invalid IL or CLR metadata」——那是 COM 類型在 AOT 下沒有運行期生成的元資料所致。
			線上那條腿（gTTS）不受影響，故 AOT 版仍可用；但「網絡斷了就退回本機語音」
			這個保險在 AOT 版上不成立。要為 AOT 版調整引擎清單或加啟動時探測，尚未拍板。
		]
	]
]

#H[已知限制][
	+ Windows only：全域 Esc 鉤子、NAudio 播放、SAPI 合成都是 Windows 專屬。
	非 Windows 上程式會直接拒絕啟動，而不是半殘地跑起來。
	+ 沒有測試涵蓋真實音頻輸出與 LLM 往返：那兩件事要網絡、要音效卡，
	故測試只涵蓋解析、映射、排程與換手邏輯（34 個用例）。
	+ 譯文快取只在記憶體裡，重啟即失；音頻快取在 `tts-output/`，依「語言＋文本」雜湊命名。
	+ AOT 版的離線語音（SAPI）很可能不可用，見上面「AOT 的三個開關與一個坑」。
]
