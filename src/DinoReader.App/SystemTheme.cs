namespace DinoReader;

/// <summary>
/// The app pins UserAppTheme to Light, so MAUI never raises RequestedThemeChanged; the platform hosts
/// (Android's MainActivity, Windows' App) call <see cref="Raise"/> when the phone's own theme changes.
/// </summary>
internal static class SystemTheme
{
	/// <summary>Raised on the UI thread after the system theme changed.</summary>
	public static event Action? Changed;

	/// <summary>Windows hook: reads the system colour mode afresh; MAUI's PlatformAppTheme lags the UISettings event there.</summary>
	public static Func<bool>? ReadIsDark { get; private set; }

	/// <summary>Sets the platform reader; the latest registration wins, so a new App instance replaces a stale reader.</summary>
	public static void RegisterReader(Func<bool> reader) => ReadIsDark = reader;

	/// <summary>True when the system is in dark mode, read at call time.</summary>
	public static bool IsDark =>
		ReadIsDark?.Invoke() ?? Microsoft.Maui.Controls.Application.Current?.PlatformAppTheme == AppTheme.Dark;

	public static void Raise() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
}
