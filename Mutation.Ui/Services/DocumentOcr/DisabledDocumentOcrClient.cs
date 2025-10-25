using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DisabledDocumentOcrClient : IDocumentOcrClient
{
	public Task<DocumentOcrJobResult> AnalyzeAsync(DocumentOcrJobInput job, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new DocumentOcrJobResult(DocumentOcrJobStatus.Failed, null, "Configure Azure Computer Vision to enable document OCR.", null, null, null));
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}
}
