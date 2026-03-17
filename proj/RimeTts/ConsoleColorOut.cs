namespace RimeTts;

public static class ConsoleColorOut{
	private static readonly Lock Gate = new();

	public static void WriteLine(str tag, str text, ConsoleColor color){
		lock(Gate){
			var old = Console.ForegroundColor;
			try{
				Console.ForegroundColor = color;
				Console.WriteLine($"{DateTime.Now:HH:mm:ss} {tag} {text}");
			}
			finally{
				Console.ForegroundColor = old;
			}
		}
	}
}
