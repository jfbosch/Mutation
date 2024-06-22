using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI;

namespace CognitiveSupport
{
	public class SpeechToTextService
	{
		private readonly string _apiKey;
		private readonly string _modelId;
		private readonly string _baseDomain;
		private readonly object _lock = new object();
		private readonly IOpenAIService _openAIService;


		public SpeechToTextService(
			string apiKey,
			string baseDomain,
			string modelId)
		{
			_apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			_baseDomain = baseDomain?.Trim();
			if (_baseDomain == "")
				_baseDomain = null;
			_modelId = modelId ?? throw new ArgumentNullException(nameof(modelId), "Check your Whisper API provider's documentation for supported modelIds. On OpenAI, it's something like 'whisper-1'. On Groq, it's something like 'whisper-large-v3'.");

			OpenAiOptions options = new OpenAiOptions
			{
				ApiKey = apiKey,
				BaseDomain = _baseDomain,
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
				Prompt = speechToTextPrompt,
				FileName = Path.GetFileName(audioffilePath),
				File = audioBytes,
				Model = _modelId,
				ResponseFormat = StaticValues.AudioStatics.ResponseFormat.VerboseJson
			});
			if (response.Successful)
			{
				return response.Text;
			}
			else
			{
				if (response.Error == null)
				{
					throw new Exception("Unknown Error");
				}
				return $"Error converting speech to text: {response.Error.Code} {response.Error.Message}";
			}
		}
	}
}