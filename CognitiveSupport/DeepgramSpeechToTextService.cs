using CognitiveSupport.Extensions;
using Deepgram.Models.Listen.v1.REST;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Text.RegularExpressions;

namespace CognitiveSupport;

public class DeepgramSpeechToTextService : ISpeechToTextService
{
	public string ServiceName { get; init; }

	private readonly string _modelId;
	private readonly object _lock = new object();
	private readonly Deepgram.Clients.Interfaces.v1.IListenRESTClient _deepgramClient;

	public DeepgramSpeechToTextService(
		string serviceName,
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient,
		string modelId)
	{
		ServiceName = serviceName;
		_deepgramClient = deepgramClient ?? throw new ArgumentNullException(nameof(deepgramClient));
		_modelId = modelId ?? throw new ArgumentNullException(nameof(modelId), "Check your Whisper API provider's documentation for supported modelIds. On OpenAI, it's something like 'whisper-1'. On Groq, it's something like 'whisper-large-v3'.");
	}

	public async Task<string> ConvertAudioToText(
		string speechToTextPrompt,
		string audioffilePath,
		CancellationToken overallCancellationToken)
	{
		if (string.IsNullOrEmpty(audioffilePath))
			throw new ArgumentException($"'{nameof(audioffilePath)}' cannot be null or empty.", nameof(audioffilePath));

		List<string> keywords = ParseKeywords(speechToTextPrompt);
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
		var response = await retryPolicy.ExecuteAsync(async (context, overallToken) =>
		{
			int attempt = context.ContainsKey(AttemptKey) ? (int)context[AttemptKey] : 1;
			var thisTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(5 * attempt));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken, thisTryCts.Token);

			if (attempt > 0)
				this.Beep(attempt);

			return await TranscribeViaDeepgram(keywords, audioBytes, linkedCts).ConfigureAwait(false);
		}, context, overallCancellationToken).ConfigureAwait(false);

		return response.Results.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault().Transcript
			?? "(no transcript available)";
	}

	private async Task<SyncResponse> TranscribeViaDeepgram(
		List<string> keywords,
		byte[] audioBytes,
		CancellationTokenSource cancellationTokenSource)
	{
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
		  }, cancellationTokenSource = cancellationTokenSource);
		return response;
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