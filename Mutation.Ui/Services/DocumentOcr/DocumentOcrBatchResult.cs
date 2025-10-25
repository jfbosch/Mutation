using System;
using System.Collections.Generic;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DocumentOcrBatchResult
{
	public DocumentOcrBatchResult(
		Guid batchId,
		string sourceName,
	IReadOnlyList<(DocumentOcrJobInput Job, DocumentOcrJobResult Result)> jobs,
	string aggregatedText,
	bool truncated)
	{
	BatchId = batchId;
	SourceName = sourceName;
	Jobs = jobs;
	AggregatedText = aggregatedText;
	TruncatedByFreeTier = truncated;
	}

	public Guid BatchId { get; }

	public string SourceName { get; }

	public IReadOnlyList<(DocumentOcrJobInput Job, DocumentOcrJobResult Result)> Jobs { get; }

	public string AggregatedText { get; }

	public bool TruncatedByFreeTier { get; }
}
