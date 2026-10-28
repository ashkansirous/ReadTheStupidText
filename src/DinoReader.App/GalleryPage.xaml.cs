using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

public partial class GalleryPage : ContentPage
{
	private const uint TileFadeMs = 180;
	private const double TileShrinkTo = 0.85;
	private const string TileTitleHeightKey = "TileTitleHeight";

	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private bool _introOpening;
	private bool _introGateDeclined;
	private readonly GrownUpMode _mode;
	private readonly StoryActions _actions;
	private readonly StarterStories _starters;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly ObservableCollection<object> _items = [];
	private readonly StorytellerDefault _storyteller;
	private bool _readySheetOpening;
	private readonly VoicePreview _preview;

	public GalleryPage(IStoryLibrary library, GrownUpGate gate, GrownUpMode mode, StoryActions actions, StarterStories starters, IServiceProvider services)
	{
		InitializeComponent();
		_library = library;
		_gate = gate;
		_mode = mode;
		_actions = actions;
		_starters = starters;
		_services = services;
		StoryList.ItemsSource = _items;
		_storyteller = services.GetRequiredService<StorytellerDefault>();
		_preview = new VoicePreview(services.GetRequiredService<ISpeechReader>());
		StorytellerLine.DownloadNow = _storyteller.DownloadNowAsync;
		StorytellerLine.Retry = _storyteller.RetryAsync;
		ReadySheet.Play = PlayNarratorAsync;
		ReadySheet.Closed = OnReadyClosed;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_mode.PropertyChanged -= OnModeChanged;
		_mode.PropertyChanged += OnModeChanged;
		_storyteller.Changed -= OnStorytellerChanged;
		_storyteller.Changed += OnStorytellerChanged;
		_preview.Attach();
		StoreClient store = _services.GetRequiredService<StoreClient>();
		store.LibraryChanged -= OnLibraryChanged;
		store.LibraryChanged += OnLibraryChanged;
		ApplyMode();
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed;
			window.Resumed += OnWindowResumed;
		}

		try
		{
			// The starters are on the shelf before the first list is read (Slice 91); a later call is a no-op.
			await _starters.InstallOnceAsync();
			await RefreshAsync();
			await ResumeIntroAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	// The first-run intro (Slice 155) opens on top of the gallery: right after "See what Dino can do", after a restart mid-intro (its step is
	// saved), and after leaving the app popped it. It is not pushed twice.
	private async Task ResumeIntroAsync()
	{
		if (!IntroState.InProgress || _introOpening || _introGateDeclined || Navigation.ModalStack.Any(p => p is NavigationPage { RootPage: IntroWizardPage }))
		{
			return;
		}

		_introOpening = true;
		try
		{
			// A fresh start comes from the first-run page, which already entered grown-up mode. Kids' mode here means the app was
			// left mid-intro and reopened, so a child may be holding it: the gate comes first.
			if (!_mode.IsGrownUp && !await _gate.RequestAsync(Navigation))
			{
				_introGateDeclined = true;
				return;
			}

			var intro = _services.GetRequiredService<IntroWizardPage>();
			await intro.PresentAsync(Navigation, firstRun: true);
		}
		catch (Exception ex)
		{
			// A bad saved step must never crash-loop the app: drop it and carry on with the gallery.
			Debug.WriteLine($"Dino Reader could not resume the intro: {ex}");
			IntroState.Clear();
		}
		finally
		{
			_introOpening = false;
		}
	}

	// A declined gate asks again only after the app comes back (not when the gate's own modal closes, which re-fires OnAppearing).
	private void OnWindowResumed(object? sender, EventArgs e)
	{
		_introGateDeclined = false;
		_ = ResumeIntroAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed;
		}

		_mode.PropertyChanged -= OnModeChanged;
		_storyteller.Changed -= OnStorytellerChanged;
		_preview.Detach();
		_services.GetRequiredService<StoreClient>().LibraryChanged -= OnLibraryChanged;
	}

