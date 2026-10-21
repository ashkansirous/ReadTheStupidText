using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The reading screen (Slice 62): the story in picture or text mode, the spoken sentence marked,
/// the page turning by itself as reading reaches it (Decision 80) until the child swipes away, and
/// a tap on a sentence reading from there. <see cref="ReadingSession"/> does the reading; the
/// <see cref="PageFollower"/> decides which page is shown.
/// </summary>
public partial class ReadingPage : ContentPage
{
	private readonly ReadingSession _session;
	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly IServiceProvider _services;
	private readonly VoicePackInstaller _installer;
	private readonly IVoiceModelService _model;
	private readonly SpokenLanguage _spoken;
	private readonly PageFollower _follower = new();
	private readonly BusyGuard _busy = new();
	private Story? _story;
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _visible;
	private bool _stoppedDuringGate;

	public ReadingPage(ReadingSession session, IStoryLibrary library, GrownUpGate gate, IServiceProvider services,
		VoicePackInstaller installer, IVoiceModelService model, SpokenLanguage spoken)
	{
		InitializeComponent();
		Header.Padding = UiFlow.Mirror(Header.Padding);
		_spoken = spoken;
		_session = session;
		_library = library;
		_gate = gate;
		_services = services;
		_installer = installer;
		_model = model;
		_session.SentenceChanged += OnSentenceChanged;
		_session.StateChanged += OnSessionStateChanged;
		_session.Failed += OnReadFailed;
		_session.TranslationSkipped += (_, _) => _spoken.Use(_story?.Language ?? UiLanguage.English);
		ShowPackStatus();
	}

	/// <summary>Opens the page on <paramref name="story"/>. Called once, right after the page is created.</summary>
	public void Load(Story story)
	{
		_story = story;
		TitleLabel.Text = story.Title;
		_spoken.Use(story);
		_sentences = StorySentences.Build(story.Text, LanguageRules.For(story.Language));
		_session.Open(story, _sentences);
		_theme = CurrentTheme();
		Modes.Set(story.Mode, _theme);
		var content = new ReadingContent(story.Text, _sentences, DinoFonts.ReadingFor(story.Language),
			DinoFonts.IsRightToLeft(story.Language), ScansOf(story));
		Surface.Show(content, story.Mode, _theme);
		ApplyTheme();
		if (_sentences.Count > 0)
		{
			_session.MoveTo(0);
		}
	}

