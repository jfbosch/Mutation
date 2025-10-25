using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using CognitiveSupport.ComputerVision;
using Windows.Storage;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DocumentOcrWorkflowService
{
	private readonly IDocumentOcrClient _ocrClient;
	private readonly DocumentFilePreprocessor _preprocessor;
	private readonly Settings _settings;
	private readonly ApiRateLimiter _rateLimiter;

	public DocumentOcrWorkflowService(
		IDocumentOcrClient ocrClient,
		DocumentFilePreprocessor preprocessor,
		Settings settings,
		ApiRateLimiter rateLimiter)
	{
		_ocrClient = ocrClient;
		_preprocessor = preprocessor;
		_settings = settings;
		_rateLimiter = rateLimiter;
	}

	public async Task<IReadOnlyList<DocumentOcrBatchResult>> ProcessAsync(
		IEnumerable<StorageFile> files,
		IProgress<DocumentOcrProgressUpdate>? progress,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(files);
		var configuration = _settings.AzureComputerVisionSettings ?? throw new InvalidOperationException("Configure Azure Computer Vision under Settings to enable OCR.");

		var fileList = files.ToList();
		var batches = new List<(Guid BatchId, DocumentOcrBatch Batch)>();

		foreach (var file in fileList)
		{
			var batch = await Task.Run(() => _preprocessor.PrepareAsync(file, configuration, cancellationToken), cancellationToken).ConfigureAwait(false);
			var batchId = Guid.NewGuid();
			batches.Add((batchId, batch));
			foreach (var job in batch.Jobs)
			{
				progress?.Report(new DocumentOcrProgressUpdate(batchId, batch.SourceName, job.Id, job.PageLabel, DocumentOcrJobStatus.Queued, 0, batch.TotalJobs, 0, null, null, null));
			}
		}

		var documentSemaphore = new SemaphoreSlim(Math.Max(1, configuration.MaxParallelDocuments));
		var tasks = batches.Select(tuple => ProcessBatchAsync(tuple.BatchId, tuple.Batch, documentSemaphore, progress, cancellationToken));
		var processed = await Task.WhenAll(tasks).ConfigureAwait(false);
		return processed;
	}

	private async Task<DocumentOcrBatchResult> ProcessBatchAsync(
		Guid batchId,
		DocumentOcrBatch batch,
		SemaphoreSlim documentSemaphore,
		IProgress<DocumentOcrProgressUpdate>? progress,
		CancellationToken cancellationToken)
	{
		await documentSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var jobResults = new List<(DocumentOcrJobInput, DocumentOcrJobResult)>();
			var aggregated = new List<string>();
			var completedJobs = 0;
			var totalJobs = batch.TotalJobs;

			foreach (var job in batch.Jobs)
			{
				cancellationToken.ThrowIfCancellationRequested();
				progress?.Report(new DocumentOcrProgressUpdate(batchId, batch.SourceName, job.Id, job.PageLabel, DocumentOcrJobStatus.Running, completedJobs, totalJobs, CalculateProgress(completedJobs, totalJobs), null, null, string.Join(Environment.NewLine, aggregated)));

				DocumentOcrJobResult result;
				try
				{
					using (await _rateLimiter.AcquireAsync(cancellationToken).ConfigureAwait(false))
					{
						result = await _ocrClient.AnalyzeAsync(job, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					result = new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, ex.Message, null, null, null);
				}

				if (result.Status == DocumentOcrJobStatus.Completed)
				{
					completedJobs++;
					if (!string.IsNullOrWhiteSpace(result.Text))
					{
						aggregated.Add(result.Text);
					}
				}

				jobResults.Add((job, result));

				progress?.Report(new DocumentOcrProgressUpdate(
					batchId,
					batch.SourceName,
					job.Id,
					job.PageLabel,
					result.Status,
					completedJobs,
					totalJobs,
					CalculateProgress(completedJobs, totalJobs),
					result.RetryAfter ?? result.SuggestedPollDelay,
					result.ErrorMessage,
					string.Join(Environment.NewLine, aggregated)));
			}

			var aggregatedText = string.Join(Environment.NewLine, aggregated).Trim();
			return new DocumentOcrBatchResult(batchId, batch.SourceName, jobResults, aggregatedText, batch.TruncatedByFreeTierLimit);
		}
		finally
		{
			documentSemaphore.Release();
		}
	}

	private static double CalculateProgress(int completed, int total)
	{
		return total <= 0 ? 0 : (double)completed / total;
	}
}
