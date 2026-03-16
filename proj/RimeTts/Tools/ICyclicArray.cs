namespace RimeTts.Tools;

using Tsinswreng.CsCore;
using Int = int;
[Doc(@$"循環數組接口。
邏輯上維持一段從頭到尾的序列；底層可環形映射到固定容量存儲。

約定：
- Ofst 均為從0開始的非負偏移。
- Try方法失敗時返回false，不拋異常。
- 非Try方法由{nameof(ExtnCyclicArray)}提供，失敗時會拋異常。
")]
public interface ICyclicArray<T>
	:ICollection<T>
{
	[Doc(@$"底層容量。
可讀可寫；調整為更小值時不得小於{nameof(Size)}。
")]
	public Int Capaticy{get;set;}

	[Doc(@$"邏輯元素數量。
範圍應在[0, {nameof(Capaticy)}]。
")]
	public Int Size{get;set;}

	[Doc(@$"equivalent to {nameof(ExtnCyclicArray.GetHead)} and {nameof(ExtnCyclicArray.SetHead)}")]
	public T this[Int Ofst]{
		get=>this.GetHead(Ofst);
		set=>this.SetHead<T>(Ofst, value);
	}

	[Doc(@$"嘗試於頭部追加元素。
成功返true；若容量不足等原因失敗返false。
")]
	public bool TryAddHead(T Value);

	[Doc(@$"嘗試於尾部追加元素。
成功返true；若容量不足等原因失敗返false。
")]
	public bool TryAddTail(T Value);

	[Doc(@$"Force Add To Head
	If full, remove the tail element.
	#Rtn[if removed, return true; else return false]
	")]
	public bool AddHeadForce(T Value, out T Removed);

	[Doc(@$"Force Add To Tail
	If full, remove the head element.
	#Rtn[if removed, return true; else return false]
	")]
	public bool AddTailForce(T Value, out T Removed);


	[Doc(@$"嘗試按頭偏移取值。
成功返true並寫入out參數；失敗返false。
")]
	public bool TryGetHead(Int Ofst, out T? Value);

	[Doc(@$"嘗試按尾偏移取值。
成功返true並寫入out參數；失敗返false。
")]
	public bool TryGetTail(Int Ofst, out T? Value);

	[Doc(@$"嘗試按頭偏移設值。
成功返true；失敗返false。
")]
	public bool TrySetHead(Int Ofst, T Value);

	[Doc(@$"嘗試按尾偏移設值。
成功返true；失敗返false。
")]
	public bool TrySetTail(Int Ofst, T Value);
}



public static class ExtnCyclicArray{
	extension<T>(ICyclicArray<T> z){
		[Doc(@$"非Try方法。失敗會拋{nameof(ArgumentOutOfRangeException)}")]
		public T GetHead(Int Ofst){
			if(z.TryGetHead(Ofst, out var ans)){
				return ans!;
			}
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot get head element");
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(ArgumentOutOfRangeException)}")]
		public T GetTail(Int Ofst){
			if(z.TryGetTail(Ofst, out var ans)){
				return ans!;
			}
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot get tail element");
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(ArgumentOutOfRangeException)}")]
		public void SetHead(Int Ofst, T Value){
			if(!z.TrySetHead(Ofst, Value)){
				throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set head element");
			}
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(ArgumentOutOfRangeException)}")]
		public void SetTail(Int Ofst, T Value){
			if(!z.TrySetTail(Ofst, Value)){
				throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set tail element");
			}
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(InvalidOperationException)}")]
		public void AddOrThrow(T Value){
			if(!z.TryAddTail(Value)){
				throw new InvalidOperationException("cyclic array is full");
			}
		}
	}
}
