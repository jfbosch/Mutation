using Deepgram.Models.Listen.v1.REST;
using MoreLinq;
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
		List<string> keywords = ParseKeywords(speechToTextPrompt);

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

	private static List<string> ParseKeywords(string speechToTextPrompt)
	{
		if (speechToTextPrompt == null)
			speechToTextPrompt = string.Empty;

		string pattern = @"(?<=\bkeywords:\s*).*?(?=\.)";
		Match match = Regex.Match(speechToTextPrompt, pattern, RegexOptions.IgnoreCase);
		if (match.Success)
		{
			string keywordsString = match.Value.Trim();
			var keywords = keywordsString.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
				.Select(n =>
				{
					//If the keyword already contains an intensifier, return it as is. Else add an intensifier of 1. 
					//https://developers.deepgram.com/docs/keywords
					return n.IndexOf(":") > 0 ?
						n :
						$"{n}1";
				})
				.ToList();
			return keywords;
		}
		else
			return null;
	}
}