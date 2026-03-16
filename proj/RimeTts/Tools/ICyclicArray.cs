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
	[Doc(@$"equivalent to {nameof(GetHead)} and {nameof(SetHead)}")]

	public T this[Int Ofst]{get;set;}
	public bool TryAdd(T Value);
	public bool TryGetHead(Int Ofst, out T? Value);
	public bool TryGetTail(Int Ofst, out T? Value);
	public bool TrySetHead(T Value, Int Ofst);
	public bool TrySetTail(T Value, Int Ofst);
}



public static class ExtnCyclicArray{
	extension<T>(ICyclicArray<T> z){
		public T GetHead(Int Ofst);
		public T GetTail(Int Ofst);
		public void SetHead(T Value, Int Ofst);
		public void SetTail(T Value, Int Ofst);
	}
}
