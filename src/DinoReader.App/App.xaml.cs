using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Settings;

namespace DinoReader;

// Named DinoApp (not App) so it never collides with the "App" in DinoReader.App.
public partial class DinoApp : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly AwayTracker _away = new();
	private CancellationTokenSource? _capTimer;
	private NavigationPage? _navigation;
	private bool _rtlFontsApplied;

	public DinoApp(IServiceProvider services, SpeechStartup startup)
	{
		InitializeComponent();
		// The kids' palette is light; night colours belong to ReadingTheme on the reading screen only.
		UserAppTheme = AppTheme.Light;
		ApplyRightToLeftFonts();
		_services = services;
		PageAppearing += (_, page) => SystemBarIcons.ForPage(page);
		_ = startup.RunAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// A new window (activity recreated, second desktop window) always starts in kids' mode.
		_services.GetRequiredService<GrownUpMode>().Leave();
		var navigation = _navigation = BuildNavigation(RootPage());
		var window = new Window(navigation);
		var external = _services.GetRequiredService<ExternalActivityGuard>();
		window.Stopped += (_, _) => OnStopped(_navigation!, external);
		window.Resumed += (_, _) => OnResumed(_navigation!, external);
		Action ended = () => OnResumed(_navigation!, external);
		external.Ended += ended;
		window.Destroying += (_, _) => external.Ended -= ended;
		return window;
	}

	private static NavigationPage BuildNavigation(Page root) => new(root)
	{
		BarBackgroundColor = DinoTokens.Get(DinoTokens.Cream),
		BarTextColor = DinoTokens.Get(DinoTokens.Ink),
		FlowDirection = UiFlow.Screen,
	};

	// The Farsi UI font is written into the shared styles once, before the first page that needs it is built.
	private void ApplyRightToLeftFonts()
	{
		if (_rtlFontsApplied || !UiCulture.IsRightToLeft)
		{
			return;
		}

		DinoFonts.UseForRightToLeftUi(Resources);
		_rtlFontsApplied = true;
	}

	/// <summary>
	/// Replaces the whole page stack with a fresh gallery in the language just picked (the first run's hand-over, or a change on the
	/// Language page): XAML text is read when a page is built, so only new pages speak the new language, and the flow direction
	/// belongs to the new root. The window's stop/resume handlers follow <see cref="_navigation"/>.
	/// </summary>
	public void RebuildRoot()
	{
		UiCulture.Apply(LanguageChoice.Preferred());
		ApplyRightToLeftFonts();
		if (Windows.FirstOrDefault() is not { } window)
		{
			return;
		}

		window.Page = _navigation = BuildNavigation(_services.GetRequiredService<GalleryPage>());
	}

	// A fresh install opens on the first-run language page (Slice 93); every later start opens on the kids' gallery.
	private Page RootPage() =>
		FirstRunState.IsNeeded(_services.GetRequiredService<ISettingsStore>())
			? _services.GetRequiredService<FirstRunPage>()
			: _services.GetRequiredService<GalleryPage>();

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
