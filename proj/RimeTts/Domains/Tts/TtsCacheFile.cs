namespace RimeTts.Domains.Tts;

using System.Security.Cryptography;
using System.Text;

[Doc($$"""
TTS 檔案命名的共用邏輯。

*為何收成一處（2026-11-16 審查 MAINT-1）*：`gtts` 與 `system_speech` 兩個引擎原本
各自複製了一份「雜湊檔名」與「刪殘檔」，兩份會各自漂移——
檔名規則一旦改了一邊（例如加語言段），另一邊的舊快取就全數失效且無人知曉。
命名規則只有一份，快取命中才在所有引擎之間一致。

*檔名規則*：`{前綴}_{SHA-256(語言 + 換行 + 文本)}.{副檔名}`。
雜湊裡含語言：同一句中文的英譯與日譯是不同音頻，只按文本雜湊會拿錯檔案。
換行只是分隔符（不可能出現在單行語言代碼裡），避免「`ab`+`c`」與「`a`+`bc`」同雜湊。
""")]
public static class TtsCacheFile{

	[Doc("文本的 SHA-256 十六進制小寫串。")]
	public static str HashHex(str Text){
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();
	}

	[Doc($$"""
	造一個引擎快取檔的完整路徑（不會真的建檔）。
	#Prm[{{nameof(Dir)}}][輸出目錄。]
	#Prm[{{nameof(Prefix)}}][引擎前綴（`gtts`、`system_speech`）。]
	#Prm[{{nameof(Lang)}}][語言代碼，會算進雜湊。]
	#Prm[{{nameof(Text)}}][要唸的文本，會算進雜湊。]
	#Prm[{{nameof(Ext)}}][副檔名（含點，如 `.mp3`）。]
	#Rtn[完整路徑。]
	""")]
	public static str MkPath(str Dir, str Prefix, str Lang, str Text, str Ext){
		return Path.Combine(Dir, $"{Prefix}_{HashHex(Lang + "\n" + Text)}{Ext}");
	}

	[Doc($$"""
	盡力刪掉殘檔；清理失敗不影響主流程。
	*殘檔為何必須刪*：快取以「檔案存在＝已合成」判定，半個音頻留下來
	會在下一輪被當成完整快取播出（另一個引擎寫暫存檔再搬移也是同一個理由）。
	""")]
	public static void TryDel(str Path){
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
