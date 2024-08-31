using Deepgram.Models.Listen.v1.REST;

namespace CognitiveSupport;

public class DeepgramSpeechToTextService : ISpeechToTextService
{
	private readonly string _modelId;
	private readonly object _lock = new object();
	private readonly Deepgram.Clients.Interfaces.v1.IListenRESTClient _deepgramClient;

	public DeepgramSpeechToTextService(
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient,
		string modelId)
	{
		_deepgramClient = deepgramClient ?? throw new ArgumentNullException(nameof(deepgramClient));
		_modelId = modelId ?? throw new ArgumentNullException(nameof(modelId), "Check your Whisper API provider's documentation for supported modelIds. On OpenAI, it's something like 'whisper-1'. On Groq, it's something like 'whisper-large-v3'.");
	}

	public async Task<string> ConvertAudioToText(
		string speechToTextPrompt,
		string audioffilePath)
	{
		var audioBytes = await File.ReadAllBytesAsync(audioffilePath).ConfigureAwait(false);

		var response = await _deepgramClient.TranscribeFile(
		  audioBytes,
		  new PreRecordedSchema()
		  {
			  Punctuate = true,
			  //Diarize = true,
			  //SmartFormat = true,
			  Model = _modelId,
		  });

		return response.Results.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault().Transcript
			?? "(no transcript available)";
	}
}