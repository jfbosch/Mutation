using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Rest;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Wraps the Azure Computer Vision Read API to provide document OCR capabilities for Mutation.
/// </summary>
public sealed class ComputerVisionOcrClient : IDocumentOcrClient
{
	private const string Placeholder = "<placeholder>";
	private readonly ComputerVisionClient _client;
	private readonly TimeSpan _defaultPollDelay = TimeSpan.FromSeconds(2);
	private bool _disposed;

	public ComputerVisionOcrClient(AzureComputerVisionSettings? settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.Equals(settings.ApiKey, Placeholder, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Azure Computer Vision API key is not configured.");
		}

		if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.Equals(settings.Endpoint, Placeholder, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Azure Computer Vision endpoint is not configured.");
		}

		_client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(settings.ApiKey))
		{
			Endpoint = settings.Endpoint
		};
	}

	public async Task<DocumentOcrJobResult> AnalyzeAsync(DocumentOcrJobInput job, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		ThrowIfDisposed();

		try
		{
			await using var stream = await job.StreamFactory(cancellationToken).ConfigureAwait(false);
			stream.Seek(0, SeekOrigin.Begin);

			var headers = await _client.ReadInStreamAsync(
				stream,
				job.ContentType,
				readingOrder: "naturalReadOrder",
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var operationId = ExtractOperationId(headers.OperationLocation);
			return await PollForCompletionAsync(operationId, cancellationToken).ConfigureAwait(false);
		}
		catch (ComputerVisionErrorResponseException ex)
		{
			var retry = TryParseRetryAfter(ex.Response?.Headers);
			var message = ex.Body?.Error?.Message ?? ex.Message;
			return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, message, retry, retry, null);
		}
		catch (HttpOperationException ex)
		{
			var retry = TryParseRetryAfter(ex.Response?.Headers);
			return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, ex.Message, retry, retry, null);
		}
	}

	private async Task<DocumentOcrJobResult> PollForCompletionAsync(string operationId, CancellationToken cancellationToken)
	{
		TimeSpan? lastSuggestedDelay = null;

		while (true)
		{
			var response = await _client.GetReadResultWithHttpMessagesAsync(Guid.Parse(operationId), cancellationToken: cancellationToken).ConfigureAwait(false);
			var retryAfter = TryParseRetryAfter(response.Response.Headers);
			var status = response.Body.Status;

			if (status == OperationStatusCodes.Succeeded)
			{
				var text = ExtractText(response.Body);
				return new DocumentOcrJobResult(DocumentOcrJobStatus.Completed, text, null, retryAfter, lastSuggestedDelay ?? retryAfter, operationId);
			}

			if (status == OperationStatusCodes.Failed)
			{
				var message = $"Computer Vision failed to process the document (status: {status}).";
				return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, message, retryAfter, lastSuggestedDelay ?? retryAfter, operationId);
			}

			var wait = retryAfter ?? _defaultPollDelay;
			lastSuggestedDelay = wait;
			await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
		}
	}

	private static string ExtractOperationId(string? operationLocation)
	{
		if (string.IsNullOrWhiteSpace(operationLocation))
		{
			throw new InvalidOperationException("The Computer Vision service did not return an operation location.");
		}

		var lastSlash = operationLocation.LastIndexOf('/') + 1;
		return operationLocation[lastSlash..];
	}

	private static string ExtractText(ReadOperationResult result)
	{
		if (result?.AnalyzeResult?.ReadResults is null)
		{
			return string.Empty;
		}

		var sb = new StringBuilder();
		foreach (var page in result.AnalyzeResult.ReadResults)
		{
			foreach (var line in page.Lines ?? Array.Empty<Line>())
			{
				sb.AppendLine(line.Text);
			}
		}

		return sb.ToString().FixNewLines();
	}

	private static TimeSpan? TryParseRetryAfter(HttpResponseHeaders? headers)
	{
		if (headers is null)
		{
			return null;
		}

		if (headers.RetryAfter?.Delta is TimeSpan delta)
		{
			return delta;
		}

		if (headers.RetryAfter?.Date is DateTimeOffset date)
		{
			return NormalizeDelay(date - DateTimeOffset.UtcNow);
		}

		return headers.TryGetValues("Retry-After", out var values)
			? ParseRetryAfterValues(values)
			: null;
	}

	private static TimeSpan? TryParseRetryAfter(IDictionary<string, IEnumerable<string>>? headers)
	{
		if (headers is null)
		{
			return null;
		}

		return headers.TryGetValue("Retry-After", out var values)
			? ParseRetryAfterValues(values)
			: headers.TryGetValue("retry-after", out var lower)
				? ParseRetryAfterValues(lower)
				: null;
	}

	private static TimeSpan? ParseRetryAfterValues(IEnumerable<string>? values)
	{
		var first = values?.FirstOrDefault();
		if (string.IsNullOrWhiteSpace(first))
		{
			return null;
		}

		if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
		{
			return TimeSpan.FromSeconds(Math.Max(0, seconds));
		}

		if (DateTimeOffset.TryParse(first, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
		{
			return NormalizeDelay(date - DateTimeOffset.UtcNow);
		}

		return null;
	}

	private static TimeSpan? NormalizeDelay(TimeSpan delay)
	{
		return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ComputerVisionOcrClient));
		}
	}

	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_client.Dispose();
		_disposed = true;
		return ValueTask.CompletedTask;
	}
}
