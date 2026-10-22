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
	}

	// Read from UISettings at call time, on the UI thread after SystemTheme.Raise has dispatched (MAUI's PlatformAppTheme lags).
	// Dark mode has a dark Background; under high contrast Background is the high-contrast window colour, so the same rule applies.
	private bool IsSystemDark()
	{
		Windows.UI.Color background = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
		return background.R + background.G + background.B < DarkBackgroundSum;
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
