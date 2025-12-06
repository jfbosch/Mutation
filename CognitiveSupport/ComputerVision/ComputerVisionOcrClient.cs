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
using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Wraps the Azure Computer Vision Read API to provide document OCR capabilities for Mutation.
/// </summary>
public sealed class ComputerVisionOcrClient : IDocumentOcrClient
{
	private const string Placeholder = "<placeholder>";
	private readonly ImageAnalysisClient _client;
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

		_client = new ImageAnalysisClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
	}

	public async Task<DocumentOcrJobResult> AnalyzeAsync(DocumentOcrJobInput job, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		ThrowIfDisposed();

		try
		{
			await using var stream = await job.StreamFactory(cancellationToken).ConfigureAwait(false);
			stream.Seek(0, SeekOrigin.Begin);

			var binaryData = BinaryData.FromStream(stream);

			var result = await _client.AnalyzeAsync(
				binaryData,
				VisualFeatures.Read,
				new ImageAnalysisOptions { },
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var text = ExtractText(result);
			return new DocumentOcrJobResult(DocumentOcrJobStatus.Completed, text, null, null, null, null);
		}
		catch (RequestFailedException ex)
		{
			return new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, ex.Message, null, null, null);
		}
	}

	private static string ExtractText(ImageAnalysisResult result)
	{
		if (result?.Read?.Blocks is null)
		{
			return string.Empty;
		}

		var sb = new StringBuilder();
		foreach (var block in result.Read.Blocks)
		{
			foreach (var line in block.Lines)
			{
				sb.AppendLine(line.Text);
			}
		}

		return sb.ToString().FixNewLines();
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

		// ImageAnalysisClient does not implement IDisposable
		_disposed = true;
		return ValueTask.CompletedTask;
	}
}
