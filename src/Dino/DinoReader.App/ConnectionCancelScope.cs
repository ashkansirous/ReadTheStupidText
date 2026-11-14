namespace DinoReader;

/// <summary>
/// Slice 377 item 16: a token that is cancelled the moment the connection becomes one <c>shouldStop</c> says stops the work (the one pattern the high-res upgrade pass and the first-install high-res fetch share).
/// A seam, no MAUI type: the caller hands over the check and the subscribe/unsubscribe pair of the connection-changed event (see <c>PictureSizeSource.WatchConnection</c>), so the unit tests drive it directly.
/// The check runs once right after subscribing, because a link that changed before the subscription raises nothing.
/// ConnectionChanged comes from a platform thread, so it can fire while the scope is being disposed: the cancel then finds nothing left to stop and is ignored.
/// </summary>
public sealed class ConnectionCancelScope : IDisposable
{
	private readonly Func<bool> _shouldStop;
	private readonly Action<EventHandler> _unsubscribe;
	private readonly CancellationTokenSource _source;
	private int _stoppedByConnection;

	public ConnectionCancelScope(Func<bool> shouldStop, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, CancellationToken outer)
	{
		_shouldStop = shouldStop;
		_unsubscribe = unsubscribe;
		_source = CancellationTokenSource.CreateLinkedTokenSource(outer);
		subscribe(OnChanged);
		StopIfNeeded();
	}

	/// <summary>Cancelled when the connection became one that stops the work, or when the outer token is.</summary>
	public CancellationToken Token => _source.Token;

	/// <summary>True only when the connection's own cancel took effect (not when the outer token had already cancelled, nor after the scope ended).</summary>
	public bool StoppedByConnection => Volatile.Read(ref _stoppedByConnection) == 1;

	private void OnChanged(object? sender, EventArgs e) => StopIfNeeded();

	private void StopIfNeeded()
	{
		if (!_shouldStop())
		{
			return;
		}

		try
		{
			if (_source.IsCancellationRequested)
			{
				return; // already stopped (by the outer token or an earlier change): not this change's doing
			}

			Interlocked.Exchange(ref _stoppedByConnection, 1); // before the cancel, so a catch filter that runs on the cancel sees it
			_source.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// no-fallback: the scope already ended, nothing is left to cancel
			Interlocked.Exchange(ref _stoppedByConnection, 0);
		}
	}

	public void Dispose()
	{
		_unsubscribe(OnChanged);
		_source.Dispose();
	}
}
