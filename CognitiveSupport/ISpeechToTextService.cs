namespace CognitiveSupport;

public interface ISpeechToTextService
{
	string ServiceName { get; init; }

	Task<string> ConvertAudioToText(string speechToTextPrompt, string audioffilePath, CancellationToken overallCancellationToken);
}
