namespace DinoReader.WinUI;

/// <summary>The WinUI 3 application for Dino Reader. No tray and no hotkey: it is a normal window app.</summary>
public partial class App : MauiWinUIApplication
{
	// Held in a field: a collected UISettings stops raising ColorValuesChanged.
	private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

	public App()
	{
		this.InitializeComponent();
		_uiSettings.ColorValuesChanged += (_, _) => SystemTheme.Raise();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
