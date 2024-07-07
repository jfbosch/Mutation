namespace CognitiveSupport;

public interface IOcrService
{
	Task<string> ExtractText(Stream imageStream);
}