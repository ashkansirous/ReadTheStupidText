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
	private readonly AppConfigClient _config;
	private CancellationTokenSource? _capTimer;
	private NavigationPage? _navigation;
	private bool _rtlFontsApplied;
	private bool _launchCounted;
	private readonly CancellationTokenSource _bookUpdateCancel = new();
	private bool _bookUpdateStarted;

	public DinoApp(IServiceProvider services, SpeechStartup startup, AppConfigClient config)
	{
		InitializeComponent();
		// The kids' palette is light; night colours belong to ReadingTheme on the reading screen only.
		UserAppTheme = AppTheme.Light;
		ApplyRightToLeftFonts();
		_services = services;
		_config = config;
		// Contract: AppConfigClient raises Changed only on the main thread, so this handler may touch UI directly.
		_config.Changed += (_, _) => ShowUpdateIfRequired();
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
		window.Stopped += (_, _) => FlushUsage();
		window.Stopped += (_, _) => _bookUpdateCancel.Cancel();
		window.Resumed += (_, _) => OnResumed(_navigation!, external);
		window.Resumed += (_, _) => _ = _config.RefreshAsync();
		var resume = _services.GetRequiredService<ReadTheStupidText.Mobile.Shared.Network.PackResumeWatcher>(); // Slice 182: created and started with the window, not when a page first asks for it
		resume.Start();
		window.Resumed += (_, _) => resume.NotifyForeground();
		Action ended = () => OnResumed(_navigation!, external);
		external.Ended += ended;
		window.Destroying += (_, _) => external.Ended -= ended;
		// Resumed does not fire on a cold start, so the launch fetch is made here; the client joins an overlapping call.
		_ = CountLaunchAsync();
		return window;
	}

	// The launch count waits for the first config fetch, which says whether counting is on at all (Slice 134); the fetch is joined, not repeated.
	// Then the queue is sent, so a session with no reading still reports (the start-up flush ran before this count existed).
	private async Task CountLaunchAsync()
	{
		try
		{
			await _config.RefreshAsync();
			_ = CheckBookUpdatesAsync();
			if (_launchCounted)
			{
				return;
			}

			_launchCounted = true;
			var usage = _services.GetRequiredService<UsageCounter>();
			usage.RecordGeneral(UsageEventName.AppOpened, LanguageChoice.Current());
			await usage.FlushAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not count the launch: {ex.Message}");
		}
	}

	// Slice 181: on a cold start, after the config fetch, the store's newer versions of the shelf's catalogue books are downloaded and staged, at most once every 7 days (BookUpdater keeps the time); the next cold start applies them (GalleryPage, StagedBookUpdates).
	// Once per process, never on resume. The token is cancelled when the window stops (the app goes to the background), even before the check starts, so a late start does not run;
	// it is never disposed (it has no timer), so a stop can never race a dispose. A forced-update screen skips the check.
	private async Task CheckBookUpdatesAsync()
	{
		if (_config.UpdateRequired || _bookUpdateStarted || _bookUpdateCancel.IsCancellationRequested)
		{
			return;
		}

		_bookUpdateStarted = true;
		try
		{
			var updater = new BookUpdater(
				_services.GetRequiredService<StoreClient>(), _services.GetRequiredService<StarterStories>(), _services.GetRequiredService<StagedBookUpdates>());
			CancellationToken token = _bookUpdateCancel.Token;
			await Task.Run(() => updater.RunIfDueAsync(token));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not check for book updates: {ex.Message}");
		}
	}

	// Counts made in a session with no reading are sent when the window stops, as the app goes to the background.
	private void FlushUsage()
	{
		try
		{
			_ = _services.GetRequiredService<UsageCounter>().FlushAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not send its counts: {ex.Message}");
		}
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

		// A forced update raised while a pack downloaded must not be undone by the hand-over to the gallery.
		Page root = _config.UpdateRequired ? _services.GetRequiredService<UpdatePage>() : _services.GetRequiredService<GalleryPage>();
		window.Page = _navigation = BuildNavigation(root);
	}

#if DEBUG
	/// <summary>
	/// Debug test hook (Slice 162): finishes first run for <paramref name="code"/> like the first-run page (screen language, voice language, no
	/// intro, a fresh gallery root); false when there is no window. Differences, on purpose: it stays in kids' mode with no hand-over
	/// (flows tap the shelf's tiles), and it does not start the voice pack (SpokenLanguage.Use crashes the Debug x86_64 emulator build, see the slice file).
	/// </summary>
	public bool CompleteFirstRun(string code)
	{
		// Acts only while the first-run page is showing: before that (no window, page not built yet) the caller retries, after it main is up and there is nothing to do.
		if (Windows.FirstOrDefault()?.Page is not NavigationPage { RootPage: FirstRunPage })
		{
			return false;
		}

		_services.GetRequiredService<ISettingsStore>().Language = LanguageChoice.ApplyScreens(code);
		IntroState.Clear();
		RebuildRoot();
		return true;
	}
#endif

	// A build below the config's minVersion opens on the "Please update" page and nothing else (Slice 132); a fresh install opens
	// on the first-run language page (Slice 93); every later start opens on the kids' gallery.
	private Page RootPage()
	{
		if (_config.UpdateRequired)
		{
			return _services.GetRequiredService<UpdatePage>();
		}

		return FirstRunState.IsNeeded(_services.GetRequiredService<ISettingsStore>())
			? _services.GetRequiredService<FirstRunPage>()
			: _services.GetRequiredService<GalleryPage>();
	}

	// A fetched config can raise minVersion while the app is open: the update page then replaces everything, grown-up mode included.
	// Lowering it again never takes the page away; the next launch opens normally.
	private void ShowUpdateIfRequired()
	{
		if (!_config.UpdateRequired || Windows.FirstOrDefault() is not { } window || window.Page is NavigationPage { RootPage: UpdatePage })
		{
			return;
		}

		_services.GetRequiredService<GrownUpMode>().Leave();
		_ = ObserveAsync(ReplaceWithUpdatePageAsync(window));
	}

	// Modals are dismissed and the stack popped first, so a reading page stops its narration and a record page its recording
	// instead of carrying on behind the update page.
	private async Task ReplaceWithUpdatePageAsync(Window window)
	{
		if (_navigation is { } old)
		{
			for (int open = old.Navigation.ModalStack.Count; open > 0; open--)
			{
				await TryAsync(() => old.Navigation.PopModalAsync(animated: false));
			}

			await TryAsync(() => old.PopToRootAsync(animated: false));
		}

		window.Page = _navigation = BuildNavigation(_services.GetRequiredService<UpdatePage>());
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
		await TryAsync(() => DismissIntroAsync(navigation));
		await TryAsync(() => PopGrownUpPagesAsync(navigation));
	}

	private static async Task DismissGatesAsync(NavigationPage navigation)
	{
		foreach (GatePage gate in navigation.Navigation.ModalStack.OfType<GatePage>().ToList())
		{
			await TryAsync(gate.DismissAsync);
		}
	}

	// The intro (Slice 155) is a modal of its own: it closes with the app, but a first run keeps its saved step, so coming back goes through the gate.
	private static async Task DismissIntroAsync(NavigationPage navigation)
	{
		bool IsIntro(Page p) => p is NavigationPage { RootPage: IntroWizardPage };
		for (int guard = navigation.Navigation.ModalStack.Count; guard > 0 && navigation.Navigation.ModalStack.Any(IsIntro); guard--)
		{
			await navigation.Navigation.PopModalAsync(animated: false);
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
