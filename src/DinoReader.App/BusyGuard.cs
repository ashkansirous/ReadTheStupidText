namespace DinoReader;

/// <summary>Drops a second tap while the first is still running, so a double tap can't push a page
/// twice or save a story twice. One per page.</summary>
public sealed class BusyGuard
{
	private const int WaitMilliseconds = 100;

	private bool _busy;

	public async Task RunAsync(Func<Task> work)
	{
		if (_busy)
		{
			return;
		}

		_busy = true;
		try
		{
			await work();
		}
		finally
		{
			_busy = false;
		}
	}

	/// <summary>Runs <paramref name="work"/> once the running action is done, instead of dropping it: for a result that must be applied, such as an installed pack.</summary>
	public async Task RunWhenFreeAsync(Func<Task> work)
	{
		await WhenFreeAsync();
		await RunAsync(work);
	}

	/// <summary>Completes once no action is running; for a tap that must not be lost to an edit in flight but must not hold the guard itself (a voice sample). Throws <see cref="OperationCanceledException"/> when <paramref name="cancel"/> fires.</summary>
	public async Task WhenFreeAsync(CancellationToken cancel = default)
	{
		while (_busy)
		{
			await Task.Delay(WaitMilliseconds, cancel);
		}
	}
}
