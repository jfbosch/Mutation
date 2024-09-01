using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Polly.Contrib.WaitAndRetry;
using Polly;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Polly.Timeout;
using Polly.Extensions.Http;

namespace CognitiveSupport;

public class WhisperSpeechToTextService : ISpeechToTextService
{
	private readonly string _modelId;
	private readonly object _lock = new object();
	private readonly IOpenAIService _openAIService;

	public WhisperSpeechToTextService(
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

		var delay = Backoff.LinearBackoff(TimeSpan.FromMicroseconds(5), retryCount: 1, factor: 1);
		var retryPolicy = Policy
			.Handle<HttpRequestException>()
			.Or<TimeoutRejectedException>()
				.WaitAndRetryAsync(delay);

		//BeepFail(args.AttemptNumber);

		var response = await retryPolicy.ExecuteAsync(async () =>
		{
			return await TranscribeViaWhisper(speechToTextPrompt, audioffilePath, audioBytes).ConfigureAwait(false);
		}).ConfigureAwait(false);

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

	private async Task<OpenAI.ObjectModels.ResponseModels.AudioCreateTranscriptionResponse> TranscribeViaWhisper(string speechToTextPrompt, string audioffilePath, byte[] audioBytes)
	{
		return await _openAIService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
		{
			Prompt = speechToTextPrompt,
			FileName = Path.GetFileName(audioffilePath),
			File = audioBytes,
			Model = _modelId,
			ResponseFormat = StaticValues.AudioStatics.ResponseFormat.VerboseJson
		});
	}
}