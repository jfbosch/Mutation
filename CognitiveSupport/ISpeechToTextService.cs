namespace CognitiveSupport;

public interface ISpeechToTextService
{
	Task<string> ConvertAudioToText(string speechToTextPrompt, string audioffilePath);
}