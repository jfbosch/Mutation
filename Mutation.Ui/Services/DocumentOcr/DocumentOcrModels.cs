using CognitiveSupport;
using System.Collections.Immutable;

namespace Mutation.Ui.Services.DocumentOcr;

/// <summary>
/// Represents a batch of OCR jobs generated from a single source document.
/// </summary>
public sealed class DocumentOcrBatch
{
	public DocumentOcrBatch(string sourceName, string originalExtension, IReadOnlyList<DocumentOcrJobInput> jobs)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		SourceName = sourceName;
		OriginalExtension = originalExtension ?? string.Empty;
		Jobs = jobs?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(jobs));
	}

	public string SourceName { get; }

	public string OriginalExtension { get; }

	public IReadOnlyList<DocumentOcrJobInput> Jobs { get; }

	public int TotalJobs => Jobs.Count;
}

/// <summary>
/// Describes a single OCR job to be sent to Azure Document Intelligence.
/// </summary>
public sealed class DocumentOcrJobInput
{
	private readonly Func<CancellationToken, Task<Stream>> _streamFactory;

	public DocumentOcrJobInput(
		string sourceName,
		int pageNumber,
		DocumentOcrContentType contentType,
		Func<CancellationToken, Task<Stream>> streamFactory,
		long? originalLength)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		if (pageNumber <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(pageNumber));
		}

		_streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));

		SourceName = sourceName;
		PageNumber = pageNumber;
		ContentType = contentType;
		OriginalLength = originalLength;
	}

	public string SourceName { get; }

	public int PageNumber { get; }

	public DocumentOcrContentType ContentType { get; }

	public long? OriginalLength { get; }

	public Task<Stream> OpenStreamAsync(CancellationToken cancellationToken)
		=> _streamFactory(cancellationToken);
}

/// <summary>
/// Supported MIME groupings for Azure Document Intelligence submissions.
/// </summary>
public enum DocumentOcrContentType
{
	Pdf,
	Jpeg,
	Png,
	Tiff,
	Bmp
}

/// <summary>
/// Descriptor representing a file selected by the user that will be preprocessed for OCR.
/// </summary>
public sealed class DocumentSourceDescriptor
{
	private readonly Func<CancellationToken, Task<Stream>> _streamFactory;

	public DocumentSourceDescriptor(
		string fileName,
		Func<CancellationToken, Task<Stream>> streamFactory,
		long? lengthBytes = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		_streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));

		FileName = fileName;
		LengthBytes = lengthBytes;
	}

	public string FileName { get; }

	public string Extension => Path.GetExtension(FileName);

	public string BaseName => Path.GetFileNameWithoutExtension(FileName);

	public long? LengthBytes { get; }

	public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
		=> _streamFactory(cancellationToken);

	public static DocumentSourceDescriptor FromBytes(string fileName, byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		return new DocumentSourceDescriptor(
			fileName,
			_ => Task.FromResult<Stream>(new MemoryStream(data, writable: false)),
			data.LongLength);
	}
}