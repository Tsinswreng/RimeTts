#import "@preview/tsinswreng-auto-heading:0.1.0": auto-heading
#let H = auto-heading


#H[RimeTts 使用說明][

  RimeTts 是一個輸入法上屏文本朗讀工具。
  Rime 輸入法的 Lua 插件將上屏文字通過文件傳給 C\# 服務，C\# 服務將中文句子用 LLM 翻譯成英文，再用 gTTS 合成語音並播放。

  #H[前置要求][

    - Windows（目前僅支持 Windows）
    - Python 3.12（需要安裝 `gtts` 包）
    - Python 可執行文件與 DLL 路徑已知（如 `d:\ENV\python312\python312.dll`）
    - LLM API Key（兼容 OpenAI 協議的接口均可，推薦使用響應速度快的模型如 `gpt-4o-mini`）

    安裝 gTTS：
    ```
    python -m pip install gtts
    ```

  ]

  #H[配置文件][

    配置文件名為 `rimetts.yaml`，須與 `RimeTts.Cli.exe` 放在同一目錄下。

    首次運行時若配置文件不存在，程序會自動生成一份樣本配置文件並退出，填寫完畢後重新運行即可。

    #H[配置文件模板][

      ```yaml
      # RimeTts 配置文件
      # 配置文件必须与 EXE 在同一目录下

      fileInteractor:
        # Lua 侧写入内容的 JSON 文件路径
        contentFile: "C:\\tmp\\rimetts_content.json"
        # Lua 侧触发信号的文件路径（C# 监听此文件变化）
        signalFile: "C:\\tmp\\rimetts_signal"

      sentenceSeg:
        # 最后一次上屏后多少毫秒认为一个句子结束，默认 5000
        noCommitGapMs: 5000

      translator:
        # LLM API Key
        apiKey: "sk-xxxxxxxxxxxxxxxx"
        # API 地址（兼容 OpenAI 协议的接口均可）
        baseUrl: "https://api.openai.com/v1/chat/completions"
        # 模型名（推荐使用响应速度快的）
        model: "gpt-4o-mini"
        # 请求超时秒数
        timeoutSec: 20
        # 翻译系统提示词
        systemPrompt: "You are a fast translator. Translate Chinese to concise natural English only. Return only translation text."

      tts:
        # Python DLL 路径
        pythonDllPath: "d:\\ENV\\python312\\python312.dll"
        # 生成的音频文件输出目录（留空则使用 EXE 目录下 tts-output）
        outputDir: ""
      ```

    ]

    #H[配置項說明][

      #table(
        columns: (auto, auto, auto),
        [*配置項*], [*默認值*], [*說明*],
        [`fileInteractor.contentFile`], [無，必填], [Lua 側寫入上屏內容的 JSON 文件路徑],
        [`fileInteractor.signalFile`], [無，必填], [Lua 側觸發信號的文件路徑，C\# 監聽此文件變化],
        [`sentenceSeg.noCommitGapMs`], [`5000`], [最後一次上屏後多少毫秒認為句子結束],
        [`translator.apiKey`], [無，必填], [LLM API Key],
        [`translator.baseUrl`], [OpenAI 官方地址], [API 地址，兼容 OpenAI 協議均可],
        [`translator.model`], [`gpt-4o-mini`], [模型名],
        [`translator.timeoutSec`], [`20`], [翻譯請求超時秒數],
        [`translator.systemPrompt`], [見模板], [翻譯系統提示詞],
        [`tts.pythonDllPath`], [無，必填], [Python DLL 路徑，如 `python312.dll`],
        [`tts.outputDir`], [EXE 目錄下 `tts-output`], [合成音頻的緩存目錄],
      )

    ]

  ]

  #H[運行][

    ```
    RimeTts.Cli.exe
    ```

    程序啓動後持續運行，監聽 `fileInteractor.signalFile` 文件變化。Lua 插件每次上屏時寫入 `contentFile` 並觸碰 `signalFile`，C\# 側讀取後完成翻譯與播放。

  ]

  #H[工作流程][

    + Rime Lua 插件上屏時向 `contentFile` 寫入 JSON（格式見下），並更新 `signalFile`。
    + C\# 側讀取上屏文本，按 `noCommitGapMs` 間隔聚合成句子。
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
