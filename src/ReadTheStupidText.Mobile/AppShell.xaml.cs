namespace ReadTheStupidText.Mobile;

public partial class AppShell : Shell
{
	public const string VoicePickerRoute = "voicepicker";
	public const string ScanRoute = "scan";

	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(VoicePickerRoute, typeof(VoicePickerPage));
		Routing.RegisterRoute(ScanRoute, typeof(ScanPage));
	}
}
