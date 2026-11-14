namespace RimeTts.Domains.Tts;

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RimeTts.Infra;

[Doc($$"""
gTTS 引擎：走 Google 翻譯的 TTS 端點拿 mp3。

*為何叫 gTTS*：它的音質明顯好過 Windows 自帶的 SAPI，是首選。名字沿用 Python 那個庫的叫法，
配置裡就寫 `{{nameof(EngineName)}}`。

*為何不用 Python*：原來的版本走 pythonnet 呼叫 gtts 套件，於是整個進程要拖一個 Python 執行時、
還要配 `python312.dll` 路徑。那個端點本身就是一個 GET，直接發 HTTP 更輕、更可控，
故本專案已完全移除 pythonnet。

*為何自己切片*：端點對 `q` 的長度有限制（約 200 字元），超過會被截或回 400。
按標點切成小段再串接，是唯一穩妥的做法。
""")]
public class GttsViaHttpTts(
	OptTts Opt,
	HttpClient Http,
	ILogger<GttsViaHttpTts> Log
):ITtsEngine{

	[Doc("配置裡用來指這個引擎的名字。")]
	public const str EngineName = "gTTS";

	[Doc("配置裡指這個引擎用的名字。")]
	public str Name => EngineName;

	[Doc("純網絡引擎：只要行程活著、網絡能通就能用，故不需要探測。")]
	public bool IsAvailable => true;

	[Doc(@"端點。用 `translate.google.com` 而非 `translate.googleapis.com`：
	後者對匿名瀏覽器式請求的寬容度較差，容易回 429（`Ngan.Dict` 的經驗）。")]
	const str Endpoint = "https://translate.google.com/translate_tts";

	[Doc("單次請求的文本上限（字元）。取 180 留一點餘量給 URL 編碼後的膨脹。")]
	const i32 MaxChunkChars = 180;

	[Doc($$"""
	合成 mp3 並回傳路徑；檔案已存在就不重抓。
	#Prm[{{nameof(Req)}}][合成請求。]
	#Rtn[mp3 檔的絕對路徑。]
	""")]
	public async Task<str> GenerateAudio(ReqGenEtPlay Req, CT Ct){
		var Text = Req.Text?.Trim() ?? "";
		if(Text.Length == 0){
			throw KeysErr.Common.ArgErr__.ToErr(nameof(Req.Text));
		}
		var Lang = LangTag.Normalize(Req.Language) is { Length: > 0 } L ? L : "en";

		Directory.CreateDirectory(Opt.OutputDir);
		// 檔名要含語言：同一句中文的英譯與日譯是不同音頻，只按文本雜湊會拿錯檔案。
		var FilePath = Path.Combine(Opt.OutputDir, $"gtts_{HashHex(Lang + "\n" + Text)}.mp3");
		if(File.Exists(FilePath)){
			return FilePath;
		}

		await DownloadAsync(Text, Lang, FilePath, Ct);
		Log.LogDebug("gtts audio generated. file={File}", FilePath);
		return FilePath;
	}

	[Doc($$"""
	逐片下載並串接成一個 mp3。
	#Prm[{{nameof(Text)}}][原文。]
	#Prm[{{nameof(Lang)}}][語言代碼。]
	#Prm[{{nameof(OutMp3Path)}}][目標檔案。]
	""")]
	async Task DownloadAsync(str Text, str Lang, str OutMp3Path, CT Ct){
		var Chunks = SplitText(Text, MaxChunkChars);
		var TmpPath = OutMp3Path + ".tmp";
		try{
			await using(var OutFs = new FileStream(TmpPath, FileMode.Create, FileAccess.Write, FileShare.Read)){
				foreach(var Chunk in Chunks){
					Ct.ThrowIfCancellationRequested();
					await AppendChunkAsync(Chunk, Lang, OutFs, Ct);
				}
				await OutFs.FlushAsync(Ct);
			}
			// 先寫暫存檔再搬移：中途失敗不會留下半個 mp3 被下一輪當成「已快取」。
			File.Move(TmpPath, OutMp3Path, overwrite: true);
		}
		catch{
			TryDel(TmpPath);
			throw;
		}
	}

	[Doc("抓一片並追加寫入輸出流。")]
	async Task AppendChunkAsync(str Chunk, str Lang, Stream OutFs, CT Ct){
		var Url = Endpoint
			+ $"?ie=UTF-8&client=tw-ob&tl={Uri.EscapeDataString(Lang)}&q={Uri.EscapeDataString(Chunk)}";
		using var Req = new HttpRequestMessage(HttpMethod.Get, Url);
		// 端點會按「像不像瀏覽器」決定要不要回 429；故補齊常見的瀏覽器請求頭。
		// 只加 UA 不夠——Referer 與 Accept 缺失時它仍可能拒絕。
		Req.Headers.UserAgent.ParseAdd(
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
		Req.Headers.Referrer = new Uri("https://translate.google.com/");
		Req.Headers.Accept.ParseAdd("audio/mpeg,audio/*;q=0.9,*/*;q=0.8");
		Req.Headers.AcceptLanguage.ParseAdd($"{Lang},en;q=0.9");
		Req.Headers.TryAddWithoutValidation("Origin", "https://translate.google.com");
		using var Resp = await Http.SendAsync(Req, HttpCompletionOption.ResponseHeadersRead, Ct);
		Resp.EnsureSuccessStatusCode();
		await using var In = await Resp.Content.ReadAsStreamAsync(Ct);
		await In.CopyToAsync(OutFs, Ct);
	}

	[Doc($$"""
	按句讀點切段；每段不超過 {{nameof(MaxChunkChars)}} 字元。
	*為何在標點處切*：切在詞中間會讓語音把兩個詞黏在一起念。
	""")]
	static IList<str> SplitText(str Text, i32 MaxChars){
		var Raw = Text.Trim();
		if(Raw.Length <= MaxChars){
			return [Raw];
		}
		var Result = new List<str>();
		var Sb = new StringBuilder();
		foreach(var Ch in Raw){
			Sb.Append(Ch);
			if(Sb.Length >= MaxChars || IsBoundary(Ch)){
				Result.Add(Sb.ToString());
				Sb.Clear();
			}
		}
		if(Sb.Length > 0){
			Result.Add(Sb.ToString());
		}
		return Result;
	}

	[Doc("句讀點（中英混合）。")]
	static bool IsBoundary(char C){
		return C is '。' or '！' or '？' or '；' or '，' or '.' or '!' or '?' or ';' or ',';
	}

	[Doc("文本的 SHA-256 十六進制串；用作快取檔名。")]
	static str HashHex(str Text){
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();
	}

	[Doc("盡力刪掉殘檔；清理失敗不影響主流程。")]
	static void TryDel(str Path){
		try{
			if(File.Exists(Path)){
				File.Delete(Path);
			}
		}
		catch(IOException){
			// 殘檔留著只是礙眼，不影響快取的正確性（同名檔案下次會被覆寫）。
		}
		catch(UnauthorizedAccessException){
			// 同上。
		}
	}
}
