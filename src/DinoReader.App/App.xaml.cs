using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

// Named DinoApp (not App) so it never collides with the "App" in DinoReader.App.
public partial class DinoApp : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly AwayTracker _away = new();

	public DinoApp(IServiceProvider services, SpeechStartup startup)
	{
		InitializeComponent();
		// The kids' palette is light; night colours belong to ReadingTheme on the reading screen only.
		UserAppTheme = AppTheme.Light;
		if (UiCulture.IsRightToLeft)
		{
			DinoFonts.UseForRightToLeftUi(Resources);
		}

		_services = services;
		PageAppearing += (_, page) => SystemBarIcons.ForPage(page);
		_ = startup.RunAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var navigation = new NavigationPage(_services.GetRequiredService<GalleryPage>())
		{
			BarBackgroundColor = DinoTokens.Get(DinoTokens.Cream),
			BarTextColor = DinoTokens.Get(DinoTokens.Ink),
			FlowDirection = UiFlow.ForCurrentCulture(),
		};
		var window = new Window(navigation);
		var external = _services.GetRequiredService<ExternalActivityGuard>();
		window.Stopped += (_, _) => OnStopped(navigation, external);
		window.Resumed += (_, _) => OnResumed(navigation, external);
		external.Ended += () => OnResumed(navigation, external);
		return window;
	}

	// A grown-up area never stays open behind a locked phone or another app: leaving the app
	// returns to the gallery, so the gate is needed again. An activity the app started itself
	// (the file picker) stops the window too, so it is timed instead: a quick pick keeps the
	// grown-up pages, one that lasts past AwayTracker.DefaultThreshold (Home pressed, another
	// app) resets like any other absence. If the process is killed, the app restarts at the
	// gallery anyway: CreateWindow builds a fresh stack and nothing restores the old one.
	private void OnStopped(NavigationPage navigation, ExternalActivityGuard external)
	{
		if (external.IsActive)
		{
			_away.Stopped(DateTimeOffset.UtcNow);
			return;
		}

		_away.Clear();
		_ = ObserveAsync(CloseGrownUpPagesAsync(navigation));
	}

	private void OnResumed(NavigationPage navigation, ExternalActivityGuard external)
	{
		if (_away.ReturnedTooLate(DateTimeOffset.UtcNow))
		{
			_ = ObserveAsync(CloseGrownUpPagesAsync(navigation));
		}
	}

	private static async Task ObserveAsync(Task work)
	{
		try
		{
			await work;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader reset to gallery failed: {ex}");
		}
	}

	// Each step is attempted whatever the one before did: a modal that will not dismiss must
	// not leave the grown-up pages open behind it.
	private static async Task CloseGrownUpPagesAsync(NavigationPage navigation)
	{
		await TryAsync(() => DismissGatesAsync(navigation));
		await TryAsync(() => PopGrownUpPagesAsync(navigation));
	}

	private static async Task DismissGatesAsync(NavigationPage navigation)
	{
		foreach (GatePage gate in navigation.Navigation.ModalStack.OfType<GatePage>().ToList())
		{
			await TryAsync(gate.DismissAsync);
		}
	}

	private static async Task PopGrownUpPagesAsync(NavigationPage navigation)
	{
		if (navigation.Navigation.NavigationStack.Any(p => p is IGrownUpPage))
		{
			await navigation.PopToRootAsync(animated: false);
		}
	}

	private static async Task TryAsync(Func<Task> step)
	{
		try
		{
			await step();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader grown-up reset step failed: {ex}");
		}
	}
}
