using CognitiveSupport.Extensions;
using Deepgram.Models.Listen.v1.REST;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Text.RegularExpressions;

namespace CognitiveSupport;

public class DeepgramSpeechToTextService : ISpeechToTextService
{
	private const string ModelNova2 = "nova-2";
	private const string ModelNova3 = "nova-3";

	public string ServiceName { get; init; }

	private readonly string _modelId;
	private readonly object _lock = new object();
	private readonly Deepgram.Clients.Interfaces.v1.IListenRESTClient _deepgramClient;
	private readonly int _timeoutSeconds;

	public DeepgramSpeechToTextService(
		string serviceName,
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient,
		string modelId,
		int timeoutSeconds = 10)
	{
		ServiceName = serviceName;
		_deepgramClient = deepgramClient ?? throw new ArgumentNullException(nameof(deepgramClient));
		_modelId = modelId ?? throw new ArgumentNullException(nameof(modelId), "Check your Deepgram API documentation for supported modelIds.");
		_timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 10;
	}

	public async Task<string> ConvertAudioToText(
		string speechToTextPrompt,
		string audioffilePath,
		CancellationToken overallCancellationToken)
	{
		if (string.IsNullOrEmpty(audioffilePath))
			throw new ArgumentException($"'{nameof(audioffilePath)}' cannot be null or empty.", nameof(audioffilePath));

		List<string> keywordsOrKeyterms = new();

		switch (_modelId)
		{
			case ModelNova2:
				keywordsOrKeyterms = ParseKeywords(speechToTextPrompt);
				break;
			case ModelNova3:
				keywordsOrKeyterms = ParseKeyterms(speechToTextPrompt);
				break;
		}

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
			int timeout = Math.Min(_timeoutSeconds * attempt, 60);
			var thisTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken, thisTryCts.Token);

			if (attempt > 0)
				this.Beep(attempt);

			return await TranscribeViaDeepgram(keywordsOrKeyterms, audioBytes, linkedCts).ConfigureAwait(false);
		}, context, overallCancellationToken).ConfigureAwait(false);

		return response.Results.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault().Transcript
			?? "(no transcript available)";
	}

	private async Task<SyncResponse> TranscribeViaDeepgram(
		List<string> keywordsOrKeyterms,
		byte[] audioBytes,
		CancellationTokenSource cancellationTokenSource)
	{
		var response = await _deepgramClient.TranscribeFile(
		  audioBytes,
		  new PreRecordedSchema()
		  {
			  Model = _modelId,
			  // nova-2 uses keywordsOrKeyterms, nova-3 uses keyterms
			  Keywords = _modelId.StartsWith(ModelNova2) ? keywordsOrKeyterms : null,
			  Keyterm = _modelId.StartsWith(ModelNova3) ? keywordsOrKeyterms : null,

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
					//https://developers.deepgram.com/docs/keywordsOrKeyterms
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

	private static List<string> ParseKeyterms(string speechToTextPrompt)
	{
		if (speechToTextPrompt == null)
			speechToTextPrompt = string.Empty;

		string pattern = @"(?<=\bkeyterms:\s*).*?(?=\.)";
		Match match = Regex.Match(speechToTextPrompt, pattern, RegexOptions.IgnoreCase);
		if (match.Success)
		{
			string keytermsString = match.Value.Trim();
			var keyterms = keytermsString.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.ToList();
			return keyterms;
		}
		else
			return null;
	}
}