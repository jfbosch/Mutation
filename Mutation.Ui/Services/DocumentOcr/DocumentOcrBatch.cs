using System;
using System.Collections.Generic;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DocumentOcrBatch
{
	public DocumentOcrBatch(string sourceName, IReadOnlyList<DocumentOcrJobInput> jobs, bool truncated)
	{
		SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
		Jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
		TruncatedByFreeTierLimit = truncated;
	}

	public string SourceName { get; }

	public IReadOnlyList<DocumentOcrJobInput> Jobs { get; }

	public bool TruncatedByFreeTierLimit { get; }

	public int TotalJobs => Jobs.Count;
}
