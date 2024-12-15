using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Text;

namespace CognitiveSupport;

public class OcrService : IOcrService
{
	private string SubscriptionKey { get; init; }
	private string Endpoint { get; init; }
	private ComputerVisionClient ComputerVisionClient { get; init; }
	private readonly object _lock = new();

	public OcrService(
		string? subscriptionKey,
		string? endpoint)
	{
		SubscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
		Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		ComputerVisionClient = CreateComputerVisionClient(Endpoint, SubscriptionKey);
	}

	public async Task<string> ExtractText(
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		return await ReadFile(imageStream, overallCancellationToken).ConfigureAwait(false);
	}

	private ComputerVisionClient CreateComputerVisionClient(
		string endpoint,
		string key)
	{
		ComputerVisionClient client =
			new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
			{
				Endpoint = endpoint
			};
		return client;
	}

	private async Task<string> ReadFile(
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
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

		string response = await retryPolicy.ExecuteAsync(async (context, overallToken) =>
		{
			int attempt = context.ContainsKey(AttemptKey) ? (int)context[AttemptKey] : 1;
			var thisTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(7.5 * attempt));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken, thisTryCts.Token);

			if (attempt > 0)
				this.Beep(attempt);

			return await ReadFileInternal(imageStream, linkedCts.Token).ConfigureAwait(false);

		}, context, overallCancellationToken).ConfigureAwait(false);

		return response;
	}


	private async Task<string> ReadFileInternal(
		Stream imageStream,
		CancellationToken cancellationToken)
	{
		const int delayMilliseconds = 500;
		const int numberOfCharsInOperationId = 36;

		Log("----------------------------------------------------------");
		Log("READ FROM file");

		var textHeaders = await this.ComputerVisionClient.ReadInStreamAsync(imageStream, cancellationToken: cancellationToken).ConfigureAwait(false);

		string operationLocation = textHeaders.OperationLocation;
		await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
		Log($"operationLocation {operationLocation}");

		string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

		var results = await GetReadOperationResultAsync(operationId, cancellationToken).ConfigureAwait(false);

		return ExtractTextFromResults(results);
	}

	private async Task<ReadOperationResult> GetReadOperationResultAsync(string operationId, CancellationToken cancellationToken)
	{
		ReadOperationResult results;
		do
		{
			results = await this.ComputerVisionClient.GetReadResultAsync(Guid.Parse(operationId), cancellationToken).ConfigureAwait(false);
		}
		while (results.Status == OperationStatusCodes.Running || results.Status == OperationStatusCodes.NotStarted);

		return results;
	}

	private string ExtractTextFromResults(ReadOperationResult results)
	{
		StringBuilder sb = new();
		var textUrlFileResults = results.AnalyzeResult.ReadResults;
		foreach (ReadResult page in textUrlFileResults)
		{
			foreach (Line line in page.Lines)
			{
				Log(line.Text);
				sb.AppendLine(line.Text);
			}
		}
		Log(string.Empty);

		return sb.ToString();
	}

	private void Log(string message)
	{
		// Replace this with a proper logging mechanism
		Console.WriteLine(message);
	}
}