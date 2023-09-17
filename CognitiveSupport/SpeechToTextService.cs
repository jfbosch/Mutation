using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace CognitiveSupport
{
	public class SpeechToTextService
	{
		private readonly string _apiKey;
		private readonly string _endpoint;
		private readonly object _lock = new object();
		private readonly IOpenAIService _openAIService;


		public SpeechToTextService(
			string apiKey)
		{
			_apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

			OpenAiOptions options = new OpenAiOptions
			{
				ApiKey = apiKey,
			};
			HttpClient httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(30);
			_openAIService = new OpenAIService(options, httpClient);
		}

		public async Task<string> ConvertAudioToText(
			string speechToTextPrompt,
			string audioffilePath)
		{
			var audioBytes = await File.ReadAllBytesAsync(audioffilePath).ConfigureAwait(false);
			var response = await _openAIService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
			{
				Language = "en",
				Prompt = speechToTextPrompt,
				FileName = Path.GetFileName(audioffilePath),
				File = audioBytes,
				Model = Models.WhisperV1,
				ResponseFormat = StaticValues.AudioStatics.ResponseFormat.VerboseJson,
				Temperature = 0.2f,
			});
			if (response.Successful)
			{
				return response.Text;
			}
			else
			{
				if (response.Error == null)
					return $"Error converting speech to text: Unknown Error";
				else
					return $"Error converting speech to text: {response.Error.Code} {response.Error.Message}";
			}
		}
	}
}