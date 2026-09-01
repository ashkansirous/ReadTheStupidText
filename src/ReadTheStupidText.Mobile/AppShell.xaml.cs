namespace ReadTheStupidText.Mobile;

public partial class AppShell : Shell
{
	public const string VoicePickerRoute = "voicepicker";

	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(VoicePickerRoute, typeof(VoicePickerPage));
	}
}
