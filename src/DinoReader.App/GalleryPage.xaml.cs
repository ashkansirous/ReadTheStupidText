using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

public partial class GalleryPage : ContentPage
{
	private const double LanguageButtonHeight = 48;
	private const double LanguageMenuGap = 6;
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
	private Story? _sheetStory; // the book the "This book" sheet is open on
	private bool _sheetChanged; // a switch was saved while the sheet was open: the tiles are rebuilt when it closes
	private readonly SemaphoreSlim _sheetSaveLock = new(1, 1);
	private readonly VoicePreview _preview;
	private readonly StoryLanguageButton _languageButton; // Slice 173 (mock 79): the child's story-language button, left of the grown-ups button, with two or three story languages

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
		BookInfo.OriginalChanged += SaveShowOriginalAsync;
		BookInfo.VoiceChanged += SaveUseVoiceAsync;
		BookInfo.CastRequested += OpenSheetCastAsync;
		BookInfo.RecordRequested += OpenSheetRecordAsync;
		BookInfo.EditRequested += OpenSheetEditAsync;
		BookInfo.ExportRequested += ExportSheetBookAsync;
		BookInfo.RemoveRequested += RemoveSheetBookAsync;
		BookInfo.Dismissed += OnBookSheetDismissed;
		_languageButton = AddLanguageButton();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, GalleryRoot.RowDefinitions.Count);
		GalleryRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	// Added beside the grown-ups button in code (the header is a three-column grid): the language button takes column 2, the gear its own column 3, and the
	// grown-ups button and Done (never shown together) move to column 4, so no two controls ever share a cell.
	private StoryLanguageButton AddLanguageButton()
	{
		var header = (Grid)GrownUpsButton.Parent;
		header.ColumnDefinitions.Insert(2, new ColumnDefinition(GridLength.Auto));
		header.ColumnDefinitions.Insert(3, new ColumnDefinition(GridLength.Auto));
		Grid.SetColumn(SettingsButton, 3);
		Grid.SetColumn(GrownUpsButton, 4);
		Grid.SetColumn(DoneButton, 4);
		var button = new StoryLanguageButton(LanguageButtonHeight, OpenLanguageMenuAsync) { IsVisible = false, AutomationId = "GalleryLanguage" };
		header.Add(button, 2, 0);
		return button;
	}

	// The same list as the reading page (88): one pick for all books, remembered. The shelf titles stay in the app language.
	private async Task OpenLanguageMenuAsync()
	{
		await _busy.RunAsync(async () =>
		{
			StoryLanguageSet set = StoryLanguages.Current();
			string current = StoryLanguages.Pick();
			_languageButton.SetOpen(true);
			string? picked = await StoryLanguageMenu.PickAsync((Grid)_languageButton.Parent.Parent, _languageButton, ChoosableLanguages(set), current, LanguageMenuGap);
			_languageButton.SetOpen(false);
			if (picked is null || picked == current)
			{
				return;
			}

			StoryLanguages.SetPick(picked);
			_services.GetRequiredService<UsageCounter>().RecordGeneral(UsageEventName.StoryLanguageSwitched, picked);
			ApplyLanguageButton();
		});
	}

	// Shown in the kids' shelf only, and only when the parent set two or three story languages.
	private void ApplyLanguageButton()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		IReadOnlyList<string> choosable = ChoosableLanguages(set);
		_languageButton.IsVisible = !_mode.IsGrownUp && choosable.Count >= 2;
		string pick = StoryLanguages.Pick();
		_languageButton.SetLanguage(LanguagePick.ShelfShown(choosable, pick, set.Main));
	}

	// The child can pick only a language whose voice is already on the phone (Slice 173): a download starts from Settings or the intro, behind the gate.
	private IReadOnlyList<string> ChoosableLanguages(StoryLanguageSet set)
	{
		var packs = _services.GetRequiredService<StoryLanguagePacks>();
		return [.. set.All.Where(language => language == set.Main || packs.StateOf(language) == LanguageVoiceState.Ready)];
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
		catch (Exception ex)
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
		if (!_mode.IsGrownUp)
		{
			BookInfo.Dismiss(); // also refreshes the tiles when a switch was saved
		}

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
		GrownUpsButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Gallery_GrownUpsButton);
		GrownUpsButton.SetValue(ToolTipProperties.TextProperty, AppResources.Gallery_GrownUpsButton);
		GrownUpsButton.SetValue(SemanticProperties.HintProperty, AppResources.Gallery_OpensGateHint);
		SettingsButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Settings_GearLabel);
		SettingsButton.SetValue(ToolTipProperties.TextProperty, AppResources.Settings_GearLabel);
		DoneButton.Text = AppResources.Common_Done;
		AddBookLabel.Text = AppResources.Gallery_AddFromBooks;
		AddBookButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Gallery_AddFromBooks);
		StoreLabel.Text = AppResources.Store_Title;
		StoreButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Store_Title);
		StorytellerLine.Invalidate();
	}

	private readonly WaitController _wait;

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

	// Slice 181: staged store updates are swapped in once per process, and the shelf is never read before that finished, whoever asks (first load, library change, resume).
	// The wait is logged so the 500 ms loading-state rule can be measured; past 500 ms the "Updating your books" overlay shows (no Cancel: a half swap is journaled), and it ends even on failure.
	private async Task WaitForStagedUpdatesAsync()
	{
		var clock = Stopwatch.StartNew();
		await _wait.RunAsync(WaitStage.Saving, AppResources.Gallery_UpdatingBooks, canCancel: false, async _ =>
		{
			await _starters.InstallOnceAsync();
			await _services.GetRequiredService<StagedBookUpdates>().ApplyOnceAsync();
			return true;
		});
		Debug.WriteLine($"[BookUpdate] gallery waited {clock.ElapsedMilliseconds} ms for the staged apply");
	}

	private async Task RefreshAsync()
	{
		await WaitForStagedUpdatesAsync();
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
		GrownUpsButton.IsVisible = !grownUp;
		SettingsButton.IsVisible = grownUp;
		ApplyLanguageButton();
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
			await _busy.RunAsync(() => _mode.IsGrownUp ? OpenBookSheetAsync(tile.Story) : _actions.ReadAsync(Navigation, tile.Story));
		}
	}

	// Grown-up mode: a book's "⋯" or its bare cover opens its options sheet (Slice 189, mock 85b; it grew out of the "This book" sheet of Slices 110 and 111): Show original text,
	// Read with my voice, Record, Cast, Edit pages (own books), Export (developer switch) and Remove from the shelf.
	private async Task OpenBookSheetAsync(Story tapped)
	{
		Story story = await _library.GetAsync(tapped.Id) ?? tapped;
		_sheetStory = story;
		_sheetChanged = false;
		BookInfo.Show(SheetContentOf(story));
	}

	private BookSheet.Content SheetContentOf(Story story)
	{
		bool translated = _services.GetRequiredService<StoryTranslator>().Applies(story, StoryTranslator.MyLanguage);
		BookOrigin origin = BookOrigins.Of(story);
		StoryTile tile = StoryTile.From(story, _library, _mode);
		string original = LanguageNames.NativeName(story.Language);
		string spoken = LanguageNames.NativeName(StoryTranslator.MyLanguage);
		string title = _services.GetRequiredService<StoryTranslator>().ShelfTitle(story);
		var plan = BookSheetPlan.For(origin, translated, StoryRecordings.Count(story) > 0, DeveloperMode.Enabled);
		return new BookSheet.Content(title, BookOrigins.Label(origin), tile.CoverImagePath, tile.CoverFill, plan, story.ShowOriginalText,
			ReadingText.ShowOriginalHint(original, spoken), VoiceOfferOf(story));
	}

	// "Read with my voice" shows only once a page is recorded.
	private static BookSheet.VoiceOffer? VoiceOfferOf(Story story) =>
		StoryRecordings.Count(story) is var recorded && recorded > 0 ? new BookSheet.VoiceOffer(recorded, story.Pages.Count, story.UseRecordings) : null;

	private Task SaveShowOriginalAsync(bool showOriginal) => SaveSheetStoryAsync(story => story with { ShowOriginalText = showOriginal });

	private Task SaveUseVoiceAsync(bool useVoice) => SaveSheetStoryAsync(story => story with { UseRecordings = useVoice });

	// Each switch is remembered per story in story.json, on top of the latest stored copy (cast edits saved meanwhile stay); one save at a time. A failed save only loses the preference.
	private async Task SaveSheetStoryAsync(Func<Story, Story> change)
	{
		await _sheetSaveLock.WaitAsync();
		try
		{
			if (_sheetStory is { } shown)
			{
				Story updated = change(await _library.GetAsync(shown.Id) ?? shown);
				await _library.UpdateAsync(updated);
				_sheetStory = updated;
				_sheetChanged = true;
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not save the book switch: {ex}");
		}
		finally
		{
			_sheetSaveLock.Release();
		}
	}

	// A switch tapped just before an action (the quiet rows close the sheet first) is saved before the action reads the story, so the editor never saves the pre-switch copy.
	private async Task<Story?> SettledSheetStoryAsync()
	{
		await _sheetSaveLock.WaitAsync();
		_sheetSaveLock.Release();
		return _sheetStory;
	}

	private async Task OpenSheetCastAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => _actions.CastAsync(Navigation, story));
		}
	}

	private async Task OpenSheetRecordAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => _actions.RecordAsync(Navigation, story));
		}
	}

	private async Task OpenSheetEditAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => _actions.EditAsync(Navigation, story));
		}
	}

	private async Task ExportSheetBookAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => _actions.ExportAsync(story));
		}
	}

	// Remove from the shelf (Slice 189): asks first, with the sheet still open behind the question; a Store book says it can come back for free. Declined leaves the sheet as it was.
	private async Task RemoveSheetBookAsync()
	{
		if (_sheetStory is { } story)
		{
			await RunGrownUpAsync(() => RemoveBookAsync(story));
		}
	}

	// Holds the sheet's save lock from the question to the delete, so a pending switch save cannot write the folder back; a save queued behind it finds no sheet story and skips.
	private async Task RemoveBookAsync(Story story)
	{
		await _sheetSaveLock.WaitAsync();
		try
		{
			DeleteOutcome outcome = await _actions.ConfirmDeleteAsync(this, story, RemovePrompt(story, _services.GetRequiredService<StoryTranslator>().ShelfTitle(story)));
			if (outcome == DeleteOutcome.Declined)
			{
				return;
			}

			BookInfo.Hide();
			_sheetStory = null;
			_sheetChanged = false;
		}
		finally
		{
			_sheetSaveLock.Release();
		}

		await RefreshShelfAsync(); // also after a failed delete: a half-deleted story no longer lists
	}

	// The question names the book as the shelf and the sheet do (its translated title).
	private static DeletePrompt RemovePrompt(Story story, string shelfTitle)
	{
		bool fromStore = BookOrigins.Of(story) == BookOrigin.Store;
		return new DeletePrompt(AppResources.Format(AppResources.Book_RemoveTitle, shelfTitle), fromStore ? AppResources.Book_RemoveBodyStore : AppResources.Delete_Body, AppResources.Book_RemoveConfirm);
	}

	// Waits for a save still in flight (a quick Back after a switch) so the tiles never keep the old flags; never throws out of async void.
	private async void OnBookSheetDismissed()
	{
		try
		{
			await _sheetSaveLock.WaitAsync();
			_sheetSaveLock.Release();
			if (_sheetChanged)
			{
				_sheetChanged = false;
				await RefreshShelfAsync();
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not refresh the shelf after the book sheet: {ex}");
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

	private async void OnOpenRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => OpenBookSheetAsync(tile.Story));
		}
	}

	// Seven taps on the title in grown-up mode flip the developer switch (Slice 91); the tiles are rebuilt, and the book sheet shows or hides Export.
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

	// The book-and-plus button (Slice 189): the gate first, then grown-up mode. A failed gate leaves the shelf as it was.
	private async void OnGrownUpsTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(EnterGrownUpModeAsync);

	private async Task EnterGrownUpModeAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			_mode.Enter();
		}
	}

	// The gear in grown-up mode: Settings with no second gate. Back from it (and from its sub-pages, one step each) ends here, then on the kids' shelf (Decision 169, mock 94).
	private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(OpenSettingsAsync);

	private async Task OpenSettingsAsync() => await Navigation.PushAsync(_services.GetRequiredService<SettingsPage>());

	// Back from grown-up mode lands on the kids' shelf, not out of the app (Slice 172, Decision 169, mock 94).
	protected override bool OnBackButtonPressed()
	{
		if (_languageButton.Parent?.Parent is Grid menuHost && StoryLanguageMenu.DismissOpen(menuHost))
		{
			return true; // the open language list closes first
		}

		if (BookInfo.IsVisible)
		{
			BookInfo.Dismiss();
			return true;
		}

		if (!_mode.IsGrownUp)
		{
			return AskToClose();
		}

		CompleteHandover();
		return true;
	}

	// The kids' shelf is home (Slice 174, Decision 169, mock 94): Android back asks "Close Dino Book Reader?" instead of leaving. iOS has no back button and
	// Windows closes from its window, so nothing changes there.
	private bool AskToClose()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return base.OnBackButtonPressed();
		}

		if (Navigation.ModalStack.Count > 0)
		{
			return true; // a modal is open or still closing: this back press is swallowed, never closes the app
		}

		_ = _busy.RunAsync(CloseIfConfirmedAsync);
		return true;
	}

	private async Task CloseIfConfirmedAsync()
	{
		if (await CloseQuestionPage.AskAsync(Navigation))
		{
			Application.Current?.Quit();
		}
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
