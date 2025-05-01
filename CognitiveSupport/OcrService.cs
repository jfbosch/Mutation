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
	private string SubscriptionKey { get; }
	private string Endpoint { get; }
	private ComputerVisionClient ComputerVisionClient { get; }
	private readonly object _lock = new();

	public OcrService(string? subscriptionKey, string? endpoint)
	{
		SubscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
		Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		ComputerVisionClient = CreateComputerVisionClient(Endpoint, SubscriptionKey);
	}

	public Task<string> ExtractText(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		return ReadFile(ocrReadingOrder, imageStream, overallCancellationToken);
	}

	private static ComputerVisionClient CreateComputerVisionClient(string endpoint, string key) =>
		 new(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };

	private async Task<string> ReadFile(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		const string attemptKey = "Attempt";
		var delay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(500), retryCount: 3, factor: 1);

		var retryPolicy = Policy
			 .Handle<HttpRequestException>()
			 .Or<TimeoutRejectedException>()
			 .Or<TaskCanceledException>()
			 .WaitAndRetryAsync(
				  delay,
				  onRetry: (_, __, ___, ctx) =>
				  {
					  int attempt = ctx.ContainsKey(attemptKey) ? (int)ctx[attemptKey] : 1;
					  ctx[attemptKey] = ++attempt;
				  });

		var context = new Context { [attemptKey] = 1 };

		return await retryPolicy.ExecuteAsync(async (ctx, overallToken) =>
		{
			int attempt = (int)ctx[attemptKey];
			using var perTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(7.5 * attempt));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken, perTryCts.Token);

			if (attempt > 0) this.Beep(attempt);

			return await ReadFileInternal(ocrReadingOrder, imageStream, linkedCts.Token).ConfigureAwait(false);

		}, context, overallCancellationToken).ConfigureAwait(false);
	}

	private async Task<string> ReadFileInternal(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken cancellationToken)
	{
		const int operationIdLength = 36;

		Log("----------------------------------------------------------");
		Log("READ FROM file");

		var headers = await ComputerVisionClient.ReadInStreamAsync(
								imageStream,
								readingOrder: ocrReadingOrder.ToEnumMemberValue(),
								cancellationToken: cancellationToken)
						  .ConfigureAwait(false);

		string operationId = headers.OperationLocation[^operationIdLength..];

		var results = await GetReadOperationResultAsync(operationId, cancellationToken).ConfigureAwait(false);

		return ExtractTextFromResults(results);
	}

	private async Task<ReadOperationResult> GetReadOperationResultAsync(
		string operationId,
		CancellationToken cancellationToken)
	{
		TimeSpan defaultDelay = TimeSpan.FromMilliseconds(150);

		while (true)
		{
			var response = await ComputerVisionClient
								  .GetReadResultWithHttpMessagesAsync(Guid.Parse(operationId), cancellationToken: cancellationToken)
								  .ConfigureAwait(false);

			var result = response.Body;

			if (result.Status is OperationStatusCodes.Succeeded or OperationStatusCodes.Failed)
				return result;

			TimeSpan wait = response.Response.Headers.RetryAfter?.Delta ?? defaultDelay;
			await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
		}
	}

	private static string ExtractTextFromResults(ReadOperationResult results)
	{
		var sb = new StringBuilder();

		foreach (ReadResult page in results.AnalyzeResult.ReadResults)
		{
			foreach (Line line in page.Lines)
			{
				Console.WriteLine(line.Text);
				sb.AppendLine(line.Text);
			}
		}

		Console.WriteLine();
		return sb.ToString();
	}

	private static void Log(string message) => Console.WriteLine(message);
}
