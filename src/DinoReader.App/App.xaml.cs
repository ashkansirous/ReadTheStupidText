using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

// Named DinoApp (not App) so it never collides with the "App" in DinoReader.App.
public partial class DinoApp : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;

	public DinoApp(IServiceProvider services, SpeechStartup startup)
	{
		InitializeComponent();
		_services = services;
		_ = startup.RunAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_services.GetRequiredService<MainPage>());
	}
}
