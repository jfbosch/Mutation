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
