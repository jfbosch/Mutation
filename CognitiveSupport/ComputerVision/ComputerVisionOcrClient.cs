using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Rest;
using System.Text;

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
	if (settings is null)
	throw new ArgumentNullException(nameof(settings));

	if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.Equals(settings.ApiKey, Placeholder, StringComparison.OrdinalIgnoreCase))
	throw new InvalidOperationException("Azure Computer Vision API key is not configured.");

	if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.Equals(settings.Endpoint, Placeholder, StringComparison.OrdinalIgnoreCase))
	throw new InvalidOperationException("Azure Computer Vision endpoint is not configured.");

	_client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(settings.ApiKey))
	{
	Endpoint = settings.Endpoint
	};
	}

	public async Task<DocumentOcrJobResult> AnalyzeAsync(DocumentOcrJobInput job, CancellationToken cancellationToken = default)
	{
	if (job is null)
	throw new ArgumentNullException(nameof(job));

	ThrowIfDisposed();

	try
	{
	await using var stream = await job.StreamFactory(cancellationToken).ConfigureAwait(false);
	stream.Seek(0, SeekOrigin.Begin);

	var headers = await _client.ReadInStreamAsync(
	stream,
	readingOrder: "naturalReadOrder",
	cancellationToken: cancellationToken).ConfigureAwait(false);

	string operationId = ExtractOperationId(headers.OperationLocation);
	return await PollForCompletionAsync(operationId, cancellationToken).ConfigureAwait(false);
	}
	catch (ComputerVisionErrorResponseException ex)
	{
	TimeSpan? retry = ex.Response?.Headers?.RetryAfter?.Delta;
	string message = ex.Body?.Error?.Message ?? ex.Message;
	return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, message, retry, retry, null);
	}
	catch (HttpOperationException ex)
	{
	TimeSpan? retry = ex.Response?.Headers?.RetryAfter?.Delta;
	return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, ex.Message, retry, retry, null);
	}
	}

	private async Task<DocumentOcrJobResult> PollForCompletionAsync(string operationId, CancellationToken cancellationToken)
	{
	TimeSpan? lastSuggestedDelay = null;

	while (true)
	{
	var response = await _client.GetReadResultWithHttpMessagesAsync(Guid.Parse(operationId), cancellationToken: cancellationToken).ConfigureAwait(false);
	var retryAfter = response.Response.Headers.RetryAfter?.Delta;
	var status = response.Body.Status;

	if (status == OperationStatusCodes.Succeeded)
	{
	string text = ExtractText(response.Body);
	return new DocumentOcrJobResult(DocumentOcrJobStatus.Completed, text, null, retryAfter, lastSuggestedDelay ?? retryAfter, operationId);
	}

	if (status == OperationStatusCodes.Failed)
	{
	string message = response.Body.AnalyzeResult?.Errors?.FirstOrDefault()?.Message ?? "Computer Vision failed to process the document.";
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
	throw new InvalidOperationException("The Computer Vision service did not return an operation location.");

	int lastSlash = operationLocation.LastIndexOf('/') + 1;
	return operationLocation[lastSlash..];
	}

	private static string ExtractText(ReadOperationResult result)
	{
	if (result?.AnalyzeResult?.ReadResults is null)
	return string.Empty;

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

	private void ThrowIfDisposed()
	{
	if (_disposed)
	throw new ObjectDisposedException(nameof(ComputerVisionOcrClient));
	}

	public ValueTask DisposeAsync()
	{
	if (_disposed)
	return ValueTask.CompletedTask;

	_client.Dispose();
	_disposed = true;
	return ValueTask.CompletedTask;
	}
}
