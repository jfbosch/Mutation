using CognitiveSupport;
using System.Collections.Generic;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mutation.Ui.Services.DocumentOcr;

/// <summary>
/// Generates Azure Document Intelligence OCR jobs by splitting PDFs and preparing image files.
/// </summary>
public sealed class DocumentFilePreprocessor
{
	private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".pdf",
		".png",
		".jpg",
		".jpeg",
		".tif",
		".tiff",
		".bmp"
	};

	private static readonly Dictionary<string, DocumentOcrContentType> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		[".pdf"] = DocumentOcrContentType.Pdf,
		[".png"] = DocumentOcrContentType.Png,
		[".jpg"] = DocumentOcrContentType.Jpeg,
		[".jpeg"] = DocumentOcrContentType.Jpeg,
		[".tif"] = DocumentOcrContentType.Tiff,
		[".tiff"] = DocumentOcrContentType.Tiff,
		[".bmp"] = DocumentOcrContentType.Bmp
	};

	private readonly AzureDocumentIntelligenceSettings _settings;

	public DocumentFilePreprocessor(AzureDocumentIntelligenceSettings settings)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	public async Task<DocumentOcrBatch> PreprocessAsync(DocumentSourceDescriptor descriptor, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(descriptor);
		string extension = descriptor.Extension;
		if (!_supportedExtensions.Contains(extension))
		{
			throw new NotSupportedException($"Unsupported document type: {extension}");
		}
		ValidateLength(descriptor);
		return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
			? await CreatePdfBatchAsync(descriptor, cancellationToken).ConfigureAwait(false)
			: await CreateSinglePageBatchAsync(descriptor, cancellationToken).ConfigureAwait(false);
	}

	private void ValidateLength(DocumentSourceDescriptor descriptor)
	{
		if (_settings.MaxDocumentBytes is null || descriptor.LengthBytes is null)
		{
			return;
		}
		if (descriptor.LengthBytes.Value > _settings.MaxDocumentBytes.Value)
		{
			throw new InvalidOperationException($"Document '{descriptor.FileName}' exceeds the configured size limit of {_settings.MaxDocumentBytes.Value} bytes.");
		}
	}

	private async Task<DocumentOcrBatch> CreatePdfBatchAsync(DocumentSourceDescriptor descriptor, CancellationToken cancellationToken)
	{
		await using Stream stream = await descriptor.OpenReadAsync(cancellationToken).ConfigureAwait(false);
		using PdfDocument pdfDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
		int totalPages = pdfDocument.PageCount;
		int limit = _settings.UseFreeTier
			? Math.Min(_settings.FreeTierPageLimit <= 0 ? 2 : _settings.FreeTierPageLimit, totalPages)
			: totalPages;
		var jobs = new List<DocumentOcrJobInput>(limit);
		for (int i = 0; i < limit; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			byte[] payload = ExtractPdfPage(pdfDocument, i);
			jobs.Add(new DocumentOcrJobInput(
				descriptor.BaseName,
				i + 1,
				DocumentOcrContentType.Pdf,
				_ => Task.FromResult<Stream>(new MemoryStream(payload, writable: false)),
				payload.LongLength));
		}
		return new DocumentOcrBatch(descriptor.BaseName, descriptor.Extension, jobs);
	}

	private static byte[] ExtractPdfPage(PdfDocument sourceDocument, int pageIndex)
	{
		using PdfDocument pageDocument = new();
		pageDocument.Version = sourceDocument.Version;
		pageDocument.Options.CompressContentStreams = true;
		pageDocument.AddPage(sourceDocument.Pages[pageIndex]);
		using MemoryStream memoryStream = new();
		pageDocument.Save(memoryStream, false);
		return memoryStream.ToArray();
	}

	private async Task<DocumentOcrBatch> CreateSinglePageBatchAsync(DocumentSourceDescriptor descriptor, CancellationToken cancellationToken)
	{
		byte[] payload = await ReadAllBytesAsync(descriptor, cancellationToken).ConfigureAwait(false);
		DocumentOcrContentType contentType = _contentTypes[descriptor.Extension];
		var job = new DocumentOcrJobInput(
			descriptor.BaseName,
			1,
			contentType,
			_ => Task.FromResult<Stream>(new MemoryStream(payload, writable: false)),
			payload.LongLength);
		return new DocumentOcrBatch(descriptor.BaseName, descriptor.Extension, new[] { job });
	}

	private static async Task<byte[]> ReadAllBytesAsync(DocumentSourceDescriptor descriptor, CancellationToken cancellationToken)
	{
		await using Stream stream = await descriptor.OpenReadAsync(cancellationToken).ConfigureAwait(false);
		using MemoryStream memoryStream = new();
		await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
		return memoryStream.ToArray();
	}
}