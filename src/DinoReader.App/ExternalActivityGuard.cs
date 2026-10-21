namespace DinoReader;

/// <summary>
/// Set while the app itself hands the screen to another activity (the file picker now, the camera
/// later). The window stops then, but the grown-up pages must stay: the grown-up is mid-task, not
/// away. <see cref="DinoApp"/> skips its reset while <see cref="IsActive"/>.
/// </summary>
public sealed class ExternalActivityGuard
{
	private int _depth;

	public bool IsActive => _depth > 0;

	/// <summary>Raised when the last activity returns, so the app can check how long it was away.</summary>
	public event Action? Ended;

	public async Task<T> RunAsync<T>(Func<Task<T>> work)
	{
		_depth++;
		try
		{
			return await work();
		}
		finally
		{
			_depth--;
			if (_depth == 0)
			{
				Ended?.Invoke();
			}
		}
	}
}
