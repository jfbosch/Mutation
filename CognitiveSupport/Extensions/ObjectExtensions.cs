namespace CognitiveSupport.Extensions;

public static class ObjectExtensions
{
	public static void Beep(
		this object caller,
		int attempt)
	{
#pragma warning disable CA1416 // Validate platform compatibility
		BeepPlayer.Play ( BeepType.Failure );
#pragma warning restore CA1416 // Validate platform compatibility
	}

}