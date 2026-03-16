namespace RimeTts.Tools;

using Tsinswreng.CsCore;
using Int = int;
[Doc(@$"Cyclic Array
//TODO 補全文檔
")]
public interface ICyclicArray<T>
	:ICollection<T>
{
	[Doc("")]
	public Int Capaticy{get;set;}
	public Int Size{get;set;}
	[Doc(@$"equivalent to {nameof(ExtnCyclicArray.GetHead)} and {nameof(ExtnCyclicArray.SetHead)}")]
	public T this[Int Ofst]{
		get=>this.GetHead(Ofst);
		set=>this.SetHead<T>(Ofst, value)
	}
	public bool TryAdd(T Value);
	public bool TryGetHead(Int Ofst, out T? Value);
	public bool TryGetTail(Int Ofst, out T? Value);
	public bool TrySetHead(T Value, Int Ofst);
	public bool TrySetTail(T Value, Int Ofst);
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
		public void SetHead(T Value, Int Ofst){
			if(!z.TrySetHead(Value, Ofst)){
				throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set head element");
			}
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(ArgumentOutOfRangeException)}")]
		public void SetTail(T Value, Int Ofst){
			if(!z.TrySetTail(Value, Ofst)){
				throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set tail element");
			}
		}

		[Doc(@$"非Try方法。失敗會拋{nameof(InvalidOperationException)}")]
		public void AddOrThrow(T Value){
			if(!z.TryAdd(Value)){
				throw new InvalidOperationException("cyclic array is full");
			}
		}
	}
}
