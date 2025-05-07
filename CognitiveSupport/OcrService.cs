using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace CognitiveSupport;

public class OcrService : IOcrService
{
	private const int RetryDelayMilliseconds = 500;
	private const double TimeoutMultiplier = 7.5;
	private const int MinimumImageWidth = 50;
	private const int MinimumImageHeight = 50;

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

	private static Context CreateRetryContext() => new() { ["Attempt"] = 1 };

	private static AsyncPolicy CreateRetryPolicy() =>
		Policy
			.Handle<HttpRequestException>()
			.Or<TimeoutRejectedException>()
			.Or<TaskCanceledException>()
			.WaitAndRetryAsync(
				Backoff.LinearBackoff(TimeSpan.FromMilliseconds(RetryDelayMilliseconds), retryCount: 3, factor: 1),
				onRetry: (_, __, ___, ctx) =>
				{
					int attempt = ctx.ContainsKey("Attempt") ? (int)ctx["Attempt"] : 1;
					ctx["Attempt"] = ++attempt;
				});

	private static CancellationTokenSource CreateLinkedCancellationTokenSource(int attempt, CancellationToken overallToken)
	{
		var perTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutMultiplier * attempt));
		return CancellationTokenSource.CreateLinkedTokenSource(overallToken, perTryCts.Token);
	}

	private async Task<string> ExecuteReadFileInternal(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		Context context,
		CancellationToken overallCancellationToken)
	{
		int attempt = (int)context["Attempt"];
		using var linkedCts = CreateLinkedCancellationTokenSource(attempt, overallCancellationToken);

		try
		{
			if (attempt > 0) this.Beep(attempt);

			imageStream.Seek(0, SeekOrigin.Begin);

			return await ReadFileInternal(ocrReadingOrder, imageStream, linkedCts.Token).ConfigureAwait(false);
		}
		finally
		{
			linkedCts.Dispose();
		}
	}

	private async Task<string> ReadFile(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		var retryPolicy = CreateRetryPolicy();
		var context = CreateRetryContext();

		return await retryPolicy.ExecuteAsync(
			(ctx, overallToken) => ExecuteReadFileInternal(ocrReadingOrder, imageStream, ctx, overallToken),
			context,
			overallCancellationToken).ConfigureAwait(false);
	}

	private Stream EnsureMinimumImageSize(Stream imageStream)
	{
		using var image = Image.FromStream(imageStream);

		if (image.Width >= MinimumImageWidth && image.Height >= MinimumImageHeight)
		{
			imageStream.Seek(0, SeekOrigin.Begin);
			return imageStream;
		}

		int newWidth = Math.Max(MinimumImageWidth, image.Width);
		int newHeight = Math.Max(MinimumImageHeight, image.Height);

		using var paddedImage = new Bitmap(newWidth, newHeight);
		using (var graphics = Graphics.FromImage(paddedImage))
		{
			graphics.Clear(Color.White);
			int offsetX = (newWidth - image.Width) / 2;
			int offsetY = (newHeight - image.Height) / 2;
			graphics.DrawImage(image, offsetX, offsetY);
		}

		var paddedStream = new MemoryStream();
		paddedImage.Save(paddedStream, ImageFormat.Png);
		paddedStream.Seek(0, SeekOrigin.Begin);

		imageStream.Dispose();

		return paddedStream;
	}

	private async Task<string> ReadFileInternal(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken cancellationToken)
	{
		const int operationIdLength = 36;

		imageStream = EnsureMinimumImageSize(imageStream);

		Log("----------------------------------------------------------");

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
