using Deepgram.Models.Listen.v1.REST;
using System.Text.RegularExpressions;

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
		List<string> keywords = new();

		string pattern = @"(?<=\bnames:\s*).*?(?=\.)";
		Match match = Regex.Match(speechToTextPrompt, pattern, RegexOptions.IgnoreCase);
		if (match.Success)
		{
			string namesString = match.Value.Trim();
			keywords = namesString.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
				// Use intencifier to boost detection.
				.Select(n => $"{n}:4")
				.ToList();
		}
		else
			keywords = null;

		var audioBytes = await File.ReadAllBytesAsync(audioffilePath).ConfigureAwait(false);

		var response = await _deepgramClient.TranscribeFile(
		  audioBytes,
		  new PreRecordedSchema()
		  {
			  Model = _modelId,
			  Keywords = keywords,

			  Punctuate = true,
			  FillerWords = false,
			  Measurements = true,
			  SmartFormat = true,
			  //Diarize = true,
		  });

		return response.Results.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault().Transcript
			?? "(no transcript available)";
	}
}