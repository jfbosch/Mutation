﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using Mutation.Ui.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mutation.Tests;

public class OcrManagerTests
{
	private const OcrReadingOrder DefaultOrder = OcrReadingOrder.TopToBottomColumnAware;

	[Fact]
	public async Task ExtractTextFromFilesAsync_ThrowsArgumentNull_WhenPathsNull()
	{
		var manager = new TestableOcrManager(CreateValidSettings(), new StubOcrService(), new TestClipboard());

		await Assert.ThrowsAsync<ArgumentNullException>(() => manager.ExtractTextFromFilesAsync(null!, DefaultOrder, CancellationToken.None));
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_ReturnsFailure_WhenNoValidPaths()
	{
		var service = new StubOcrService();
		var clipboard = new TestClipboard();
		var manager = new TestableOcrManager(CreateValidSettings(), service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { string.Empty, "   	" }, DefaultOrder, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal(string.Empty, result.Text);
		Assert.Equal(0, result.TotalCount);
		Assert.Equal(0, result.SuccessCount);
		Assert.Empty(result.Failures);
		Assert.Equal(0, clipboard.SetTextCalls);
		Assert.Equal(0, service.CallCount);

		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Failure, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_ReturnsFailure_WhenOcrNotConfigured()
	{
		var settings = new Settings();
		var service = new StubOcrService();
		var clipboard = new TestClipboard();
		using var file = new TempFile(".png");
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { file.Path }, DefaultOrder, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(0, result.SuccessCount);
		Assert.Single(result.Failures);
		Assert.Contains("missing", result.Failures[0], StringComparison.OrdinalIgnoreCase);
		Assert.Equal(0, clipboard.SetTextCalls);
		Assert.Equal(0, service.CallCount);
		Assert.Single(manager.Beeps);
		Assert.Equal(BeepType.Failure, manager.Beeps.Single());
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_ProcessesSingleImageFile_Successfully()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService("recognized text");
		using var file = new TempFile(".png");
		var manager = new TestableOcrManager(settings, service, clipboard, () => true, action =>
		{
			action();
			return Task.CompletedTask;
		});

		var result = await manager.ExtractTextFromFilesAsync(new[] { file.Path }, DefaultOrder, CancellationToken.None);

		string expectedText = $"[{Path.GetFileName(file.Path)}]{Environment.NewLine}recognized text{Environment.NewLine}";
		Assert.True(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(1, result.SuccessCount);
		Assert.Equal(expectedText, result.Text);
		Assert.Equal(expectedText, clipboard.LastText);
		Assert.Equal(1, clipboard.SetTextCalls);
		Assert.Equal(0, manager.RunOnDispatcherCalls);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Success, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_DispatchesClipboardUpdate_WhenOffUiThread()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService("batched result");
		using var file = new TempFile(".png");
		var dispatched = false;
		var manager = new TestableOcrManager(settings, service, clipboard, () => false, action =>
		{
			dispatched = true;
			action();
			return Task.CompletedTask;
		});

		var result = await manager.ExtractTextFromFilesAsync(new[] { file.Path }, DefaultOrder, CancellationToken.None);

		string expectedText = $"[{Path.GetFileName(file.Path)}]{Environment.NewLine}batched result{Environment.NewLine}";
		Assert.True(result.Success);
		Assert.True(dispatched);
		Assert.Equal(1, manager.RunOnDispatcherCalls);
		Assert.Equal(expectedText, clipboard.LastText);
		Assert.Equal(1, clipboard.SetTextCalls);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Success, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_ProcessesMultipleFiles_WithMixedSuccess()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var failure = new InvalidOperationException("OCR failed");
		var service = new StubOcrService("first text", failure);
		using var first = new TempFile(".png");
		using var second = new TempFile(".jpg");
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { first.Path, second.Path }, DefaultOrder, CancellationToken.None);

		string expectedText = $"[{Path.GetFileName(first.Path)}]{Environment.NewLine}first text{Environment.NewLine}";
		Assert.False(result.Success);
		Assert.Equal(2, result.TotalCount);
		Assert.Equal(1, result.SuccessCount);
		Assert.Equal(expectedText, result.Text);
		Assert.Single(result.Failures);
		Assert.Contains(Path.GetFileName(second.Path), result.Failures[0]);
		Assert.Contains("OCR failed", result.Failures[0]);
		Assert.Equal(expectedText, clipboard.LastText);
		Assert.Equal(1, clipboard.SetTextCalls);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Failure, manager.Beeps);
		Assert.Equal(2, service.CallCount);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_HandlesPdfFiles_WithMultiplePages()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService("page one\npage two");
		using var pdf = new TempPdf(2);
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { pdf.Path }, DefaultOrder, CancellationToken.None);

		string expectedText = $"[{Path.GetFileName(pdf.Path)}]{Environment.NewLine}(Pages 1-2){Environment.NewLine}page one{Environment.NewLine}page two{Environment.NewLine}";
		Assert.True(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(1, result.SuccessCount);
		Assert.Equal(expectedText, result.Text);
		Assert.Empty(result.Failures);
		Assert.Equal(expectedText, clipboard.LastText);
		Assert.Equal(1, clipboard.SetTextCalls);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Success, manager.Beeps);
		Assert.Equal(1, service.CallCount);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_HandlesPdfWithNoPages()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService();
		using var pdf = new TempPdf(0);
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { pdf.Path }, DefaultOrder, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(0, result.SuccessCount);
		Assert.Empty(result.Text);
		Assert.Single(result.Failures);
		Assert.Contains("PDF contains no pages", result.Failures[0], StringComparison.OrdinalIgnoreCase);
		Assert.Equal(0, clipboard.SetTextCalls);
		Assert.Equal(0, service.CallCount);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Failure, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_HandlesInvalidPdf()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService();
		using var invalidPdf = new TempFile(".pdf", "not a real pdf");
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { invalidPdf.Path }, DefaultOrder, CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(0, result.SuccessCount);
		Assert.Empty(result.Text);
		Assert.Single(result.Failures);
		Assert.Contains(Path.GetFileName(invalidPdf.Path), result.Failures[0]);
		Assert.Equal(0, clipboard.SetTextCalls);
		Assert.Equal(0, service.CallCount);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Failure, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_HandlesCancellation()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		using var first = new TempFile(".png");
		using var second = new TempFile(".png");
		var cts = new CancellationTokenSource();
		var service = new StubOcrService(new Func<Stream, CancellationToken, Task<string>>(async (_, token) =>
		{
			cts.Cancel();
			await Task.Yield();
			return "first";
		}));
		var manager = new TestableOcrManager(settings, service, clipboard);

		await Assert.ThrowsAsync<OperationCanceledException>(() => manager.ExtractTextFromFilesAsync(new[] { first.Path, second.Path }, DefaultOrder, cts.Token));

		Assert.Equal(0, clipboard.SetTextCalls);
		Assert.Equal(1, service.CallCount);
		Assert.Equal(0, manager.BeepCount);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_ReportsProgress_Correctly()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService("page one\npage two");
		using var pdf = new TempPdf(2);
		var progress = new List<OcrProcessingProgress>();
		var manager = new TestableOcrManager(settings, service, clipboard);

		var result = await manager.ExtractTextFromFilesAsync(new[] { pdf.Path }, DefaultOrder, CancellationToken.None, new Progress<OcrProcessingProgress>(progress.Add));

		Assert.True(result.Success);
		Assert.Equal(1, progress.Count);
		Assert.Equal(2, progress[0].ProcessedPages);
		Assert.Equal(2, progress[0].TotalPages);
		Assert.Equal(Path.GetFileName(pdf.Path), progress[0].FileName);
		Assert.Equal("1-2", progress[0].BatchPages);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Success, manager.Beeps);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_SkipsDuplicatePaths()
	{
		var settings = CreateValidSettings();
		var clipboard = new TestClipboard();
		var service = new StubOcrService("text");
		using var file = new TempFile(".png");
		var manager = new TestableOcrManager(settings, service, clipboard);

		var duplicatePaths = new[] { file.Path, file.Path.ToUpperInvariant() };
		var result = await manager.ExtractTextFromFilesAsync(duplicatePaths, DefaultOrder, CancellationToken.None);

		Assert.True(result.Success);
		Assert.Equal(1, result.TotalCount);
		Assert.Equal(1, result.SuccessCount);
		Assert.Equal(1, service.CallCount);
		WaitForBeep(manager, 1);
		Assert.Contains(BeepType.Success, manager.Beeps);
	}

	private static Settings CreateValidSettings() => new()
	{
		AzureComputerVisionSettings = new AzureComputerVisionSettings
		{
			ApiKey = "key",
			Endpoint = "https://example.com"
		}
	};

	private static void WaitForBeep(TestableOcrManager manager, int expectedCount)
	{
		var reached = SpinWait.SpinUntil(() => manager.BeepCount >= expectedCount, TimeSpan.FromSeconds(1));
		Assert.True(reached, $"Expected at least {expectedCount} beep(s).");
	}

	private sealed class TestableOcrManager : OcrManager
	{
		private readonly Func<bool> _hasThreadAccess;
		private readonly Func<Action, Task> _dispatcher;
		private readonly ConcurrentQueue<BeepType> _beeps = new();

		public TestableOcrManager(Settings settings, IOcrService ocrService, TestClipboard clipboard, Func<bool>? hasThreadAccess = null, Func<Action, Task>? dispatcher = null)
			: base(settings, ocrService, clipboard)
		{
			Clipboard = clipboard;
			_hasThreadAccess = hasThreadAccess ?? (() => true);
			_dispatcher = dispatcher ?? (action =>
			{
				action();
				return Task.CompletedTask;
			});
		}

		public TestClipboard Clipboard { get; }
		public int RunOnDispatcherCalls { get; private set; }
		public int BeepCount => _beeps.Count;
		public IReadOnlyCollection<BeepType> Beeps => _beeps.ToArray();

		protected override bool HasDispatcherThreadAccess() => _hasThreadAccess();

		protected override Task RunOnDispatcherAsync(Action action)
		{
			RunOnDispatcherCalls++;
			return _dispatcher(action);
		}

		protected override void PlayBeep(BeepType type)
		{
			_beeps.Enqueue(type);
		}
	}

	private sealed class TestClipboard : ClipboardManager
	{
		public string? LastText { get; private set; }
		public int SetTextCalls { get; private set; }

		public override void SetText(string text)
		{
			SetTextCalls++;
			LastText = text;
		}
	}

	private sealed class StubOcrService : IOcrService
	{
		private readonly Queue<Func<Stream, CancellationToken, Task<string>>> _behaviors;

		public StubOcrService(params object[] behaviors)
		{
			_behaviors = new Queue<Func<Stream, CancellationToken, Task<string>>>(behaviors.Select(ConvertBehavior));
		}

		public int CallCount { get; private set; }

		public Task<string> ExtractText(OcrReadingOrder ocrReadingOrder, Stream imageStream, CancellationToken overallCancellationToken)
		{
			CallCount++;
			if (_behaviors.Count == 0)
				throw new InvalidOperationException("No behavior configured for this OCR call.");

			var behavior = _behaviors.Dequeue();
			return behavior(imageStream, overallCancellationToken);
		}

		private static Func<Stream, CancellationToken, Task<string>> ConvertBehavior(object behavior) => behavior switch
		{
			Func<Stream, CancellationToken, Task<string>> typed => typed,
			Func<Stream, Task<string>> streamFunc => (stream, _) => streamFunc(stream),
			Func<Task<string>> taskFactory => (_, _) => taskFactory(),
			string text => (_, _) => Task.FromResult(text),
			Exception ex => (_, _) => Task.FromException<string>(ex),
			_ => throw new ArgumentException("Unsupported behavior type.", nameof(behavior))
		};
	}

	private sealed class TempFile : IDisposable
	{
		public string Path { get; }

		public TempFile(string extension, string? contents = null)
		{
			var basePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
			Path = System.IO.Path.ChangeExtension(basePath, extension);
			File.WriteAllText(Path, contents ?? "test");
		}

		public void Dispose()
		{
			if (File.Exists(Path))
				File.Delete(Path);
		}
	}

	private sealed class TempPdf : IDisposable
	{
		public string Path { get; }

		public TempPdf(int pageCount)
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".pdf");
			if (pageCount == 0)
			{
				string minimalPdf = "%PDF-1.4\n1 0 obj\n<<\n/Type /Catalog\n/Pages 2 0 R\n>>\nendobj\n2 0 obj\n<<\n/Type /Pages\n/Kids []\n/Count 0\n>>\nendobj\nxref\n0 3\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \ntrailer\n<<\n/Size 3\n/Root 1 0 R\n>>\nstartxref\n110\n%%EOF";
				File.WriteAllText(Path, minimalPdf);
			}
			else
			{
				using var document = new PdfDocument();
				for (var i = 0; i < pageCount; i++)
				{
					document.Pages.Add();
				}
				document.Save(Path);
			}
		}

		public void Dispose()
		{
			if (File.Exists(Path))
				File.Delete(Path);
		}
	}
}
