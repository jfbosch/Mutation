namespace CognitiveSupport.Extensions;

public static class ObjectExtensions
{
	public static void Beep(
		this object caller,
		int attempt)
	{
#pragma warning disable CA1416 // Validate platform compatibility
		Console.Beep(400 + (100 * attempt), 100);
#pragma warning restore CA1416 // Validate platform compatibility
	}

}