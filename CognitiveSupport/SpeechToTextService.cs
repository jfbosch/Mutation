using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace CognitiveSupport
{
	public class SpeechToTextService : ISpeechToTextService
	{
		private readonly string _modelId;
		private readonly object _lock = new object();
		private readonly IOpenAIService _openAIService;

		public SpeechToTextService(
			IOpenAIService openAIService,
			string modelId)
		{
			_openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
			_modelId = modelId ?? throw new ArgumentNullException(nameof(modelId), "Check your Whisper API provider's documentation for supported modelIds. On OpenAI, it's something like 'whisper-1'. On Groq, it's something like 'whisper-large-v3'.");
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