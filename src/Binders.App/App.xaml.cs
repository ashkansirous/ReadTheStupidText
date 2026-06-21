using Binders.Application.Input;
using Binders.Application.Reading;
using Binders.Infrastructure.Input;
using Binders.Infrastructure.Reading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Binders_App;

/// <summary>
/// Application entry point. Keeps to provider/DI wiring and window bootstrap;
/// all read-aloud behaviour lives in the Application/Infrastructure layers.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    /// <summary>The composition root for the running app.</summary>
    public IServiceProvider Services { get; }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechReader, SpeechReader>();
        services.AddSingleton<IClipboardReader, ClipboardReader>();
        services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<ReadAloudService>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates the tray-hosting window. It is never activated — Binders lives in
    /// the notification area, not as a visible window.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow(
            Services.GetRequiredService<ReadAloudService>(),
            Services.GetRequiredService<IHotkeyService>());
    }
}
