using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mutation.Ui.Services.DocumentOcr;
using CognitiveSupport;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Mutation.Tests;

public class DocumentFilePreprocessorTests
{
	[Fact]
	public async Task PreprocessAsync_SplitsPdfRespectingFreeTierLimit()
	{
		var settings = new AzureDocumentIntelligenceSettings
		{
			UseFreeTier = true,
			FreeTierPageLimit = 2
		};
		var preprocessor = new DocumentFilePreprocessor(settings);
		byte[] pdfBytes = CreatePdfDocument(4);
		var descriptor = DocumentSourceDescriptor.FromBytes("sample.pdf", pdfBytes);

		DocumentOcrBatch batch = await preprocessor.PreprocessAsync(descriptor, CancellationToken.None);

		Assert.Equal(2, batch.TotalJobs);
		Assert.All(batch.Jobs, job => Assert.Equal(DocumentOcrContentType.Pdf, job.ContentType));
		Assert.Equal(new[] { 1, 2 }, batch.Jobs.Select(job => job.PageNumber).ToArray());
	}

	[Fact]
	public async Task PreprocessAsync_ProcessesAllPdfPagesWhenNotFreeTier()
	{
		var settings = new AzureDocumentIntelligenceSettings
		{
			UseFreeTier = false
		};
		var preprocessor = new DocumentFilePreprocessor(settings);
		byte[] pdfBytes = CreatePdfDocument(3);
		var descriptor = DocumentSourceDescriptor.FromBytes("contract.pdf", pdfBytes);

		DocumentOcrBatch batch = await preprocessor.PreprocessAsync(descriptor, CancellationToken.None);

		Assert.Equal(3, batch.TotalJobs);
		Assert.Equal(new[] { 1, 2, 3 }, batch.Jobs.Select(job => job.PageNumber).ToArray());
	}

	[Fact]
	public async Task PreprocessAsync_ThrowsForUnsupportedExtension()
	{
		var settings = new AzureDocumentIntelligenceSettings();
		var preprocessor = new DocumentFilePreprocessor(settings);
		var descriptor = DocumentSourceDescriptor.FromBytes("notes.txt", new byte[] { 1, 2, 3 });

		await Assert.ThrowsAsync<NotSupportedException>(() => preprocessor.PreprocessAsync(descriptor, CancellationToken.None));
	}

	[Fact]
	public async Task PreprocessAsync_RejectsDocumentsExceedingConfiguredSize()
	{
		var settings = new AzureDocumentIntelligenceSettings
		{
			MaxDocumentBytes = 2
		};
		var preprocessor = new DocumentFilePreprocessor(settings);
		byte[] bytes = new byte[] { 0, 1, 2 };
		var descriptor = DocumentSourceDescriptor.FromBytes("image.png", bytes);

		await Assert.ThrowsAsync<InvalidOperationException>(() => preprocessor.PreprocessAsync(descriptor, CancellationToken.None));
	}

	[Fact]
	public async Task PreprocessAsync_PreparesImageAsSingleJob()
	{
		var settings = new AzureDocumentIntelligenceSettings();
		var preprocessor = new DocumentFilePreprocessor(settings);
		byte[] imageBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
		var descriptor = DocumentSourceDescriptor.FromBytes("photo.png", imageBytes);

		DocumentOcrBatch batch = await preprocessor.PreprocessAsync(descriptor, CancellationToken.None);

		var job = Assert.Single(batch.Jobs);
		Assert.Equal(DocumentOcrContentType.Png, job.ContentType);
		Assert.Equal(1, job.PageNumber);
	}

	private static byte[] CreatePdfDocument(int pageCount)
	{
		using PdfDocument document = new();
		for (int i = 0; i < pageCount; i++)
		{
			PdfPage page = document.AddPage();
			using XGraphics gfx = XGraphics.FromPdfPage(page);
			gfx.DrawString($"Page {i + 1}", new XFont("Arial", 12), XBrushes.Black, new XPoint(20, 20));
		}

		using MemoryStream stream = new();
		document.Save(stream, false);
		return stream.ToArray();
	}
}