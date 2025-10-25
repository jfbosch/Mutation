using System;
using System.Threading;
using System.Threading.Tasks;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Abstraction over the Azure Computer Vision client used for document OCR workflows.
/// </summary>
public interface IDocumentOcrClient : IAsyncDisposable
{
	Task<DocumentOcrJobResult> AnalyzeAsync(DocumentOcrJobInput job, CancellationToken cancellationToken = default);
}
