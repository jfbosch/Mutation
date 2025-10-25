using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using CognitiveSupport.ComputerVision;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Windows.Storage;

namespace Mutation.Ui.Services.DocumentOcr;

public sealed class DocumentFilePreprocessor
{
	private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		[".pdf"] = "application/pdf",
		[".png"] = "image/png",
		[".jpg"] = "image/jpeg",
		[".jpeg"] = "image/jpeg",
		[".tif"] = "image/tiff",
		[".tiff"] = "image/tiff",
		[".bmp"] = "image/bmp"
	};

	public async Task<DocumentOcrBatch> PrepareAsync(StorageFile file, AzureComputerVisionSettings settings, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(file);
		ArgumentNullException.ThrowIfNull(settings);

		var extension = Path.GetExtension(file.Name);
		if (string.IsNullOrWhiteSpace(extension) || !SupportedContentTypes.TryGetValue(extension, out var contentType))
		{
			throw new InvalidOperationException($"Unsupported file type: {extension ?? "(none)"}.");
		}

		var properties = await file.GetBasicPropertiesAsync();
		if (settings.MaxDocumentBytes.HasValue && (long)properties.Size > settings.MaxDocumentBytes.Value)
		{
			throw new InvalidOperationException($"{file.Name} exceeds the configured size limit of {settings.MaxDocumentBytes.Value / 1024 / 1024} MB.");
		}

		return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
			? await ProcessPdfAsync(file, settings, contentType, cancellationToken)
			: await ProcessImageAsync(file, contentType, cancellationToken);
	}

	private static async Task<DocumentOcrBatch> ProcessImageAsync(StorageFile file, string contentType, CancellationToken cancellationToken)
	{
		await using var stream = await file.OpenReadAsync();
		using var managed = stream.AsStreamForRead();
		using var memory = new MemoryStream();
		await managed.CopyToAsync(memory, 81920, cancellationToken).ConfigureAwait(false);
		var buffer = memory.ToArray();

		var job = new DocumentOcrJobInput(
			Guid.NewGuid(),
			_ => Task.FromResult<Stream>(new MemoryStream(buffer, writable: false)),
			contentType,
			1,
			file.Name);

		return new DocumentOcrBatch(file.Name, new[] { job }, false);
	}

	private static async Task<DocumentOcrBatch> ProcessPdfAsync(StorageFile file, AzureComputerVisionSettings settings, string contentType, CancellationToken cancellationToken)
	{
		await using var stream = await file.OpenReadAsync();
		using var managed = stream.AsStreamForRead();
		using var pdf = PdfReader.Open(managed, PdfDocumentOpenMode.Import);

		var jobs = new List<DocumentOcrJobInput>();
		var pageLimit = settings.UseFreeTier ? Math.Max(1, settings.FreeTierPageLimit) : int.MaxValue;
		var truncated = false;

		for (var i = 0; i < pdf.PageCount; i++)
		{
			if (i >= pageLimit)
			{
				truncated = true;
				break;
			}

			using var pageDocument = new PdfDocument();
			pageDocument.Options.CompressContentStreams = true;
			pageDocument.AddPage(pdf.Pages[i]);

			using var memory = new MemoryStream();
			pageDocument.Save(memory, false);
			var buffer = memory.ToArray();
			var pageNumber = i + 1;
			var label = $"Page {pageNumber}";

			jobs.Add(new DocumentOcrJobInput(
				Guid.NewGuid(),
				_ => Task.FromResult<Stream>(new MemoryStream(buffer, writable: false)),
				contentType,
				pageNumber,
				label));
		}

		if (jobs.Count == 0)
		{
			throw new InvalidOperationException($"{file.Name} does not contain any pages to process.");
		}

		return new DocumentOcrBatch(file.Name, jobs, truncated);
	}
}
