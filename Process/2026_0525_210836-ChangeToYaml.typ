
改一下RimeTts。
你讓大模型輸出yaml格式的
如
```yaml
UserInputLang: zh # 用戶輸入文字的語種。都用BCP-47標準的語種代碼
TargetLang: en # 目標語言的語種 (用戶要求你翻譯成的語種)
Translation: Translated Text
```

把c\#代碼的處理方式 和 提示詞 都改了。

然後檢查一下 AI響應的 TargetLang 字段。
如果不合要求就直接不要朗讀 打警告。

強調一下 如果AI不認識 不會翻譯 就把 Translation 設成 null

還有一件事、加全局按鍵監聽、如果按了esc鍵 就中斷當前播放。
注意是全局按鍵監聽、不是兼容終端的按鍵輸入。

然後更新Readme
