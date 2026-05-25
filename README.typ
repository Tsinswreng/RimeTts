#import "@preview/tsinswreng-auto-heading:0.1.0": auto-heading
#let H = auto-heading

#H[RimeTts 使用說明][
  RimeTts 是一個輸入法上屏文本朗讀工具。
  Rime Lua 插件把上屏文字寫入文件，C\# 服務按語言流水線逐個翻譯並朗讀。

  #H[AI 輸出格式][
    請在 `rimetts.yaml` 裡自行定義提示詞，AI 自行檢測 `UserInputLang`，回應需為 YAML：
    ```yaml
    UserInputLang: zh
    TargetLang: en
    Translation: Translated Text
    ```
    - `Translation` 不會翻譯時要填 `null`
    - `TargetLang` 不合要求時會直接跳過朗讀並警告
  ]

  #H[配置文件][
    `rimetts.yaml` 與 `RimeTts.Cli.exe` 放同一目錄。
    其中 `Translator.DefaultSystemPrompt`、`LanguagePipeline.Languages[].SystemPrompt` 都可完全自定義，程式不會替你拼接固定提示詞。
    樣例配置文件已直接寫入可編輯的 prompt 範本。
  ]

  #H[操作][
    啟動後持續監聽文件變化。
    `Esc` 可全局中斷當前播放。
  ]
]
