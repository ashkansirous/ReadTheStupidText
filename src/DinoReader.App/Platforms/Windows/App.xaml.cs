namespace DinoReader.WinUI;

/// <summary>The WinUI 3 application for Dino Reader. No tray and no hotkey: it is a normal window app.</summary>
public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
