using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

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

	private async Task<string> ReadFile(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		const string attemptKey = "Attempt";
		var delay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(RetryDelayMilliseconds), retryCount: 3, factor: 1);

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
			using var perTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutMultiplier * attempt));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken, perTryCts.Token);

			if (attempt > 0) this.Beep(attempt);

			// Reset the stream position before retrying
			imageStream.Seek(0, SeekOrigin.Begin);

			return await ReadFileInternal(ocrReadingOrder, imageStream, linkedCts.Token).ConfigureAwait(false);

		}, context, overallCancellationToken).ConfigureAwait(false);
	}

	// This method ensures that images meet Azure OCR's minimum size requirement of 50x50 pixels.
	// If an image is smaller than the required dimensions, it is padded with a neutral background color to comply with the requirement.
	private Stream EnsureMinimumImageSize(Stream imageStream)
	{
		using var image = Image.FromStream(imageStream);

		// Check if the image dimensions are already >= 50x50
		if (image.Width >= MinimumImageWidth && image.Height >= MinimumImageHeight)
		{
			imageStream.Seek(0, SeekOrigin.Begin); // Reset stream position
			return imageStream;
		}

		// Calculate new dimensions and padding
		int newWidth = Math.Max(MinimumImageWidth, image.Width);
		int newHeight = Math.Max(MinimumImageHeight, image.Height);

		// Create a new canvas with the required dimensions
		using var paddedImage = new Bitmap(newWidth, newHeight);
		using (var graphics = Graphics.FromImage(paddedImage))
		{
			graphics.Clear(Color.White); // Fill with a neutral background color
			int offsetX = (newWidth - image.Width) / 2;
			int offsetY = (newHeight - image.Height) / 2;
			graphics.DrawImage(image, offsetX, offsetY);
		}

		// Save the padded image to a new memory stream
		var paddedStream = new MemoryStream();
		paddedImage.Save(paddedStream, ImageFormat.Png);
		paddedStream.Seek(0, SeekOrigin.Begin);

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
