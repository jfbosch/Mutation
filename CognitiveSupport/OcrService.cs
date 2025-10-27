using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Rest;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CognitiveSupport;

public class OcrService : IOcrService, IDisposable
{
	private const int RetryDelayMilliseconds = 500;
        private const int MinimumImageWidth = 50;
        private const int MinimumImageHeight = 50;
        private const int MaxTimeoutSeconds = 60;

        private string SubscriptionKey { get; }
        private string Endpoint { get; }
        private ComputerVisionClient ComputerVisionClient { get; }
        private readonly int _timeoutSeconds;
        private readonly RequestRateLimiter _rateLimiter = new(20, TimeSpan.FromMinutes(1));

	public OcrService(string? subscriptionKey, string? endpoint, int timeoutSeconds = 30)
	{
		SubscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
		Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		ComputerVisionClient = CreateComputerVisionClient(Endpoint, SubscriptionKey);
		_timeoutSeconds = Math.Max(1, Math.Min(timeoutSeconds, MaxTimeoutSeconds));
	}

	public Task<string> ExtractText(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		return Read(ocrReadingOrder, imageStream, overallCancellationToken);
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

	private CancellationTokenSource CreateLinkedCancellationTokenSource(int attempt, CancellationToken overallToken)
	{
		// Cap the per-try timeout to avoid excessive waits
		int perTryTimeout = Math.Max(1, Math.Min(_timeoutSeconds, MaxTimeoutSeconds));
		var perTryCts = new CancellationTokenSource(TimeSpan.FromSeconds(perTryTimeout));
		return CancellationTokenSource.CreateLinkedTokenSource(overallToken, perTryCts.Token);
	}

	private async Task<string> ExecuteReadInternal(
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

			return await ReadInternal(ocrReadingOrder, imageStream, linkedCts.Token).ConfigureAwait(false);
		}
		finally
		{
			linkedCts.Dispose();
		}
	}

	private async Task<string> Read(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken overallCancellationToken)
	{
		// Buffer the stream into a byte array so we can create a new stream for each retry
		byte[] imageBytes;
		if (imageStream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer) && buffer.Array != null)
		{
			imageBytes = buffer.Array;
		}
		else
		{
			using (var tempMs = new MemoryStream())
			{
				imageStream.Seek(0, SeekOrigin.Begin);
				imageStream.CopyTo(tempMs);
				imageBytes = tempMs.ToArray();
			}
		}

		var retryPolicy = CreateRetryPolicy();
		var context = CreateRetryContext();

