using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI;

namespace CognitiveSupport
{
	public class SpeechToTextService
	{
		private readonly string ApiKey;
		private readonly string Endpoint;
		private readonly object _lock = new object();
		private readonly IOpenAIService _openAIService;


		public SpeechToTextService(
			string apiKey,
			string endpoint)
		{
			ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

			OpenAiOptions options = new OpenAiOptions
			{
				ApiKey = apiKey,
			};
			HttpClient httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(30);
			_openAIService = new OpenAIService(options, httpClient);
		}

		public async Task<string> ConvertAudioToText(
			string filePath)
		{
			var audioBytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
			var audioResult = await _openAIService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
			{
				FileName = Path.GetFileName(filePath),
				File = audioBytes,
				Model = Models.WhisperV1,
				ResponseFormat = StaticValues.AudioStatics.ResponseFormat.VerboseJson
			});
			if (audioResult.Successful)
			{
				return audioResult.Text;
			}
			else
			{
				if (audioResult.Error == null)
				{
					throw new Exception("Unknown Error");
				}
				return $"Error converting speech to text: {audioResult.Error.Message}";
			}
		}
	}
}