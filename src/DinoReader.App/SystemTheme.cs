namespace DinoReader;

/// <summary>
/// The app pins UserAppTheme to Light, so MAUI never raises RequestedThemeChanged; the platform hosts
/// (Android's MainActivity, Windows' App) call <see cref="Raise"/> when the phone's own theme changes.
/// </summary>
internal static class SystemTheme
{
	/// <summary>Raised on the UI thread after the system theme changed.</summary>
	public static event Action? Changed;

	public static void Raise() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
}