		return await retryPolicy.ExecuteAsync(
			(ctx, overallToken) =>
			{
				var ms = new MemoryStream(imageBytes ?? Array.Empty<byte>(), writable: false);
				return ExecuteReadInternal(ocrReadingOrder, ms, ctx, overallToken);
			},
			context,
			overallCancellationToken).ConfigureAwait(false);
	}

        private Stream EnsureMinimumImageSize(Stream imageStream)
        {
                if (!imageStream.CanSeek)
                        return imageStream;

                imageStream.Seek(0, SeekOrigin.Begin);

                try
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

                        return paddedStream;
                }
                catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or InvalidOperationException)
                {
                        imageStream.Seek(0, SeekOrigin.Begin);
                        return imageStream;
                }
        }

	private async Task<string> ReadInternal(
		OcrReadingOrder ocrReadingOrder,
		Stream imageStream,
		CancellationToken cancellationToken)
	{
		const int operationIdLength = 36;

		imageStream = EnsureMinimumImageSize(imageStream);

                await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                using HttpOperationHeaderResponse<ReadInStreamHeaders> response = await ComputerVisionClient
                                                                  .ReadInStreamWithHttpMessagesAsync(
                                                                          imageStream,
                                                                          readingOrder: ocrReadingOrder.ToEnumMemberValue(),
                                                                          cancellationToken: cancellationToken)
                                                                  .ConfigureAwait(false);

                string operationId = response.Headers.OperationLocation[^operationIdLength..];
                TimeSpan initialDelay = TryParseRetryAfter(response.Response.Headers) ?? TimeSpan.Zero;

                var results = await GetReadOperationResultAsync(operationId, initialDelay, cancellationToken).ConfigureAwait(false);

		return ExtractTextFromResults(results);
	}

        private async Task<ReadOperationResult> GetReadOperationResultAsync(
                string operationId,
                TimeSpan initialDelay,
                CancellationToken cancellationToken)
        {
                TimeSpan defaultDelay = TimeSpan.FromMilliseconds(150);

                TimeSpan delayBeforeFirstPoll = initialDelay > TimeSpan.Zero ? initialDelay : defaultDelay;
                if (delayBeforeFirstPoll > TimeSpan.Zero)
                        await Task.Delay(delayBeforeFirstPoll, cancellationToken).ConfigureAwait(false);

                while (true)
                {
                        await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                        var response = await ComputerVisionClient
                                                                  .GetReadResultWithHttpMessagesAsync(Guid.Parse(operationId), cancellationToken: cancellationToken)
                                                                  .ConfigureAwait(false);

			var result = response.Body;

			if (result.Status is OperationStatusCodes.Succeeded or OperationStatusCodes.Failed)
				return result;

                        TimeSpan wait = TryParseRetryAfter(response.Response.Headers) ?? defaultDelay;
                        if (wait <= TimeSpan.Zero)
                                wait = defaultDelay;

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
                                sb.AppendLine(line.Text);
                        }
                }

                return sb.ToString();
        }

        private static TimeSpan? TryParseRetryAfter(HttpResponseHeaders? headers)
        {
                if (headers is null)
                        return null;

                if (headers.RetryAfter?.Delta is TimeSpan delta)
                        return NormalizeDelay(delta);

                if (headers.RetryAfter?.Date is DateTimeOffset date)
                        return NormalizeDelay(date - DateTimeOffset.UtcNow);

                return headers.TryGetValues("Retry-After", out IEnumerable<string>? values)
                        ? ParseRetryAfterValues(values)
                        : null;
        }

        private static TimeSpan? ParseRetryAfterValues(IEnumerable<string>? values)
        {
                string? first = values?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(first))
                        return null;

                if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
                        return NormalizeDelay(TimeSpan.FromSeconds(Math.Max(0, seconds)));

                if (DateTimeOffset.TryParse(first, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset date))
                        return NormalizeDelay(date - DateTimeOffset.UtcNow);

                return null;
        }

        private static TimeSpan? NormalizeDelay(TimeSpan delay)
        {
                return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay;
        }


        public void Dispose()
        {
                ComputerVisionClient?.Dispose();
        }

        private sealed class RequestRateLimiter
        {
                private readonly int _limit;
                private readonly TimeSpan _window;
                private readonly Queue<DateTime> _timestamps = new();
                private readonly SemaphoreSlim _mutex = new(1, 1);

                public RequestRateLimiter(int limit, TimeSpan window)
                {
                        _limit = limit;
                        _window = window;
                }

                public async Task WaitAsync(CancellationToken token)
                {
                        while (true)
                        {
                                token.ThrowIfCancellationRequested();

                                TimeSpan delay = TimeSpan.Zero;

                                await _mutex.WaitAsync(token).ConfigureAwait(false);
                                try
                                {
                                        var now = DateTime.UtcNow;

                                        while (_timestamps.Count > 0 && now - _timestamps.Peek() >= _window)
                                        {
                                                _timestamps.Dequeue();
                                        }

                                        if (_timestamps.Count < _limit)
                                        {
                                                _timestamps.Enqueue(now);
                                                return;
                                        }

                                        var oldest = _timestamps.Peek();
                                        delay = _window - (now - oldest);
                                        if (delay < TimeSpan.Zero)
                                                delay = TimeSpan.Zero;
                                }
                                finally
                                {
                                        _mutex.Release();
                                }

                                if (delay > TimeSpan.Zero)
                                {
                                        await Task.Delay(delay, token).ConfigureAwait(false);
                                }
                        }
                }
        }
}
