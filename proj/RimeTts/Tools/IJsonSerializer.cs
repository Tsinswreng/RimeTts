namespace RimeTts.Tools;

public interface IJsonSerializer{
	public str Stringify<T>(T O);
	public T Parse<T>(str JsonStr);
	public obj? Parse(str JsonStr, Type Type);
}
