namespace ReadTheStupidText.Mobile;

public partial class AppShell : Shell
{
	public const string VoicePickerRoute = "voicepicker";
	public const string SettingsRoute = "settings";
	public const string CaptureRoute = "capture";
	public const string ReviewRoute = "review";
	public const string BackRoute = "..";

	public AppShell()
	{
		InitializeComponent();
		FlowDirection = FlowDirection.LeftToRight;
		Routing.RegisterRoute(VoicePickerRoute, typeof(VoicePickerPage));
		Routing.RegisterRoute(SettingsRoute, typeof(SettingsPage));
		Routing.RegisterRoute(CaptureRoute, typeof(CapturePage));
		Routing.RegisterRoute(ReviewRoute, typeof(ReviewPage));
	}
}
