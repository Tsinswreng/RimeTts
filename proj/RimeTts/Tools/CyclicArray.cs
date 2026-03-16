namespace RimeTts.Tools;

using Tsinswreng.CsCore;
using Int = int;

[Doc(@$"{nameof(ICyclicArray<int>)}之默認實現")]
public class CyclicArray<T>:ICyclicArray<T>{
	protected T?[] _data = [];
	protected Int _size;
	protected Int _headIndex;
	protected IEqualityComparer<T> Comparer {get;} = EqualityComparer<T>.Default;

	public CyclicArray(){
	}

	public CyclicArray(Int capaticy){
		Capaticy = capaticy;
	}

	public Int Capaticy{
		get => _data.Length;
		set {
			if(value < 0){
				throw new ArgumentOutOfRangeException(nameof(value), value, "capacity must be >= 0");
			}
			if(value < _size){
				throw new ArgumentOutOfRangeException(nameof(value), value, $"capacity must be >= {nameof(Size)}");
			}
			if(value == _data.Length){
				return;
			}

			var neo = new T?[value];
			for(var i = 0; i < _size; i++){
				neo[i] = GetHead(i);
			}
			_data = neo;
			_headIndex = 0;
		}
	}

	public Int Size{
		get => _size;
		set {
			if(value < 0 || value > Capaticy){
				throw new ArgumentOutOfRangeException(nameof(value), value, $"size must be in [0, {Capaticy}]");
			}
			if(value < _size){
				for(var i = value; i < _size; i++){
					SetHead(i, default!);
				}
			}
			_size = value;
			if(_size == 0){
				_headIndex = 0;
			}
		}
	}

	public Int Count => Size;

	public bool IsReadOnly => false;

	[Doc(@$"equivalent to {nameof(GetHead)} and {nameof(SetHead)}")]
	public T this[Int Ofst]{
		get => GetHead(Ofst);
		set => SetHead(Ofst, value);
	}

	public bool TryAddHead(T Value){
		if(Capaticy <= 0 || Size >= Capaticy){
			return false;
		}

		if(Size == 0){
			_data[_headIndex] = Value;
		}else{
			_headIndex = Mod(_headIndex - 1, Capaticy);
			_data[_headIndex] = Value;
		}
		_size += 1;
		return true;
	}

	public bool TryAddTail(T Value){
		if(Capaticy <= 0 || Size >= Capaticy){
			return false;
		}

		var index = NormalizeInsertTailIndex();
		_data[index] = Value;
		_size += 1;
		return true;
	}

	public bool AddHeadForce(T Value, out T Removed){
		if(TryAddHead(Value)){
			Removed = default!;
			return false;
		}

		var removed = GetTail(0);
		_ = RemoveTailCore();
		_ = TryAddHead(Value);
		Removed = removed;
		return true;
	}

	public bool AddTailForce(T Value, out T Removed){
		if(TryAddTail(Value)){
			Removed = default!;
			return false;
		}

		var removed = GetHead(0);
		_ = RemoveHeadCore();
		_ = TryAddTail(Value);
		Removed = removed;
		return true;
	}

	public bool TryGetHead(Int Ofst, out T? Value){
		if(!TryNormalizeHeadOffset(Ofst, out var index)){
			Value = default;
			return false;
		}

		Value = _data[index];
		return true;
	}

	public bool TryGetTail(Int Ofst, out T? Value){
		if(!TryNormalizeTailOffset(Ofst, out var index)){
			Value = default;
			return false;
		}

		Value = _data[index];
		return true;
	}

	public bool TrySetHead(Int Ofst, T Value){
		if(!TryNormalizeHeadOffset(Ofst, out var index)){
			return false;
		}

		_data[index] = Value;
		return true;
	}

	public bool TrySetTail(Int Ofst, T Value){
		if(!TryNormalizeTailOffset(Ofst, out var index)){
			return false;
		}

		_data[index] = Value;
		return true;
	}

	public T GetHead(Int Ofst){
		if(!TryGetHead(Ofst, out var value)){
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot get head element");
		}
		return value!;
	}

	public T GetTail(Int Ofst){
		if(!TryGetTail(Ofst, out var value)){
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot get tail element");
		}
		return value!;
	}

