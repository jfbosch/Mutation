using CognitiveSupport.Extensions;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;

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
		const string AttemptKey = "Attempt";

		var delay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(500), retryCount: 3, factor: 1);
		var retryPolicy = Policy
			.Handle<HttpRequestException>()
			.Or<TimeoutRejectedException>()
			.Or<TaskCanceledException>()
				.WaitAndRetryAsync(
					delay,
					onRetry: (exception, timeSpan, attemptNumber, context) =>
					{
						int attempt = context.ContainsKey(AttemptKey) ? (int)context[AttemptKey] : 1;
						context[AttemptKey] = ++attempt;
					}
				);

		var context = new Context();
		context[AttemptKey] = 1;
		
		var response = await retryPolicy.ExecuteAsync(async (context, token) =>
		{
			int attempt = context.ContainsKey(AttemptKey) ? (int)context[AttemptKey] : 1;
			var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5 * attempt));

			if (attempt > 0)
				this.Beep(attempt);

			return await TranscribeViaWhisper(speechToTextPrompt, audioffilePath, audioBytes, cts.Token).ConfigureAwait(false);
		}, context, new CancellationTokenSource().Token).ConfigureAwait(false);

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

	private async Task<OpenAI.ObjectModels.ResponseModels.AudioCreateTranscriptionResponse> TranscribeViaWhisper(
		string speechToTextPrompt,
		string audioffilePath,
		byte[] audioBytes,
		CancellationToken cancellationToken)
	{
		return await _openAIService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
		{
			Prompt = speechToTextPrompt,
			FileName = Path.GetFileName(audioffilePath),
			File = audioBytes,
			Model = _modelId,
			ResponseFormat = StaticValues.AudioStatics.ResponseFormat.VerboseJson
		}, cancellationToken);
	}
}