	// A store download can finish on a background thread while the gallery shows; reload the list on the UI thread (Slice 92).
	private void OnLibraryChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	});

	// The tiles follow the mode through their binding; only the Add entry and the chrome change here.
	private void OnModeChanged(object? sender, PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		ApplyMode();
		if (_mode.IsGrownUp)
		{
			// A grown-up got in (settings gate): the saved intro may be offered again.
			_introGateDeclined = false;
			_ = ResumeIntroAsync();
		}
	});

	private void ApplyMode()
	{
		if (_shelfLanguage is not null && _shelfLanguage != LanguageChoice.Current())
		{
			_ = RefreshShelfAsync(); // the app language changed: the shelf titles (and the Add tile, rebuilt with them) follow it
		}

		ApplyTexts();

		ApplyModeChrome(_mode.IsGrownUp);
	}

	private string? _textLanguage; // the app language the fixed labels were set in

	// The labels are x:Static (bound once on this cached page), so a language change re-applies them here.
	private void ApplyTexts()
	{
		string language = LanguageChoice.Current();
		if (_textLanguage == language)
		{
			return;
		}

		_textLanguage = language;
		AppIcon.SetValue(SemanticProperties.DescriptionProperty, AppResources.App_Name);
		SettingsButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Settings_GearLabel);
		SettingsButton.SetValue(ToolTipProperties.TextProperty, AppResources.Settings_GearLabel);
		SettingsButton.SetValue(SemanticProperties.HintProperty, AppResources.Gallery_OpensGateHint);
		DoneButton.Text = AppResources.Common_Done;
		AddBookLabel.Text = AppResources.Gallery_AddFromBooks;
		AddBookButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Gallery_AddFromBooks);
		StoreLabel.Text = AppResources.Store_Title;
		StoreButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Store_Title);
		StorytellerLine.Invalidate();
	}

	private string? _shelfLanguage; // the app language the shelf titles were built in

	private async Task RefreshShelfAsync()
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	private async Task RefreshAsync()
	{
		IReadOnlyList<Story> stories = await _library.ListAsync();
		_shelfLanguage = LanguageChoice.Current();
		_items.Clear();
		// The Add button above the shelf replaced the Add tile (Slice 172, mock 85).
		foreach (object item in GalleryItems.Build(stories, _library, _mode, _services.GetRequiredService<StoryTranslator>().ShelfTitle).Where(item => item is not AddStoryEntry))
		{
			_items.Add(item);
		}

		ApplyModeChrome(_mode.IsGrownUp);
	}

	private void ApplyModeChrome(bool grownUp)
	{
		bool handover = grownUp && FirstRunState.HandoverPending;
		SettingsButton.IsVisible = !grownUp;
		DoneButton.IsVisible = grownUp && !handover;
		HandoverButton.IsVisible = handover;
		HandoverButton.Text = AppResources.Gallery_ReadyForChild;
		AppIcon.IsVisible = !grownUp;
		TitleLabel.Text = grownUp ? AppResources.Gallery_GrownUpsTitle : AppResources.Gallery_Title;
		ModeLabel.Text = AppResources.Gallery_GrownUpsSub;
		ModeLabel.IsVisible = grownUp;
		GrownUpActions.IsVisible = grownUp;
		ApplyStorytellerChrome(grownUp);
		BackgroundColor = DinoTokens.Get(grownUp ? DinoTokens.Sand : DinoTokens.Cream);
	}

	// The pack service reports from a worker thread (Slice 115).
	private void OnStorytellerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(() => ApplyStorytellerChrome(_mode.IsGrownUp));

	private void ApplyStorytellerChrome(bool grownUp)
	{
		StorytellerLine.Apply(_storyteller.Status, grownUp);
		if (grownUp && _storyteller.ReadyPending && !ReadySheet.IsVisible && !_readySheetOpening)
		{
			_readySheetOpening = true;
			_ = ShowReadySheetAsync();
		}
		else if (!grownUp)
		{
			// Never in the child's view: the note waits for the next time grown-up mode opens.
			ReadySheet.Hide();
		}
	}

	// The language the "ready" sheet names the narrator of: the grown-up's own when the installed storyteller pack speaks it (Slice 118: a version-2 pack speaks English, Spanish, Portuguese, Chinese; version 1 only English), else English.
	private string StorytellerLanguage()
	{
		string mine = LanguageChoice.Current(_services.GetRequiredService<ISettingsStore>());
		return KokoroVoiceTable.Serves(mine) && _storyteller.Speaks(mine) ? mine : KokoroVoiceTable.EnglishCode;
	}

	private async Task ShowReadySheetAsync()
	{
		try
		{
			string language = StorytellerLanguage();
			string narrator = await Task.Run(() => _services.GetRequiredService<StoryVoicing>().NarratorOf(language));
			if (_mode.IsGrownUp && _storyteller.ReadyPending)
			{
				ReadySheet.Show(CastText.VoiceName(narrator, language), language);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader ready sheet failed: {ex}");
		}
		finally
		{
			_readySheetOpening = false;
		}
	}

	private async Task PlayNarratorAsync()
	{
		try
		{
			string language = StorytellerLanguage();
			string narrator = _services.GetRequiredService<StoryVoicing>().NarratorOf(language);
			await _preview.PlayAsync(CastText.PreviewLineFor(language), narrator, VoiceFlavour.Storyteller);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator sample failed: {ex}");
		}
	}

	private void OnReadyClosed()
	{
		_preview.Stop();
		_storyteller.AcknowledgeReady();
	}

	private static StoryTile? TileOf(object? sender) => (sender as BindableObject)?.BindingContext as StoryTile;

	private async void OnStoryTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => _mode.IsGrownUp ? _actions.EditAsync(Navigation, tile.Story) : _actions.ReadAsync(Navigation, tile.Story));
		}
	}

	private void OnTitleProbeSizeChanged(object? sender, EventArgs e)
	{
		if (TitleProbe.Height > 0 && Math.Abs(TitleProbe.Height - (double)Resources[TileTitleHeightKey]) > 0.5)
		{
			Resources[TileTitleHeightKey] = Math.Ceiling(TitleProbe.Height);
		}
	}

	// Every grown-up action re-checks the mode right before it runs: a queued tap must not outlive the mode.
	private Task RunGrownUpAsync(Func<Task> work) => _busy.RunAsync(() => _mode.IsGrownUp ? work() : Task.CompletedTask);

	// The store (Slice 92, "Store" since Slice 172): a grown-up page, so it is only reachable in grown-up mode and closes with it.
	private async void OnStoreTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(OpenStoreAsync);

	private async Task OpenStoreAsync()
	{
		var store = _services.GetRequiredService<StorePage>();
		if (_mode.IsGrownUp)
		{
			await Navigation.PushAsync(store);
		}
	}

	private async void OnAddTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(() => _actions.EditAsync(Navigation, null));

	private async void OnEditRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.EditAsync(Navigation, tile.Story));
		}
	}

	private async void OnCastRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.CastAsync(Navigation, tile.Story));
		}
	}

	// Record (Slice 111): the page-by-page recording screen for this book.
	private async void OnRecordRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.RecordAsync(Navigation, tile.Story));
		}
	}

	private async void OnExportRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.ExportAsync(tile.Story));
		}
	}

	// Seven taps on the title in grown-up mode flip the developer switch (Slice 91); the tiles are rebuilt to show or hide Export.
	private async void OnTitleTapped(object? sender, TappedEventArgs e)
	{
		if (!_mode.IsGrownUp)
		{
			DeveloperMode.ResetTaps();
			return;
		}

		try
		{
			if (DeveloperMode.RegisterTap())
			{
				await RefreshAsync();
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	private async void OnDeleteRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile && sender is Element source)
		{
			await RunGrownUpAsync(() => DeleteAsync(tile, TileRootOf(source)));
		}
	}

	private async Task DeleteAsync(StoryTile tile, VisualElement? root)
	{
		DeleteOutcome outcome = await _actions.ConfirmDeleteAsync(this, tile.Story);
		if (outcome == DeleteOutcome.Deleted)
		{
			await AnimateOutAsync(root);
			if (!_items.Remove(tile))
			{
				// A refresh replaced the list during the fade: re-read so no deleted story lingers.
				await RefreshAsync();
			}
		}
		else if (outcome == DeleteOutcome.Failed)
		{
			// A half-deleted story no longer lists, so re-read after a failure.
			await RefreshAsync();
		}
	}

	// The tile's root is the story template's VerticalStackLayout; a recycled view must come back opaque.
	private static VisualElement? TileRootOf(Element source)
	{
		for (Element? e = source; e is not null; e = e.Parent)
		{
			if (e is VerticalStackLayout root)
			{
				return root;
			}
		}

		return null;
	}

	private static async Task AnimateOutAsync(VisualElement? root)
	{
		if (root is null)
		{
			return;
		}

		await Task.WhenAll(root.FadeToAsync(0, TileFadeMs), root.ScaleToAsync(TileShrinkTo, TileFadeMs));
		root.Opacity = 1;
		root.Scale = 1;
	}

	// The gear (Slice 172): the gate first, then Settings. A failed gate leaves the shelf as it was; Settings is a grown-up page, so leaving the app closes it.
	private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(OpenSettingsAsync);

	private async Task OpenSettingsAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			await Navigation.PushAsync(_services.GetRequiredService<SettingsPage>());
		}
	}

	// Back from grown-up mode lands on the kids' shelf, not out of the app (Slice 172, Decision 169, mock 94).
	protected override bool OnBackButtonPressed()
	{
		if (!_mode.IsGrownUp)
		{
			return base.OnBackButtonPressed();
		}

		CompleteHandover();
		return true;
	}

	private void OnDoneClicked(object? sender, EventArgs e) => _mode.Leave();

	// The first hand-over (Slice 93): from now on the header shows Done instead.
	private void OnHandoverClicked(object? sender, EventArgs e) => CompleteHandover();

	// Leaves grown-up mode; Back during the first hand-over counts as "Ready for my child", so the flag never outlives the mode.
	private void CompleteHandover()
	{
		FirstRunState.HandoverPending = false;
		_mode.Leave();
	}
}
