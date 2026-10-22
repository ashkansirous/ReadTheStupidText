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
	private CancellationTokenSource? _capTimer;

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
		// A new window (activity recreated, second desktop window) always starts in kids' mode.
		_services.GetRequiredService<GrownUpMode>().Leave();
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
		Action ended = () => OnResumed(navigation, external);
		external.Ended += ended;
		window.Destroying += (_, _) => external.Ended -= ended;
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
			ScheduleCapReset(navigation);
			return;
		}

		CancelCapReset();
		_away.Clear();
		_ = ObserveAsync(CloseGrownUpPagesAsync(navigation));
	}

	// The picker deferral is bounded: if the window is still away after the cap, reset anyway.
	private void ScheduleCapReset(NavigationPage navigation)
	{
		CancelCapReset();
		var timer = _capTimer = new CancellationTokenSource();
		_ = ObserveAsync(ResetAfterCapAsync(navigation, timer.Token));
	}

	private void CancelCapReset()
	{
		_capTimer?.Cancel();
		_capTimer = null;
	}

	private async Task ResetAfterCapAsync(NavigationPage navigation, CancellationToken token)
	{
		try
		{
			await Task.Delay(AwayTracker.ExternalActivityCap, token);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		_away.Clear();
		await MainThread.InvokeOnMainThreadAsync(() => CloseGrownUpPagesAsync(navigation));
	}

	private void OnResumed(NavigationPage navigation, ExternalActivityGuard external)
	{
		CancelCapReset();
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
	private async Task CloseGrownUpPagesAsync(NavigationPage navigation)
	{
		// First and synchronous: kids' mode must hold whatever the navigation steps below do.
		_services.GetRequiredService<GrownUpMode>().Leave();
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
