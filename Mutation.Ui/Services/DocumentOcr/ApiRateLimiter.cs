using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mutation.Ui.Services.DocumentOcr;

/// <summary>
/// Simple rate limiter used to throttle calls to the Document Intelligence service.
/// </summary>
public sealed class ApiRateLimiter
{
	private readonly TimeProvider _timeProvider;
	private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
	private readonly Queue<DateTimeOffset> _invocations = new();
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly int _maxCalls;
	private readonly TimeSpan _window;

	public ApiRateLimiter(
		int maxCallsPerWindow,
		TimeSpan window,
		TimeProvider? timeProvider = null,
		Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
	{
		if (maxCallsPerWindow <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxCallsPerWindow));
		}
		if (window <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(window));
		}

		_maxCalls = maxCallsPerWindow;
		_window = window;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_delayAsync = delayAsync ?? ((delay, token) => Task.Delay(delay, token));
	}

	public async Task AcquireAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			TimeSpan? waitDuration = null;
			await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				TrimOldEntries();
				if (_invocations.Count < _maxCalls)
				{
					_invocations.Enqueue(_timeProvider.GetUtcNow());
					return;
				}
				DateTimeOffset oldest = _invocations.Peek();
				DateTimeOffset now = _timeProvider.GetUtcNow();
				TimeSpan elapsed = now - oldest;
				TimeSpan remaining = _window - elapsed;
				if (remaining < TimeSpan.Zero)
				{
					remaining = TimeSpan.Zero;
				}
				waitDuration = remaining;
			}
			finally
			{
				_gate.Release();
			}

			if (waitDuration.HasValue)
			{
				await _delayAsync(waitDuration.Value, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private void TrimOldEntries()
	{
		DateTimeOffset now = _timeProvider.GetUtcNow();
		while (_invocations.Count > 0 && now - _invocations.Peek() >= _window)
		{
			_invocations.Dequeue();
		}
	}
}