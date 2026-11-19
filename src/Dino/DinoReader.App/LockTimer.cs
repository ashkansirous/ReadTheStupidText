using System.Diagnostics;

namespace DinoReader;

/// <summary>
/// The one-second tick of the shelf's lock (Slice 418, decision 13): it feeds the real time between ticks to <see cref="GrownUpMode.Tick"/>, so a slow tick never stretches the 30 seconds. The tick runs for the whole
/// life of the app (it does nothing while the shelf is locked), so a Settings sub-page or the Record intro pushed over the gallery still counts down. <see cref="DinoApp"/> owns and starts it once.
/// </summary>
internal sealed class LockTimer(GrownUpMode mode)
{
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

	private readonly Stopwatch _clock = new();
	private IDispatcherTimer? _timer;

	/// <summary>Starts the tick on <paramref name="dispatcher"/>; a second call does nothing.</summary>
	public void Start(IDispatcher dispatcher)
	{
		if (_timer is not null)
		{
			return;
		}

		_timer = dispatcher.CreateTimer();
		_timer.Interval = Interval;
		_timer.Tick += (_, _) => Tick();
		_clock.Restart();
		_timer.Start();
		mode.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(GrownUpMode.IsGrownUp))
			{
				_clock.Restart(); // the first tick of a new unlock counts from the unlock, not from the last tick
			}
		};
	}

	private void Tick()
	{
		TimeSpan elapsed = _clock.Elapsed;
		_clock.Restart();
		mode.Tick(elapsed);
	}
}

/// <summary>
/// The holds on the lock's clock (Slice 418, decision 16): the store's own buying sheet and a recording in progress stop the 30 seconds until they end. Each hold is a disposable; disposing twice releases once.
/// Shared code reaches the clock through here, so Android, iOS and Windows hold it the same way.
/// </summary>
internal static class LockHolds
{
	/// <summary>Holds the clock until the returned value is disposed.</summary>
	public static IDisposable Begin(string reason)
	{
		GrownUpMode? mode = IPlatformApplication.Current?.Services.GetService<GrownUpMode>();
		if (mode is null)
		{
			return NoHold.Instance;
		}

		int token = mode.Clock.Hold();
		LockLog($"hold {reason}");
		return new Hold(mode, reason, token);
	}

	/// <summary>Holds the clock while <paramref name="work"/> runs (an alert, a system picker, an external screen); the hold ends on every path, a throw included (Slice 418 decision 16).</summary>
	public static async Task<T> WhileAsync<T>(string reason, Func<Task<T>> work)
	{
		using IDisposable hold = Begin(reason);
		return await work();
	}

	/// <summary><see cref="WhileAsync{T}"/> for work that returns nothing.</summary>
	public static async Task WhileAsync(string reason, Func<Task> work)
	{
		using IDisposable hold = Begin(reason);
		await work();
	}

	private static void LockLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Lock] {line}"); // the lock flow's expect-log markers (RTST-TEST tag)
#endif
		Debug.WriteLine($"Dino Reader lock {line}");
	}

	private sealed class Hold(GrownUpMode mode, string reason, int token) : IDisposable
	{
		private int _released;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _released, 1) == 0)
			{
				mode.Clock.Release(token);
				mode.RaiseCountdown();
				LockLog($"release {reason}");
			}
		}
	}

	private sealed class NoHold : IDisposable
	{
		public static readonly NoHold Instance = new();

		public void Dispose()
		{
			// nothing was held: the app has no services yet (a unit-test host)
		}
	}
}
