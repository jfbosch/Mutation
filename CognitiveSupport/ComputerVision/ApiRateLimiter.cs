using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Provides a lightweight rate limiter for Azure Computer Vision operations. The limiter keeps
/// track of the number of calls made within a sliding time window and blocks when the configured
/// quota is exhausted. Callers should wrap API calls in a <c>using</c> statement to clearly
/// delineate the protected section.
/// </summary>
public sealed class ApiRateLimiter
{
	private readonly int _maxCalls;
	private readonly TimeSpan _window;
	private readonly Queue<DateTimeOffset> _timestamps = new();
	private readonly object _sync = new();
	private readonly Func<DateTimeOffset> _clock;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;

	public ApiRateLimiter(
		int maxCalls = 20,
		TimeSpan? window = null,
		Func<DateTimeOffset>? clock = null,
		Func<TimeSpan, CancellationToken, Task>? delay = null)
	{
		if (maxCalls <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxCalls));
		}

		_window = window ?? TimeSpan.FromSeconds(60);
		if (_window <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(window));
		}

		_maxCalls = maxCalls;
		_clock = clock ?? (() => DateTimeOffset.UtcNow);
		_delay = delay ?? ((wait, token) => Task.Delay(wait, token));
	}

	public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
	{
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TimeSpan wait;
			lock (_sync)
			{
				var now = _clock();
				CleanupExpired(now);

				if (_timestamps.Count < _maxCalls)
				{
					_timestamps.Enqueue(now);
					return new Releaser();
				}

				var oldest = _timestamps.Peek();
				wait = oldest + _window - now;
				if (wait < TimeSpan.Zero)
				{
					wait = TimeSpan.Zero;
				}
			}

			if (wait > TimeSpan.Zero)
			{
				await _delay(wait, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private void CleanupExpired(DateTimeOffset now)
	{
		while (_timestamps.Count > 0 && now - _timestamps.Peek() >= _window)
		{
			_timestamps.Dequeue();
		}
	}

	private sealed class Releaser : IDisposable
	{
		public void Dispose()
		{
			// Entries age out automatically; disposing is a semantic marker only.
		}
	}
}
