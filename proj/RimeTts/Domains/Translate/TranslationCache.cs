namespace RimeTts.Domains.Translate;

[Doc($$"""
有上限的譯文快取（最近最少使用淘汰）。

*為何要有上限*：輸入法常把同一句重打（改字、重新上屏），不快取就每次重付一次延遲；
但只增不減的字典在長駐進程裡就是內存泄漏。原本的版本正是後者。
上限取 {{nameof(OptTranslator.CacheCapacity)}}，`0` 表示完全不快取。

*為何連「翻不了」也快取*：那也是模型對這句話的結論；不快取等於每次重問一次。
值是 null 與「沒有這條」必須分得開，故 {{nameof(TryGet)}} 回 bool 而不是看值是否為 null。

*為何不用現成的記憶體快取套件*：本專案只用到「有上限的鍵值 ＋ 淘汰」，一個 60 行的類就夠；
引入套件反而要處理它的生命週期與選項。
""")]
public class TranslationCache{

	[Doc("一條快取項。Key 存在節點裡是為了淘汰時能從字典反查。")]
	sealed record Entry(str Key, str? Value);

	[Doc("上限；<=0 表示不快取（Set 直接不做事）。")]
	readonly i32 Capacity;

	[Doc("鍵 → 節點；節點同時掛在 Lru 鏈上。")]
	readonly Dictionary<str, LinkedListNode<Entry>> Map = new();

	[Doc("LRU 鏈：頭部最近用過，尾部最先淘汰。")]
	readonly LinkedList<Entry> Lru = new();

	[Doc("快取會被多條工作線程同時讀寫（同一句可能被兩個語言同時翻）。")]
	readonly Lock Gate = new();

	[Doc($$"""
	建快取。
	#Prm[{{nameof(Capacity)}}][最大條目數；<=0 表示關閉快取。]
	""")]
	public TranslationCache(i32 Capacity){
		this.Capacity = Capacity;
	}

	[Doc("目前條目數（診斷與測試用）。")]
	public i32 Count{
		get{
			lock(Gate){
				return Map.Count;
			}
		}
	}

	[Doc($$"""
	查一條；命中時把它移到鏈頭。
	#Prm[{{nameof(Key)}}][快取鍵。]
	#Prm[{{nameof(Value)}}][命中時寫出值（可能是 null＝上次的結論是「翻不了」）。]
	#Rtn[命中回 true。]
	""")]
	public bool TryGet(str Key, out str? Value){
		lock(Gate){
			if(!Map.TryGetValue(Key, out var Node)){
				Value = null;
				return false;
			}
			Lru.Remove(Node);
			Lru.AddFirst(Node);
			Value = Node.Value.Value;
			return true;
		}
	}

	[Doc($$"""
	寫入一條；超出上限時淘汰鏈尾。
	#Prm[{{nameof(Key)}}][快取鍵。]
	#Prm[{{nameof(Value)}}][值；null 表示「翻不了」這個結論。]
	""")]
	public void Set(str Key, str? Value){
		if(Capacity <= 0){
			return;
		}
		lock(Gate){
			if(Map.TryGetValue(Key, out var Exist)){
				// 已存在：換值並提到鏈頭，長度不變。
				Exist.Value = new Entry(Key, Value);
				Lru.Remove(Exist);
				Lru.AddFirst(Exist);
				return;
			}
			var Node = Lru.AddFirst(new Entry(Key, Value));
			Map[Key] = Node;
			// 淘汰：從尾部一次可能不止一條（上限被調小時），故用 while。
			while(Map.Count > Capacity && Lru.Last is { } Last){
				Lru.RemoveLast();
				Map.Remove(Last.Value.Key);
			}
		}
	}
}
