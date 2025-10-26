using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using Mutation.Ui.Services;

namespace Mutation.Tests;

public class OcrManagerTests
{
	[Fact]
	public async Task ExtractTextFromFilesAsync_CopiesResult_WhenUiThread()
	{
		var settings = new Settings
		{
			AzureComputerVisionSettings = new AzureComputerVisionSettings
			{
				ApiKey = "dummy",
				Endpoint = "https://dummy.com"
			}
		};
		var clipboard = new TestClipboardManager();
		var ocrService = new FakeOcrService("recognized text");
		using var temp = new TempFile();
		File.WriteAllText(temp.Path, "sample");
		var manager = new TestableOcrManager(settings, ocrService, clipboard, () => true, _ => Task.CompletedTask);
		var result = await manager.ExtractTextFromFilesAsync(new[] { temp.Path }, OcrReadingOrder.TopToBottomColumnAware, CancellationToken.None);
		var expected = $"[{Path.GetFileName(temp.Path)}]{Environment.NewLine}recognized text{Environment.NewLine}";
		Assert.True(result.Success);
		Assert.Equal(expected, result.Text);
		Assert.Equal(expected, clipboard.LastText);
		Assert.Equal(1, clipboard.CallCount);
		Assert.Equal(0, manager.RunOnDispatcherCalls);
	}

	[Fact]
	public async Task ExtractTextFromFilesAsync_DispatchesClipboardUpdate_WhenOffUiThread()
	{
		var settings = new Settings
		{
			AzureComputerVisionSettings = new AzureComputerVisionSettings
			{
				ApiKey = "dummy",
				Endpoint = "https://dummy.com"
			}
		};
		var clipboard = new TestClipboardManager();
		var ocrService = new FakeOcrService("batched result");
		using var temp = new TempFile();
		File.WriteAllText(temp.Path, "sample");
		var dispatched = false;
		var manager = new TestableOcrManager(settings, ocrService, clipboard, () => false, action =>
		{
			dispatched = true;
			action();
			return Task.CompletedTask;
		});
		var result = await manager.ExtractTextFromFilesAsync(new[] { temp.Path }, OcrReadingOrder.TopToBottomColumnAware, CancellationToken.None);
		var expected = $"[{Path.GetFileName(temp.Path)}]{Environment.NewLine}batched result{Environment.NewLine}";
		Assert.True(result.Success);
		Assert.True(dispatched);
		Assert.Equal(expected, clipboard.LastText);
		Assert.Equal(1, clipboard.CallCount);
		Assert.Equal(1, manager.RunOnDispatcherCalls);
	}

	private sealed class TestableOcrManager : OcrManager
	{
		private readonly Func<bool> _hasThreadAccess;
		private readonly Func<Action, Task> _dispatcher;

		public int RunOnDispatcherCalls { get; private set; }

		public TestableOcrManager(Settings settings, IOcrService ocrService, ClipboardManager clipboard, Func<bool> hasThreadAccess, Func<Action, Task> dispatcher)
			: base(settings, ocrService, clipboard)
		{
			_hasThreadAccess = hasThreadAccess;
			_dispatcher = dispatcher;
		}

		protected override bool HasDispatcherThreadAccess() => _hasThreadAccess();

		protected override Task RunOnDispatcherAsync(Action action)
		{
			RunOnDispatcherCalls++;
			return _dispatcher(action);
		}

		protected override void PlayBeep(BeepType type)
		{
			// Suppress audio during tests
		}
	}

	private sealed class TestClipboardManager : ClipboardManager
	{
		public string? LastText { get; private set; }
		public int CallCount { get; private set; }

		public override void SetText(string text)
		{
			CallCount++;
			LastText = text;
		}
	}

	private sealed class FakeOcrService : IOcrService
	{
		private readonly string _text;

		public FakeOcrService(string text)
		{
			_text = text;
		}

		public Task<string> ExtractText(OcrReadingOrder ocrReadingOrder, Stream imageStream, CancellationToken overallCancellationToken)
		{
			return Task.FromResult(_text);
		}
	}

	private sealed class TempFile : IDisposable
	{
		public string Path { get; }

		public TempFile()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
		}

		public void Dispose()
		{
			if (File.Exists(Path))
			{
				File.Delete(Path);
			}
		}
	}
}
