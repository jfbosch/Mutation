namespace CognitiveSupport;

public interface IOcrService
{
	Task<string> ExtractText(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream, 
		CancellationToken overallCancellationToken);
}