namespace RimeTts.Domains.Translate;

[Doc($$"""
出廠的 LLM 提示詞。

*為何收成一處*：同一段「回 jsonr、用生字串、翻不了就寫 null」的規則原本在六份提示詞裡各抄一遍
（兜底那份 ＋ 五個語言）。要改一條規則就得改六處，漏一處就是某個語言的行為不一致——
本檔就是為了讓那種改動只發生一次（英文規則一份、日文規則一份）。

*為何仍然逐語言寫*：每個語言的引言句要用該語言寫（模型對「用目標語言描述的任務」更穩），
故 {{nameof(Mk)}} 收一句引言，規則段共用。

*為何定界詞要模型自己隨機造*：生字串的結束條件是「遇到 `)定界詞"`」。
若我們在提示詞裡寫死 `EOF`，譯文裡一旦出現 `)EOF"`（翻的是一段程式碼、或原文本身有這種字串），
解析就會在那裡提前收尾、剩下的全變成垃圾。故要求模型每次自己造一個新的英數字詞，
且該詞不得出現在譯文裡——碰撞概率因此可以忽略，而「模型偷懶照抄範例」也明確禁止了。

#Eg[
	```cs
	var Prompt = {nameof(LlmPrompts)}.{nameof(Mk)}("You are a translator.", "es", "Texto traducido");
	```
]
""")]
public static class LlmPrompts{

	[Doc($$"""
	英文的兜底提示詞（目標語言固定為英文）。
	#Rtn[完整的系統提示詞。]
	""")]
	public static str MkEn(){
		return Mk(
			"You are a fast translator. Translate the user's text into English."
			,"en"
			,"Translated Text"
		);
	}

	[Doc($$"""
	日文的提示詞（目標語言固定為日文，規則段用日文寫）。
	#Rtn[完整的系統提示詞。]
	""")]
	public static str MkJa(){
		return """
あなたは高速な翻訳者です。利用者の文章を日本語に翻訳してください。

jsonr のみを返してください。jsonr は JSON に C++ 風の生文字列を足したものです。
生文字列の区切り語は、返答のたびにあなたが新しく乱数で決めてください。
下の例の語をそのまま使わないこと。前回の返答で使った語も使わないこと。

{
	"UserInputLang": "zh",
	"TargetLang": "ja",
	"Translation": R"Xk7q(翻訳文)Xk7q"
}

規則:
- UserInputLang: 入力から判定した言語の BCP-47 タグ。
- TargetLang: 必ず "ja"。
- Translation: 訳文を R"区切り語(…)区切り語" の形で包む。区切り語は今回の返答のために作る
  英数字の羅列で、訳文の中に一度も現れないものにすること（現れるなら別の語に変える）。
  読み手は「)区切り語"」が最初に現れたところで値を終えるので、区切り語が本文に無いことが
  そのまま「引用符も改行もエスケープしなくてよい」ことの条件になる。
- 翻訳できない場合は "Translation": null と書く。
- 返答全体を markdown のコードフェンスで囲まないこと。
""";
	}

	[Doc($$"""
	造一份英文提示詞。
	#Prm[{{nameof(Intro)}}][引言句：說明「翻成哪個語言」，用該目標語言或英文寫。]
	#Prm[{{nameof(Lang)}}][目標語言代碼（寫進 {{nameof(ResLlm.TargetLang)}} 的要求裡）。]
	#Prm[{{nameof(SampleTranslation)}}][範例譯文（只出現在示例裡，讓模型看得懂格式）。]
	#Rtn[完整的系統提示詞。]
	""")]
	public static str Mk(str Intro, str Lang, str SampleTranslation){
		return $$"""
{{Intro}}

Return jsonr only, nothing else. jsonr is JSON plus C++ style raw strings.
Pick a NEW random delimiter for the raw string in every answer. Never reuse the delimiter
shown in the example below, and never reuse the one from a previous answer.

{
	"UserInputLang": "zh",
	"TargetLang": "{{Lang}}",
	"Translation": R"Xk7q({{SampleTranslation}})Xk7q"
}

Rules:
- UserInputLang: the BCP-47 tag of the language you detected in the input.
- TargetLang: must be exactly "{{Lang}}".
- Translation: the translated text, wrapped as R"k3Xq(…)k3Xq" where k3Xq stands for an
  alphanumeric token you invent for THIS answer — make it look random (mixed letters and
  digits), and never reuse the token from the example or from a previous answer.
  The token must not occur anywhere inside the text; if it does, invent another one.
  The reader ends the value at the first occurrence of that closing token, so "the token is
  absent from the text" is exactly what lets you skip escaping quotes and newlines.
- If the text cannot be translated, write: "Translation": null
- Do not wrap the whole answer in markdown fences.
""";
	}
}
