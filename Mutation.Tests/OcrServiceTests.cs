using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CognitiveSupport;
using Mutation.Ui.Services;
using PdfSharp.Pdf;

namespace Mutation.Tests;

public class OcrServiceTests
{
	[Fact]
        public async Task RequestRateLimiter_EnforcesWindowBetweenRequests()
        {
                Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
                Assert.NotNull(limiterType);
                object? limiter = Activator.CreateInstance(limiterType!, 1, TimeSpan.FromMilliseconds(100));
		Assert.NotNull(limiter);
		MethodInfo? waitAsync = limiterType!.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		Task first = (Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!;
		await first.ConfigureAwait(false);

		var stopwatch = Stopwatch.StartNew();
		Task second = (Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!;
		await second.ConfigureAwait(false);
		stopwatch.Stop();

                Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(85));
        }

	[Fact]
	public async Task RequestRateLimiter_RespectsLimitAcrossMultipleCalls()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		object? limiter = Activator.CreateInstance(limiterType!, 2, TimeSpan.FromMilliseconds(120));
		Assert.NotNull(limiter);
		MethodInfo? waitAsync = limiterType!.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		await ((Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		await ((Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);

		var stopwatch = Stopwatch.StartNew();
		await ((Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		stopwatch.Stop();

		Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public async Task RequestRateLimiter_HonorsCancellation()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		object? limiter = Activator.CreateInstance(limiterType!, 1, TimeSpan.FromMilliseconds(250));
		Assert.NotNull(limiter);
		MethodInfo? waitAsync = limiterType!.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		await ((Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);

		using var cts = new CancellationTokenSource();
		cts.CancelAfter(TimeSpan.FromMilliseconds(50));

		await Assert.ThrowsAsync<TaskCanceledException>(async () =>
		{
			await ((Task)waitAsync.Invoke(limiter, new object[] { cts.Token })!).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestRateLimiter_AllowsRequestsAfterWindowExpires()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		object? limiter = Activator.CreateInstance(limiterType!, 2, TimeSpan.FromMilliseconds(80));
		Assert.NotNull(limiter);
		MethodInfo? waitAsync = limiterType!.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		await ((Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		await ((Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);

		await Task.Delay(TimeSpan.FromMilliseconds(120)).ConfigureAwait(false);

		var stopwatch = Stopwatch.StartNew();
		await ((Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		stopwatch.Stop();

		Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(40));
	}

	[Fact]
	public async Task SharedRateLimiter_ResetClearsWindowState()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		FieldInfo? sharedField = typeof(OcrService).GetField("SharedRateLimiter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(sharedField);
		object? limiter = sharedField!.GetValue(null);
		Assert.NotNull(limiter);
		MethodInfo? reset = limiterType!.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(reset);
		reset!.Invoke(limiter, Array.Empty<object>());
		MethodInfo? waitAsync = limiterType.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		await ((Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		await ((Task)waitAsync.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);

		OcrService.OcrRequestWindowState populated = OcrService.GetSharedRequestWindowState();
		Assert.Equal(2, populated.RequestsInWindow);
		Assert.Equal(2, populated.TotalRequestsGranted);
		Assert.True(populated.LastRequestUtc.HasValue);

		reset.Invoke(limiter, Array.Empty<object>());

		OcrService.OcrRequestWindowState cleared = OcrService.GetSharedRequestWindowState();
		Assert.Equal(0, cleared.RequestsInWindow);
		Assert.Equal(0, cleared.TotalRequestsGranted);
		Assert.False(cleared.LastRequestUtc.HasValue);
	}

	[Fact]
	public async Task SharedRateLimiter_TracksUsageAcrossOperations()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		FieldInfo? sharedField = typeof(OcrService).GetField("SharedRateLimiter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(sharedField);
		object? limiter = sharedField!.GetValue(null);
		Assert.NotNull(limiter);
		MethodInfo? reset = limiterType!.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(reset);
		reset!.Invoke(limiter, Array.Empty<object>());
		MethodInfo? waitAsync = limiterType.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
		Assert.NotNull(waitAsync);

		for (int i = 0; i < 3; i++)
		{
			await ((Task)waitAsync!.Invoke(limiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
		}

		OcrService.OcrRequestWindowState state = OcrService.GetSharedRequestWindowState();
		Assert.Equal(20, state.Limit);
		Assert.Equal(TimeSpan.FromMinutes(1), state.WindowLength);
		Assert.Equal(3, state.TotalRequestsGranted);
		Assert.Equal(3, state.RequestsInWindow);
		Assert.True(state.TimeUntilWindowReset >= TimeSpan.Zero);
		Assert.True(state.LastRequestUtc.HasValue);
	}


	[Fact]
	public async Task SharedRateLimiter_PreventsExceedingLimitPerWindowAcrossSequentialRuns()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		FieldInfo? sharedField = typeof(OcrService).GetField("SharedRateLimiter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(sharedField);
		object? originalLimiter = sharedField!.GetValue(null);
		Assert.NotNull(originalLimiter);

		MethodInfo? reset = limiterType!.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(reset);
		reset!.Invoke(originalLimiter, Array.Empty<object>());

		object? testLimiter = Activator.CreateInstance(limiterType!, 3, TimeSpan.FromMilliseconds(80));
		Assert.NotNull(testLimiter);
		sharedField.SetValue(null, testLimiter);

		try
		{
			MethodInfo? waitAsync = limiterType.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
			Assert.NotNull(waitAsync);

			var stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < 6; i++)
			{
				await ((Task)waitAsync!.Invoke(testLimiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
				OcrService.OcrRequestWindowState snapshot = OcrService.GetSharedRequestWindowState();
				Assert.InRange(snapshot.RequestsInWindow, 1, 3);
				Assert.True(snapshot.TotalRequestsGranted >= i + 1);
			}
			stopwatch.Stop();

			Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(80));
		}
		finally
		{
			sharedField.SetValue(null, originalLimiter);
			reset.Invoke(originalLimiter, Array.Empty<object>());
		}
	}

	[Fact]
	public async Task SharedRateLimiter_AllowsFreshBatchAfterWindowAcrossSequentialRuns()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		FieldInfo? sharedField = typeof(OcrService).GetField("SharedRateLimiter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(sharedField);
		object? originalLimiter = sharedField!.GetValue(null);
		Assert.NotNull(originalLimiter);

		MethodInfo? reset = limiterType!.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(reset);
		reset!.Invoke(originalLimiter, Array.Empty<object>());

		object? testLimiter = Activator.CreateInstance(limiterType!, 3, TimeSpan.FromMilliseconds(90));
		Assert.NotNull(testLimiter);
		sharedField.SetValue(null, testLimiter);

		try
		{
			MethodInfo? waitAsync = limiterType.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
			Assert.NotNull(waitAsync);

			for (int i = 0; i < 3; i++)
			{
				await ((Task)waitAsync!.Invoke(testLimiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
			}

			OcrService.OcrRequestWindowState saturated = OcrService.GetSharedRequestWindowState();
			Assert.Equal(3, saturated.RequestsInWindow);

			await Task.Delay(TimeSpan.FromMilliseconds(120)).ConfigureAwait(false);

			OcrService.OcrRequestWindowState afterWait = OcrService.GetSharedRequestWindowState();
			Assert.Equal(0, afterWait.RequestsInWindow);

			var stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < 3; i++)
			{
				await ((Task)waitAsync!.Invoke(testLimiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
			}
			stopwatch.Stop();

			Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(90));
		}
		finally
		{
			sharedField.SetValue(null, originalLimiter);
			reset.Invoke(originalLimiter, Array.Empty<object>());
		}
	}

	[Fact]
	public async Task SharedRateLimiter_ScalesTwentyPerMinuteThrottleAcrossRuns()
	{
		Type? limiterType = typeof(OcrService).GetNestedType("RequestRateLimiter", BindingFlags.NonPublic);
		Assert.NotNull(limiterType);
		FieldInfo? sharedField = typeof(OcrService).GetField("SharedRateLimiter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(sharedField);
		object? originalLimiter = sharedField!.GetValue(null);
		Assert.NotNull(originalLimiter);

		MethodInfo? reset = limiterType!.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(reset);
		reset!.Invoke(originalLimiter, Array.Empty<object>());

		// Use a scaled window mirroring Azure's 20 requests per minute limit (4 per 120 ms in tests)
		object? testLimiter = Activator.CreateInstance(limiterType!, 4, TimeSpan.FromMilliseconds(120));
		Assert.NotNull(testLimiter);
		sharedField.SetValue(null, testLimiter);

		try
		{
			MethodInfo? waitAsync = limiterType.GetMethod("WaitAsync", BindingFlags.Public | BindingFlags.Instance);
			Assert.NotNull(waitAsync);

			var stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < 10; i++)
			{
				await ((Task)waitAsync!.Invoke(testLimiter, new object[] { CancellationToken.None })!).ConfigureAwait(false);
				OcrService.OcrRequestWindowState snapshot = OcrService.GetSharedRequestWindowState();
				Assert.InRange(snapshot.RequestsInWindow, 1, 4);
				Assert.True(snapshot.TotalRequestsGranted >= i + 1);
			}
			stopwatch.Stop();

			Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(200));
		}
		finally
		{
			sharedField.SetValue(null, originalLimiter);
			reset.Invoke(originalLimiter, Array.Empty<object>());
		}
	}

	[Fact]
	public void TryParseRetryAfter_ParsesHttpDateHeader()
	{
		MethodInfo? method = typeof(OcrService).GetMethod("TryParseRetryAfter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);
		var message = new HttpResponseMessage();
		var date = DateTimeOffset.UtcNow.AddSeconds(5);
		message.Headers.RetryAfter = new RetryConditionHeaderValue(date);

		TimeSpan? delay = (TimeSpan?)method!.Invoke(null, new object?[] { message.Headers });

		Assert.True(delay.HasValue);
		Assert.InRange(delay!.Value.TotalSeconds, 0.5, 10);
	}

	[Fact]
	public void TryParseRetryAfter_ParsesStringSecondsValue()
	{
		MethodInfo? method = typeof(OcrService).GetMethod("TryParseRetryAfter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);
		var message = new HttpResponseMessage();
		message.Headers.TryAddWithoutValidation("Retry-After", "7");

		TimeSpan? delay = (TimeSpan?)method!.Invoke(null, new object?[] { message.Headers });

		Assert.True(delay.HasValue);
		Assert.InRange(delay!.Value.TotalSeconds, 6.5, 7.5);
	}

	[Fact]
	public void TryParseRetryAfter_PrefersDeltaHeader()
	{
		MethodInfo? method = typeof(OcrService).GetMethod("TryParseRetryAfter", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);
		var message = new HttpResponseMessage();
		message.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

		TimeSpan? delay = (TimeSpan?)method!.Invoke(null, new object?[] { message.Headers });

		Assert.True(delay.HasValue);
		Assert.InRange(delay!.Value.TotalSeconds, 2.5, 3.5);
	}

	[Fact]
	public void ExpandFile_CreatesPdfWorkItemPerPage()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
		try
		{
			using (var document = new PdfDocument())
			{
				document.Pages.Add();
				document.Pages.Add();
				document.Pages.Add();
				document.Save(path);
			}

			MethodInfo? expandFile = typeof(OcrManager).GetMethod("ExpandFile", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(expandFile);

			var raw = expandFile!.Invoke(null, new object?[] { path });
			Assert.NotNull(raw);
			var items = ((IEnumerable)raw!).Cast<object>().ToList();
			Assert.Equal(3, items.Count);

			Type itemType = items[0].GetType();
			PropertyInfo? pageNumber = itemType.GetProperty("PageNumber", BindingFlags.Public | BindingFlags.Instance);
			PropertyInfo? totalPages = itemType.GetProperty("TotalPages", BindingFlags.Public | BindingFlags.Instance);
			PropertyInfo? error = itemType.GetProperty("InitializationError", BindingFlags.Public | BindingFlags.Instance);
			Assert.NotNull(pageNumber);
			Assert.NotNull(totalPages);
			Assert.NotNull(error);

			for (int i = 0; i < items.Count; i++)
			{
				object item = items[i];
				Assert.Equal(i + 1, (int)pageNumber!.GetValue(item)!);
				Assert.Equal(3, (int)totalPages!.GetValue(item)!);
				Assert.Null(error!.GetValue(item));
			}
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}
}
