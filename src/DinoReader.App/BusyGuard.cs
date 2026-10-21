namespace DinoReader;

/// <summary>Drops a second tap while the first is still running, so a double tap can't push a page
/// twice or save a story twice. One per page.</summary>
public sealed class BusyGuard
{
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
}
