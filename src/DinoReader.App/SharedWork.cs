namespace DinoReader;

/// <summary>
/// Runs one piece of work for every caller that asks for the same key at once (Slice 110): the reading screen and the voice both
/// translate a story, and each sentence is translated once for the two. The work stops when the last caller still waiting for it
/// cancels, so a page that goes away takes its translations with it, but one caller leaving does not cancel it for the other.
/// </summary>
internal sealed class SharedWork<TKey, TResult> where TKey : notnull
{
	private sealed class Entry(CancellationTokenSource source)
	{
		public CancellationTokenSource Source { get; } = source;
		public Task<TResult> Task { get; set; } = null!;
		public int Waiters { get; set; }
	}

	private readonly Dictionary<TKey, Entry> _running = [];
	private readonly Dictionary<TKey, Task> _winding = [];
	private readonly object _lock = new();

	public async Task<TResult> RunAsync(TKey key, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
	{
		Entry entry = await JoinAsync(key, work, cancellationToken);
		try
		{
			return await entry.Task.WaitAsync(cancellationToken);
		}
		finally
		{
			Leave(key, entry);
		}
	}

	// Work that was cancelled when its last waiter left may still be winding down; a new caller waits for it to end before starting its own,
	// so two translations of one key never run at once (the engine may not be reentrant).
	private async Task<Entry> JoinAsync(TKey key, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
	{
		while (true)
		{
			if (TryJoin(key, work, out Entry? entry, out Task? winding))
			{
				return entry!;
			}

			try
			{
				await winding!.WaitAsync(cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception)
			{
				// How the cancelled work ended does not matter; only that it has.
			}
		}
	}

	private bool TryJoin(TKey key, Func<CancellationToken, Task<TResult>> work, out Entry? joined, out Task? winding)
	{
		lock (_lock)
		{
			joined = null;
			if (_winding.TryGetValue(key, out winding))
			{
				return false;
			}

			if (!_running.TryGetValue(key, out Entry? entry))
			{
				entry = new Entry(new CancellationTokenSource());
				CancellationToken token = entry.Source.Token;
				entry.Task = Task.Run(() => work(token));
				_ = entry.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
				_running[key] = entry;
			}

			entry.Waiters++;
			joined = entry;
			return true;
		}
	}

	private void EndWinding(TKey key, Task task)
	{
		lock (_lock)
		{
			if (_winding.TryGetValue(key, out Task? current) && current == task)
			{
				_winding.Remove(key);
			}
		}
	}

	private void Leave(TKey key, Entry entry)
	{
		lock (_lock)
		{
			entry.Waiters--;
			if (entry.Waiters > 0)
			{
				return;
			}

			if (_running.TryGetValue(key, out Entry? current) && current == entry)
			{
				_running.Remove(key);
			}

			if (!entry.Task.IsCompleted)
			{
				entry.Source.Cancel();
				_winding[key] = entry.Task;
				_ = entry.Task.ContinueWith(_ => EndWinding(key, entry.Task), TaskScheduler.Default);
			}

			// The work may still be winding down after a cancel; the source is released once it ends.
			_ = entry.Task.ContinueWith(_ => entry.Source.Dispose(), TaskScheduler.Default);
		}
	}
}
