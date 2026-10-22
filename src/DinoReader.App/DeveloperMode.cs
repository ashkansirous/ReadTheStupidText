namespace DinoReader;

/// <summary>
/// The developer switch (Slice 91): on in a Debug build, and in any build once toggled by tapping the gallery title
/// <see cref="TapsToToggle"/> times in quick succession in grown-up mode. It reveals "Export story" on the grown-up tile overlay and nothing else;
/// it is never a kid or parent feature. The choice is remembered in preferences.
/// </summary>
public static class DeveloperMode
{
	public const int TapsToToggle = 7;
	private const string SwitchKey = "dino.developer";

	private static readonly TimeSpan TapWindow = TimeSpan.FromSeconds(2);

	private static readonly object TapLock = new();

	private static int _taps;
	private static long _lastTapTicks;

	public static bool Enabled => IsDebugBuild || Read();

	private static bool IsDebugBuild =>
#if DEBUG
		true;
#else
		false;
#endif

	/// <summary>Counts one tap on the gallery title; the <see cref="TapsToToggle"/>th flips the switch and returns true.</summary>
	public static bool RegisterTap()
	{
		lock (TapLock)
		{
			long now = Environment.TickCount64;
			if (now - _lastTapTicks > TapWindow.TotalMilliseconds)
			{
				_taps = 0;
			}

			_lastTapTicks = now;
			if (++_taps < TapsToToggle)
			{
				return false;
			}

			_taps = 0;
			Write(!Read());
			return true;
		}
	}

	/// <summary>Forgets the taps so far (the grown-up left grown-up mode).</summary>
	public static void ResetTaps()
	{
		lock (TapLock)
		{
			_taps = 0;
		}
	}

	private static bool Read() => Preferences.Default.Get(SwitchKey, false);

	private static void Write(bool on) => Preferences.Default.Set(SwitchKey, on);
}
