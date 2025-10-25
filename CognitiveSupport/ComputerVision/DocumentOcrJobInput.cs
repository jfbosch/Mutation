using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Represents a single page or image to be processed by the Computer Vision service.
/// </summary>
public sealed class DocumentOcrJobInput
{
	public DocumentOcrJobInput(
		Guid id,
		Func<CancellationToken, Task<Stream>> streamFactory,
		string contentType,
		int pageNumber,
		string pageLabel)
	{
		Id = id;
		StreamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		PageNumber = pageNumber;
		PageLabel = pageLabel ?? throw new ArgumentNullException(nameof(pageLabel));
	}

	public Guid Id { get; }

	public Func<CancellationToken, Task<Stream>> StreamFactory { get; }

	public string ContentType { get; }

	public int PageNumber { get; }

	public string PageLabel { get; }
}
