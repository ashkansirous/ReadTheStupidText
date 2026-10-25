namespace DinoReader;

/// <summary>
/// Keeps the screen on while a book plays aloud (Slice 126). All state is read and written on the UI thread
/// only (every public call hops there), so no locks are needed. The page that wants the screen is the owner,
/// so a page leaving cannot undo the page that replaced it. <see cref="Refresh"/> applies the state again,
/// because iOS clears the idle timer in the background and Android drops the window flag when the window is
/// recreated.
/// </summary>
public static class ScreenAwake
{
	private static object? _owner;
	private static bool _suspended;

	/// <summary>True for a state in which the book is being read, so the screen should stay on.</summary>
	public static bool Wants(SessionState state) => state is SessionState.Playing or SessionState.Loading;

	/// <summary>The owner asks for the screen on or off; an off from a page that is not the owner is ignored.</summary>
	public static void Set(object owner, bool on) => RunOnMainThread(() =>
	{
		if (on)
		{
			_owner = owner;
		}
		else if (ReferenceEquals(_owner, owner))
		{
			_owner = null;
		}
		else
		{
			return;
		}

		Apply(cycle: false);
	});

	/// <summary>The window went to the background (true) or came back (false); while stopped the screen is released.</summary>
	public static void Suspend(bool suspended) => RunOnMainThread(() =>
	{
		_suspended = suspended;
		Apply(cycle: !suspended);
	});

	// Cycles off then on: the platform setter may skip a value it believes is unchanged, while the real flag was lost.
	private static void Apply(bool cycle)
	{
		bool on = _owner is not null && !_suspended;
		try
		{
			if (cycle && on)
			{
				DeviceDisplay.Current.KeepScreenOn = false;
			}

			DeviceDisplay.Current.KeepScreenOn = on;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceError($"KeepScreenOn failed: {ex.Message}");
		}
	}

	private static void RunOnMainThread(Action action)
	{
		if (MainThread.IsMainThread)
		{
			action();
			return;
		}

		MainThread.BeginInvokeOnMainThread(action);
	}
}
