using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mutation.Ui.Services.DocumentOcr;

namespace Mutation.Tests;

public class ApiRateLimiterTests
{
	[Fact]
	public async Task AcquireAsync_DelaysWhenLimitExceeded()
	{
		var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
		var delays = new List<TimeSpan>();
		var limiter = new ApiRateLimiter(20, TimeSpan.FromMinutes(1), timeProvider, (span, token) =>
		{
			delays.Add(span);
			timeProvider.Advance(span);
			return Task.CompletedTask;
		});

		for (int i = 0; i < 20; i++)
		{
			await limiter.AcquireAsync(CancellationToken.None);
		}

		await limiter.AcquireAsync(CancellationToken.None);

		Assert.Single(delays);
		Assert.Equal(TimeSpan.FromMinutes(1), delays[0]);

		await limiter.AcquireAsync(CancellationToken.None);
		Assert.Single(delays);
	}

	private sealed class TestTimeProvider : TimeProvider
	{
		private DateTimeOffset _utcNow;

		public TestTimeProvider(DateTimeOffset start)
		{
			_utcNow = start;
		}

		public override DateTimeOffset GetUtcNow() => _utcNow;

		public void Advance(TimeSpan value) => _utcNow += value;

		public override long GetTimestamp() => 0;
		public override long GetTimestamp(DateTimeOffset utcDateTime) => 0;
		public override TimeSpan GetElapsedTime(long startingTimestamp) => TimeSpan.Zero;
		public override TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) => TimeSpan.Zero;

		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		=> new NoopTimer();

		private sealed class NoopTimer : ITimer
		{
			public void Dispose()
			{
			}

			public ValueTask DisposeAsync() => ValueTask.CompletedTask;

			public bool Change(TimeSpan dueTime, TimeSpan period) => true;
		}
	}
}