#import "@preview/tsinswreng-auto-heading:0.1.0": auto-heading
#let H = auto-heading


#H[RimeTts 使用說明][

  RimeTts 是一個輸入法上屏文本朗讀工具。
  Rime 輸入法的 Lua 插件將上屏文字通過文件傳給 C\# 服務，C\# 服務將中文句子用 LLM 翻譯成英文，再用 gTTS 合成語音並播放。

  #H[前置要求][

    - Windows（目前僅支持 Windows）
  - 能連通 Google TTS HTTP 服務（網路可用）
    - LLM API Key（兼容 OpenAI 協議的接口均可，推薦使用響應速度快的模型如 `gpt-4o-mini`）

  ]

  #H[配置文件][

    配置文件名為 `rimetts.yaml`，須與 `RimeTts.Cli.exe` 放在同一目錄下。

    首次運行時若配置文件不存在，程序會自動生成一份樣本配置文件並退出，填寫完畢後重新運行即可。

    #H[配置文件模板][

      ```yaml
      # RimeTts 配置文件
      # 配置文件必须与 EXE 在同一目录下

      FileInteractor:
        # Lua 侧写入内容的 JSON 文件路径
        ContentFile: "C:\\tmp\\rimetts_content.json"
        # Lua 侧触发信号的文件路径（C# 监听此文件变化）
        SignalFile: "C:\\tmp\\rimetts_signal"

      SentenceSeg:
        # 最后一次上屏后多少毫秒认为一个句子结束，默认 5000
        NoCommitGapMs: 5000

      Translator:
        # LLM API Key
        ApiKey: "sk-xxxxxxxxxxxxxxxx"
        # API 地址（兼容 OpenAI 协议的接口均可）
        BaseUrl: "https://api.openai.com/v1/chat/completions"
        # 模型名（推荐使用响应速度快的）
        Model: "gpt-4o-mini"
        # 请求超时秒数
        TimeoutSec: 20
        # 翻译系统提示词
        SystemPrompt: "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text."

      Tts:
    # TTS 引擎優先級，越靠前越先嘗試
    # 默認先 gTTS；若失敗則回退 SystemSpeech
    Engines: ["gTTS", "SystemSpeech"]
        # 生成的音频文件输出目录（留空则使用 EXE 目录下 tts-output）
        OutputDir: ""
      ```

    ]

    #H[配置項說明][

      #table(
        columns: (auto, auto, auto),
        [*配置項*], [*默認值*], [*說明*],
        [`FileInteractor.ContentFile`], [無，必填], [Lua 側寫入上屏內容的 JSON 文件路徑],
        [`FileInteractor.SignalFile`], [無，必填], [Lua 側觸發信號的文件路徑，C\# 監聽此文件變化],
        [`SentenceSeg.NoCommitGapMs`], [`5000`], [最後一次上屏後多少毫秒認為句子結束],
        [`Translator.ApiKey`], [無，必填], [LLM API Key],
        [`Translator.BaseUrl`], [OpenAI 官方地址], [API 地址，兼容 OpenAI 協議均可],
        [`Translator.Model`], [`gpt-4o-mini`], [模型名],
        [`Translator.TimeoutSec`], [`20`], [翻譯請求超時秒數],
        [`Translator.SystemPrompt`], [見模板], [翻譯系統提示詞],
        [`Tts.Engines`], [`["gTTS","SystemSpeech"]`], [TTS 引擎優先級列表，前者優先；失敗時按順序回退],
        [`Tts.OutputDir`], [EXE 目錄下 `tts-output`], [合成音頻的緩存目錄],
      )

    ]

  ]

  #H[運行][

    ```
    RimeTts.Cli.exe
    ```

    程序啓動後持續運行，監聽 `FileInteractor.SignalFile` 文件變化。Lua 插件每次上屏時寫入 `ContentFile` 並觸碰 `SignalFile`，C\# 側讀取後完成翻譯與播放。

  ]

  #H[工作流程][

    + Rime Lua 插件上屏時向 `ContentFile` 寫入 JSON（格式見下），並更新 `SignalFile`。
    + C\# 側讀取上屏文本，按 `NoCommitGapMs` 間隔聚合成句子。
    + 句子完成後調用 LLM 翻譯為英文（帶緩存，相同原文不重複請求）。
    + 調用 gTTS 合成英文語音 MP3（帶緩存，相同譯文不重複生成）。
    + 串行播放，前一段播完才播下一段。

  ]

  #H[Lua 側 JSON 格式][

    上屏事件：
    ```json
    { "Type": "Commit", "Text": "你好世界" }
    ```

    按鍵事件（用於重置句子計時）：
    ```json
    { "Type": "KeyEvent" }
    ```

  ]
]



#H[TODO][
	改日誌輸出細度、每次收到上屏詞時都應該在日誌中顯示
	處理回車/空格等、使AI見之
	上下文、保持數個歷史句、㕥增譯ʹ精度
]
