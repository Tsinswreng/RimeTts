using System.Text.Json;

namespace RimeTts.Tools;

public sealed class JsonSerializer_:IJsonSerializer{
	private static readonly JsonSerializerOptions _opt = new(){
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
	};

	public str Stringify<T>(T O){
		return JsonSerializer.Serialize(O, _opt);
	}

	public T Parse<T>(str JsonStr){
		return JsonSerializer.Deserialize<T>(JsonStr, _opt)
			?? throw new InvalidOperationException("json parse failed");
	}

	public obj? Parse(str JsonStr, Type Type){
		return JsonSerializer.Deserialize(JsonStr, Type, _opt);
	}
}
