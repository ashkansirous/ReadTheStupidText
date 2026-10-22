namespace ReadTheStupidText.Mobile;

public partial class AppShell : Shell
{
	public const string VoicePickerRoute = "voicepicker";

	public AppShell()
	{
		InitializeComponent();
		FlowDirection = FlowDirection.LeftToRight;
		Routing.RegisterRoute(VoicePickerRoute, typeof(VoicePickerPage));
	}
}
