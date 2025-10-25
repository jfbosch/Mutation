using System;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Represents the outcome of calling the Computer Vision Read API for a single job.
/// </summary>
public sealed class DocumentOcrJobResult
{
	public DocumentOcrJobResult(
		DocumentOcrJobStatus status,
		string? text,
		string? error,
		TimeSpan? retryAfter,
		TimeSpan? suggestedPollDelay,
		string? operationId)
	{
		Status = status;
		Text = text;
		ErrorMessage = error;
		RetryAfter = retryAfter;
		SuggestedPollDelay = suggestedPollDelay;
		OperationId = operationId;
	}

	public DocumentOcrJobStatus Status { get; }

	public string? Text { get; }

	public string? ErrorMessage { get; }

	public TimeSpan? RetryAfter { get; }

	public TimeSpan? SuggestedPollDelay { get; }

	public string? OperationId { get; }
}