	public void SetHead(Int Ofst, T Value){
		if(!TrySetHead(Ofst, Value)){
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set head element");
		}
	}

	public void SetTail(Int Ofst, T Value){
		if(!TrySetTail(Ofst, Value)){
			throw new ArgumentOutOfRangeException(nameof(Ofst), Ofst, "cannot set tail element");
		}
	}

	public void Add(T item){
		if(!TryAddTail(item)){
			throw new InvalidOperationException("cyclic array is full");
		}
	}

	public void Clear(){
		Array.Clear(_data);
		_size = 0;
		_headIndex = 0;
	}

	public bool Contains(T item){
		for(var i = 0; i < Size; i++){
			if(Comparer.Equals(GetHead(i), item)){
				return true;
			}
		}
		return false;
	}

	public void CopyTo(T[] array, Int arrayIndex){
		ArgumentNullException.ThrowIfNull(array);
		if(arrayIndex < 0){
			throw new ArgumentOutOfRangeException(nameof(arrayIndex));
		}
		if(array.Length - arrayIndex < Size){
			throw new ArgumentException("destination array is too small", nameof(array));
		}

		for(var i = 0; i < Size; i++){
			array[arrayIndex + i] = GetHead(i);
		}
	}

	public bool Remove(T item){
		for(var i = 0; i < Size; i++){
			if(!Comparer.Equals(GetHead(i), item)){
				continue;
			}

			for(var j = i; j < Size - 1; j++){
				SetHead(j, GetHead(j + 1));
			}

			var tailIndex = NormalizeTailOffset(0);
			_data[tailIndex] = default;
			_size -= 1;
			if(_size == 0){
				_headIndex = 0;
			}
			return true;
		}

		return false;
	}

	public IEnumerator<T> GetEnumerator(){
		for(var i = 0; i < Size; i++){
			yield return GetHead(i);
		}
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator(){
		return GetEnumerator();
	}

	protected Int NormalizeHeadOffset(Int ofst){
		if(!TryNormalizeHeadOffset(ofst, out var index)){
			throw new ArgumentOutOfRangeException(nameof(ofst), ofst, "offset must be >= 0 and capacity must be > 0");
		}
		return index;
	}

	protected Int NormalizeTailOffset(Int ofst){
		if(!TryNormalizeTailOffset(ofst, out var index)){
			throw new ArgumentOutOfRangeException(nameof(ofst), ofst, "offset must be >= 0 and capacity must be > 0");
		}
		return index;
	}

	protected bool TryNormalizeHeadOffset(Int ofst, out Int index){
		if(Capaticy <= 0 || ofst < 0){
			index = default;
			return false;
		}
		index = Mod(_headIndex + ofst, Capaticy);
		return true;
	}

	protected bool TryNormalizeTailOffset(Int ofst, out Int index){
		if(Capaticy <= 0 || ofst < 0){
			index = default;
			return false;
		}
		var tailIndex = _size == 0 ? _headIndex : Mod(_headIndex + _size - 1, Capaticy);
		index = Mod(tailIndex - ofst, Capaticy);
		return true;
	}

	protected Int NormalizeInsertTailIndex(){
		if(_size == 0){
			return _headIndex;
		}
		return Mod(_headIndex + _size, Capaticy);
	}

	protected T? RemoveHeadCore(){
		if(_size <= 0){
			return default;
		}

		var oldHeadIndex = _headIndex;
		var removed = _data[oldHeadIndex];
		_data[oldHeadIndex] = default;
		_size -= 1;
		if(_size == 0){
			_headIndex = 0;
		}else{
			_headIndex = Mod(oldHeadIndex + 1, Capaticy);
		}
		return removed;
	}

	protected T? RemoveTailCore(){
		if(_size <= 0){
			return default;
		}

		var tailIndex = NormalizeTailOffset(0);
		var removed = _data[tailIndex];
		_data[tailIndex] = default;
		_size -= 1;
		if(_size == 0){
			_headIndex = 0;
		}
		return removed;
	}

	protected void EnsureCapacityAvailable(){
		if(Capaticy <= 0){
			throw new InvalidOperationException("capacity is 0");
		}
	}

	protected static Int Mod(Int value, Int divisor){
		var ans = value % divisor;
		if(ans < 0){
			ans += divisor;
		}
		return ans;
	}
}
