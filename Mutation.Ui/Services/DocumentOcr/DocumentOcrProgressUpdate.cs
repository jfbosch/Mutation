using System;
using System.Collections.Generic;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DocumentOcrProgressUpdate
{
public DocumentOcrProgressUpdate(
Guid batchId,
string sourceName,
Guid jobId,
string pageLabel,
DocumentOcrJobStatus status,
int completedJobs,
int totalJobs,
double progress,
TimeSpan? nextRetry,
string? message,
string? aggregatedText)
{
BatchId = batchId;
SourceName = sourceName;
JobId = jobId;
PageLabel = pageLabel;
Status = status;
CompletedJobs = completedJobs;
TotalJobs = totalJobs;
Progress = progress;
NextRetry = nextRetry;
Message = message;
AggregatedText = aggregatedText;
}

public Guid BatchId { get; }

public string SourceName { get; }

public Guid JobId { get; }

public string PageLabel { get; }

	public DocumentOcrJobStatus Status { get; }

	public int CompletedJobs { get; }

	public int TotalJobs { get; }

	public double Progress { get; }

	public TimeSpan? NextRetry { get; }

	public string? Message { get; }

	public string? AggregatedText { get; }
}
