namespace DinoReader.WinUI;

/// <summary>The WinUI 3 application for Dino Reader. No tray and no hotkey: it is a normal window app.</summary>
public partial class App : MauiWinUIApplication
{
	private const int DarkBackgroundSum = 3 * 128;

	// Held in a field: a collected UISettings stops raising ColorValuesChanged.
	private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

	public App()
	{
		this.InitializeComponent();
		SystemTheme.RegisterReader(IsSystemDark);
		_uiSettings.ColorValuesChanged += (_, _) => SystemTheme.Raise();
#if DEBUG || DINO_TEST_BOOKS
		// Slice 264 diagnosis tooling, only in Debug and in a Release package built for the VM checks (-p:DinoTestBooks=true): never in a store build.
		this.UnhandledException += OnUnhandledException;
		this.DebugSettings.LayoutCycleTracingLevel = Microsoft.UI.Xaml.LayoutCycleTracingLevel.High;
		LayoutCycleTrace.Start();
#endif
	}

#if DEBUG || DINO_TEST_BOOKS
	private const int TraceStackFrames = 30;

	// Slice 264 diagnosis: a native crash in Microsoft.UI.Xaml.dll (0xc000027b) left no managed trace. Writes what the framework reports to window-trace.log, never marks it handled.
	private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		try
		{
			Exception? ex = e.Exception;
			WindowTrace.Write($"Unhandled: {ex?.GetType().FullName ?? "(no exception)"}: {e.Message}");
			for (Exception? current = ex; current is not null; current = current.InnerException)
			{
				string label = ReferenceEquals(current, ex) ? "stack" : $"inner {current.GetType().FullName}: {current.Message}";
				WindowTrace.Write($"Unhandled {label}: {FirstFrames(current.StackTrace)}");
			}
		}
		catch (Exception)
		{
			// no-fallback: the failure trace is best effort and never replaces the original failure
			// the trace must never replace the original failure
		}
	}

	private static string FirstFrames(string? stackTrace) =>
		string.Join(" | ", (stackTrace ?? "(none)").Split('\n').Take(TraceStackFrames).Select(frame => frame.Trim()));
#endif

	// Read from UISettings at call time, on the UI thread after SystemTheme.Raise has dispatched (MAUI's PlatformAppTheme lags).
	// Dark mode has a dark Background; under high contrast Background is the high-contrast window colour, so the same rule applies.
	private bool IsSystemDark()
	{
		Windows.UI.Color background = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
		return background.R + background.G + background.B < DarkBackgroundSum;
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