	// A story with pictures draws its real pages in picture mode; one without keeps the generated pages.
	private ScanSource? ScansOf(Story story) =>
		story.HasPictures ? new ScanSource(story.Pages, file => _library.TryResolveFile(story.Id, file)) : null;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_visible = true;
		Unsubscribe();
		_session.Attach();
		_installer.Changed += OnInstallerChanged;
		_model.ReadyChanged += OnInstallerChanged;
		Microsoft.Maui.Controls.Application.Current!.RequestedThemeChanged += OnThemeChanged;
		ShowPackStatus();
		if (CurrentTheme() != _theme)
		{
			_theme = CurrentTheme();
			ApplyTheme();
		}
	}

	// Disappearing also fires for a locked screen, another app or the gate over this page; the read
	// carries on then (it is audio). Only a page that has left the navigation stack stops it. The
	// check runs one tick later so it doesn't depend on whether the pop has already removed the page.
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_visible = false;
		Dispatcher.Dispatch(() =>
		{
			if (!Navigation.NavigationStack.Contains(this))
			{
				Unsubscribe();
				_session.Detach();
				_session.Stop();
			}
		});
	}

	private void Unsubscribe()
	{
		_installer.Changed -= OnInstallerChanged;
		_model.ReadyChanged -= OnInstallerChanged;
		if (Microsoft.Maui.Controls.Application.Current is { } app)
		{
			app.RequestedThemeChanged -= OnThemeChanged;
		}
	}

	private static ReadingTheme CurrentTheme() =>
		ReadingTheme.For(Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark);

	private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		_theme = CurrentTheme();
		ApplyTheme();
	}

	private void ApplyTheme()
	{
		BackgroundColor = _theme.Page;
		if (_visible)
		{
			SystemBarIcons.ForPage(this);
		}

		TitleLabel.TextColor = _theme.Ink;
		BackIcon.Color = _theme.BackIcon;
		LockButton.BackgroundColor = _theme.ControlFill;
		LockIcon.Color = _theme.ControlIcon;
		WanderPill.BackgroundColor = _theme.PillFill;
		PillLabel.TextColor = _theme.PillText;
		Modes.Set(Modes.Mode, _theme);
		Surface.SetTheme(_theme);
		RenderChrome();
	}

	private void OnSentenceChanged(object? sender, int sentence)
	{
		Surface.Mark(sentence, _session.CurrentPosition);
		int page = Surface.PageOfSentence(sentence, _session.CurrentPosition);
		if (page >= 0)
		{
			// While a layout is pending there is no page to follow; OnLaidOut catches up once it is adopted.
			_follower.Read(page);
			ShowFollowedPage();
		}

		RenderChrome();
	}

	private void ShowFollowedPage()
	{
		if (_follower.ShownPage != Surface.CurrentPage)
		{
			Surface.ShowPage(_follower.ShownPage);
		}
	}

	private void OnSessionStateChanged(object? sender, EventArgs e) => RenderChrome();

	// The pages were laid out again (a new size or mode). A follower goes with the reading; a child who
	// had wandered keeps the page that now holds what they were looking at (the surface remapped it).
	private void OnLaidOut(object? sender, EventArgs e)
	{
		int reading = Math.Max(0, Surface.PageOfSentence(_session.CurrentSentence, _session.CurrentPosition));
		_follower.Relaid(reading, Surface.CurrentPage);
		ShowFollowedPage();
		RenderChrome();
	}

	// After a finished read a swipe also means "from here": Play then starts at the shown page.
	private void OnPageSwiped(object? sender, int page)
	{
		_session.ClearFinished();
		_follower.Swiped(page);
		RenderChrome();
	}

	// Dots, the wander pill and the transport row all follow the same three facts: the page shown,
	// whether the child has wandered from the page being read, and whether anything is reading.
	private void RenderChrome()
	{
		SessionState state = _session.State;
		bool wandering = _follower.IsWandering && state != SessionState.Idle;
		Dots.Show(Surface.PageCount, Surface.CurrentPage, _theme);
		Transport.Apply(state, wandering, _theme);
		WanderPill.IsVisible = wandering;
		PillLabel.Text = ReadingText.ReadingPage(_follower.ReadingPage + 1);
	}

	private void OnModeChanged(object? sender, ReadingMode mode)
	{
		Surface.SetMode(mode);
		_ = SaveModeAsync(mode);
	}

	// The choice is remembered per story in story.json. A failed save only loses the preference.
	private async Task SaveModeAsync(ReadingMode mode)
	{
		if (_story is not { } story)
		{
			return;
		}

		try
		{
			Story updated = story with { PreferredMode = mode };
			await _library.UpdateAsync(updated);
			_story = updated;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not save the reading mode: {ex}");
		}
	}

	private void OnSentenceTapped(object? sender, int sentence)
	{
		_follower.ResumeAt(Surface.CurrentPage);
		_ = _session.StartAtAsync(sentence);
	}

	private async void OnPlayTapped(object? sender, EventArgs e)
	{
		switch (_session.State)
		{
			case SessionState.Loading:
				_session.Stop();
				break;
			case SessionState.Playing when _follower.IsWandering:
				JumpBackToReading();
				break;
			case SessionState.Playing:
				_session.Pause();
				break;
			case SessionState.Paused:
				JumpBackToReading();
				_session.Resume();
				break;
			default:
				await StartFromPlayAsync();
				break;
		}
	}

	private void JumpBackToReading()
	{
		_follower.Resume();
		ShowFollowedPage();
		RenderChrome();
	}

	// Play from nothing reads from the top after a finished read, from the page the child swiped to,
	// else from the sentence the highlight is on.
	private async Task StartFromPlayAsync()
	{
		int sentence = _session.Finished ? 0
			: _follower.IsWandering ? Surface.FirstSentenceOnPage(_follower.ShownPage)
			: Math.Max(0, _session.CurrentSentence);
		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(sentence)));
		await _session.StartAtAsync(sentence);
	}

	private async void OnPreviousTapped(object? sender, EventArgs e) => await SkipAsync(-1);

	private async void OnNextTapped(object? sender, EventArgs e) => await SkipAsync(1);

	// Skip moves by one sentence, the highlight unit. While nothing plays (idle or paused) it only
	// moves the mark and starts no audio; a paused read is dropped so Play starts at the mark.
	private async Task SkipAsync(int delta)
	{
		SessionState state = _session.State;
		bool playing = state is SessionState.Playing or SessionState.Loading;
		SkipPlan plan = SkipPlanner.Plan(_session.CurrentSentence, delta, _sentences.Count, playing);
		if (plan.Target < 0)
		{
			return;
		}

		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(plan.Target)));
		if (plan.StartsAudio)
		{
			await _session.StartAtAsync(plan.Target);
			return;
		}

		if (state == SessionState.Paused)
		{
			_session.Stop();
		}

		_session.MoveTo(plan.Target);
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnLockTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(OpenGrownUpAsync);

	// The story pauses while the gate is open (a read still loading is stopped, its sentence kept) and
	// goes on if the grown-up backs out, but only when the child is looking at this page: not when the
	// gate was closed because the app went to the background, and not when something else is on top.
	private async Task OpenGrownUpAsync()
	{
		SessionState before = _session.State;
		int sentence = Math.Max(0, _session.CurrentSentence);
		PauseForGate(before);
		if (await AskGateAsync())
		{
			await GoToManageAsync();
			return;
		}

		if (!_stoppedDuringGate && IsOnTop())
		{
			await ResumeAfterGateAsync(before, sentence);
		}
	}

	private void PauseForGate(SessionState state)
	{
		if (state == SessionState.Loading)
		{
			_session.Stop();
			return;
		}

		_session.Pause();
	}

	private async Task<bool> AskGateAsync()
	{
		_stoppedDuringGate = false;
		Window? window = Window;
		if (window is not null)
		{
			window.Stopped += OnWindowStopped;
		}

		try
		{
			return await _gate.RequestAsync(Navigation);
		}
		finally
		{
			if (window is not null)
			{
				window.Stopped -= OnWindowStopped;
			}
		}
	}

	private void OnWindowStopped(object? sender, EventArgs e) => _stoppedDuringGate = true;

	private bool IsOnTop() =>
		Navigation.ModalStack.Count == 0 && Navigation.NavigationStack.LastOrDefault() == this;

	private async Task ResumeAfterGateAsync(SessionState before, int sentence)
	{
		switch (before)
		{
			case SessionState.Playing:
				_session.Resume();
				break;
			case SessionState.Loading:
				await _session.StartAtAsync(sentence);
				break;
		}
	}

	// Manage mode opens over the gallery, as from the gallery's own lock; leaving this page stops the read.
	private async Task GoToManageAsync()
	{
		var root = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page as NavigationPage;
		await Navigation.PopToRootAsync(animated: false);
		if (root is not null)
		{
			await root.PushAsync(_services.GetRequiredService<ManagePage>());
		}
	}

	private async void OnReadFailed(object? sender, EventArgs e) =>
		await DisplayAlertAsync(ReadingText.CouldntRead, ReadingText.ReadFailed, ReadingText.Ok);

	private void OnInstallerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ShowPackStatus);

	private void OnPackStatusTapped(object? sender, TappedEventArgs e)
	{
		if (_installer.CanRetry)
		{
			_ = _installer.StartAsync();
		}
	}

	// Every non-ready pack state, Failed included, has copy (with its retry hint) in
	// PackStatusText; tapping the label retries when the installer allows it.
	private void ShowPackStatus()
	{
		string? status = LocalizedPackStatus.Compose(_installer.State, _installer.Progress);
		StatusLabel.Text = status;
		StatusLabel.IsVisible = status is not null;
	}
}
