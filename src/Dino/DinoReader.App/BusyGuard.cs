namespace DinoReader;

/// <summary>One action at a time on a page. <see cref="RunAsync"/> drops a second tap while the first is running (so a double tap can't push a page twice or save a story twice);
/// <see cref="RunWhenFreeAsync"/> queues instead, strictly one after the other, for work that must happen. One per page. Built on a <see cref="SemaphoreSlim"/>, so there is no polling and no gap between "free" and "taken".</summary>
public sealed class BusyGuard
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private TaskCompletionSource _released = NewReleased();

	/// <summary>True while an action is running.</summary>
	public bool IsBusy => _gate.CurrentCount == 0;

	/// <summary>Runs <paramref name="work"/> now, or drops it (returns at once) when an action is running.</summary>
	public async Task RunAsync(Func<Task> work)
	{
		if (!_gate.Wait(0))
		{
			return;
		}

		try
		{
			await work();
		}
		finally
		{
			ReleaseGate();
		}
	}

	/// <summary>Runs <paramref name="work"/> once the running action (and any queued before it) is done, instead of dropping it: for a result that must be applied, such as an installed pack or leaving a page.</summary>
	public async Task RunWhenFreeAsync(Func<Task> work)
	{
		await _gate.WaitAsync();
		try
		{
			await work();
		}
		finally
		{
			ReleaseGate();
		}
	}

	/// <summary>Completes once no action is running; for a tap that must not be lost to an edit in flight but must not hold the guard itself (a voice sample). Throws <see cref="OperationCanceledException"/> when <paramref name="cancel"/> fires.</summary>
	public async Task WhenFreeAsync(CancellationToken cancel = default)
	{
		while (true)
		{
			Task released = _released.Task; // taken before the check, so a release between the check and the wait still completes it
			if (!IsBusy)
			{
				return;
			}

			await released.WaitAsync(cancel);
		}
	}

	// Releases the gate, then wakes everyone waiting for "free" (WhenFreeAsync); the waiters never hold the gate, so they can never make a real tap drop.
	private void ReleaseGate()
	{
		_gate.Release();
		Interlocked.Exchange(ref _released, NewReleased()).TrySetResult();
	}

	private static TaskCompletionSource NewReleased() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
