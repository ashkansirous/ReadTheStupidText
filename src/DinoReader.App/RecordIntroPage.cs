using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The page every tap on Record opens (Slice 279, Batch 48 mock frames 178, 179 and 189): the words that apply to every recording once, the two ways to record (a character at a time, or whole pages),
/// and the characters to record. A book that already has recordings leads with how much is recorded and offers "Listen and change" and "Record the rest". The choice is remembered per book and story language.
/// None of this text is repeated on the recording pages.
/// </summary>
public sealed class RecordIntroPage : ContentPage, IGrownUpPage
{
	private const double ColumnMaxWidth = 560;
	private const double SideMargin = 14; // frames 192 and 193: the body's and the bottom bar's side padding
	private const double RowHeight = 42;
	private const double BoxSize = 24;
	private const double BoxCorner = 7;
	private const double RadioSize = 20;
	private const double ModeRadius = 16;
	private const double ButtonHeight = 52;
	private const double OutlinedLabelSize = 15.5;
	private const double StartLabelSize = 17;
	private const double OutlinedPadding = 12;
	private const double OutlinedPaddingVertical = 6;
	private const double OutlinedStroke = 2;
	private const double PlayIconSize = 16;
	private const double PlayIconGap = 7;
	private const string OutlinedStrokeColor = "#C9C0F5";
	private const string DividerColor = "#efe9dd";
	private const string DoneGreen = "#2E7D32";
	private const double BlockSpacing = 8;
	private const double BlockPaddingVertical = 12;
	private const double BlockPaddingSide = 14;
	private const double BlockTopExtra = 4;
	private const double NoteIconSize = 18;
	private const double NoteGap = 10;
	private const double NoteSpacing = 9;
	private const double ModeIconSize = 22;
	private const double ModeSpacing = 6;
	private const double CardHeight = 56;
	private const double CardGap = 8;
	private const double CardFlagSize = 30;
	private const double CardFlagGap = 9;
	private const double SelectedRing = 2.5;
	private const float FlagRingAlpha = 0.14f;
	private const double BarHeight = 8;
	private const double ChangeHeight = 40;
	private const string PageName = "RecordIntro";

	/// <summary>The AutomationIds the scripted check taps.</summary>
	internal const string StartId = "RecordIntroStart";
	internal const string ListenId = "RecordIntroListen";
	internal const string RestId = "RecordIntroRest";
	internal const string ModeCharacterId = "RecordModeCharacter";
	internal const string ModePageId = "RecordModePage";
	internal const string ChangeId = "RecordIntroChange";
	internal const string SpeakerIdPrefix = "RecordSpeaker-";
	internal const string ProgressId = "RecordIntroProgress";
	internal const string UnavailableId = "RecordLanguageUnavailable";
	internal const string RetryId = "RecordLanguageRetry";

	private readonly IStoryLibrary _library;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly GrownUpHeader _header = new("RecordIntroDone");
	private readonly VerticalStackLayout _body = new() { Spacing = BlockSpacing };
	// Frame 192's bottom bar: padding 10 14 16, two equal columns, 8 gap. The side space is the Column margin, so the buttons are half the screen less 14 and 4.
	private readonly Grid _bottom = new() { ColumnSpacing = 8, Padding = new Thickness(0, 10, 0, 16) };
	internal const string LanguageIdPrefix = "RecordLanguage-";

	private Story? _story;
	private RecordBook? _book; // the book in the picked story language (the words of its translation, Slice 286)
	private RecordBook? _source; // the book as the story itself reads, whose sentence numbers every language shares
	private readonly Dictionary<string, RecordBook> _books = [];
	private string? _unavailable; // the language whose words could not be made, shown with Retry (Slice 286)
	private string _language = UiLanguage.English; // the story language being recorded in: the app language until the grown-up picks another
	private RecordChoice _choice = new(RecordMode.PerCharacter, new HashSet<string>());
	private bool _showChange;
	private (string Key, IReadOnlyList<RecordPiece> Pieces)? _cut; // the book's pieces for the mode and ticks in Key, cut once on a worker thread
	private bool _warming;
	private readonly Dictionary<string, IReadOnlyList<RecordPiece>> _lines = []; // each story language's spoken lines as the record page cuts them in it: every count comes from these (frame 193)
	private readonly HashSet<string> _linesFailed = []; // languages whose words could not be made: not tried again until the page is opened again or the grown-up taps that language's card
	private CancellationTokenSource? _warmCts; // the background cut that is running: cancelled when the page goes away or a newer version of the story loads
	private bool _cutting; // a cut has run over 500 ms: counts still being cut show a quiet "..." instead of staying blank
	private bool _shown;
	private const string PendingCount = "…";
	private static readonly TimeSpan CuttingDelay = TimeSpan.FromMilliseconds(500);
	private RecordPage? _record; // the recording page last pushed from here: the book is read again only after it has finished saving

	public RecordIntroPage(IStoryLibrary library, IServiceProvider services)
	{
		_library = library;
		_services = services;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_wait = new WaitController(_waitOverlay);
		_header.ButtonVisible = false;
		_header.BackRequested += async (_, _) => await BackAsync();
		var scroll = new ScrollView { Content = new Grid { Children = { Column(_body) } } };
		var root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		root.Add(_header, 0, 0);
		root.Add(scroll, 0, 1);
		root.Add(Column(_bottom), 0, 2);
		Content = new Grid { Children = { root, _waitOverlay } };
	}

	/// <summary>Set by the intro wizard when it pushes this page: the pages that follow end in "Continue" (Slice 229).</summary>
	public bool LaunchedFromIntro { get; set; }

	/// <summary>Opens the page on <paramref name="story"/>, reading its sentences and speakers once (shows the wait overlay if that takes over 500 ms).</summary>
	public async Task LoadAsync(Story story)
	{
		_story = story;
		_language = StoryLanguages.Current().Main;
		_header.Title = AppResources.Record_YourVoice; // frames 192 and 193: "Your voice" in every state
		_header.Subtitle = story.Title;
		_book = await TryBookAsync(story, _language);
		if (_book is not null)
		{
			_choice = RecordChoices.Load(story, _book, _language);
		}

		Render();
	}

	// The book in <language> behind the wait overlay (with Cancel: a translation engine can run long), or null when it could not be made: the page then shows the language as unavailable
	// with Retry (Slice 286), never the story's own words under another language's name. Nothing is cached for a failure, so Retry works the translation again.
	private async Task<RecordBook?> TryBookAsync(Story story, string language)
	{
		CancellationToken cancellation = _wait.BeginCancellable(WaitStage.GettingFile, AppResources.Wait_GettingBook);
		try
		{
			RecordBook? book = await BookInAsync(story, language, cancellation);
			_unavailable = book is null ? language : null;
			return book;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the grown-up cancelled the wait; the page stays as it was (with no book yet, it shows Retry)
			_unavailable = _book is null ? language : null; // a cancelled switch is not a failure: the book already shown stays, with no "unavailable" line beside it
			return null;
		}
		catch (Exception ex)
		{
			// no-fallback: the language shows as unavailable with Retry; no other language's words are shown for it
			Debug.WriteLine($"Dino Reader could not read the book in {language} for the record page: {ex.Message}");
			_unavailable = language;
			return null;
		}
		finally
		{
			_wait.End();
		}
	}

	// The book as it reads in <language>: the story's own words, or those of its translation when it is read translated in that language (the same text the reading screen shows), with the
	// speakers keeping the app language's names (labels stay in the app language). Both share the story's sentence numbers, so a clip's key means the same sentences in every language. Worked out once per language;
	// null when the translation could not be made.
	private async Task<RecordBook?> BookInAsync(Story story, string language, CancellationToken cancellation)
	{
		if (_books.TryGetValue(language, out RecordBook? cached))
		{
			return cached;
		}

		_source ??= await Task.Run(() => RecordSplitBook.Of(story), cancellation);
		RecordBook? book = await RecordBooks.InAsync(_services, story, _source, language, cancellation);
		if (book is not null)
		{
			_books[language] = book;
		}

		return book;
	}

	// An async-void handler must never throw: a failed Back is logged and the page stays.
	private async Task BackAsync()
	{
		try
		{
			await _busy.RunAsync(() => Navigation.PopAsync());
		}
		catch (Exception ex)
		{
			// no-fallback: nothing was changed; the grown-up taps Back again
			Debug.WriteLine($"Dino Reader record intro could not go back: {ex}");
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_shown = true;
		_linesFailed.Clear(); // a language whose words failed is tried again each time the page opens
		await RefreshStoryAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_shown = false;
		CancelWarm();
	}

	// Stops the background cut (its half-made results are dropped) and clears the loading mark.
	private void CancelWarm()
	{
		_warmCts?.Cancel();
		_warmCts = null;
		_cutting = false;
	}

	// Coming back from the recording page: the recordings may have changed, so the counts and buttons are read again.
	private async Task RefreshStoryAsync()
	{
		if (_story is null || _book is null)
		{
			return;
		}

		try
		{
			if (_record is { } record)
			{
				await record.FinishedAsync; // a take left pending by a swipe back is put in place before the book is read again
			}

			Story? latest = await _library.GetAsync(_story.Id);
			if (latest is not null)
			{
				await ReloadBookIfWordsChangedAsync(latest);
				_story = latest;
				Render();
			}
		}
		catch (Exception ex)
		{
			// no-fallback: the page keeps the counts it had; nothing is lost (this runs from an async-void OnAppearing, so it must not throw)
			Debug.WriteLine($"Dino Reader could not reload the book for the record page: {ex.Message}");
		}
	}

	// The cached books were cut from the story's words, language and cast: when the story was edited meanwhile they are dropped and the picked language's book is made again (a story edit must
	// never leave the cards, the checklist or the clips' sentence numbers on stale words).
	private async Task ReloadBookIfWordsChangedAsync(Story latest)
	{
		if (_story is not { } old || (old.Text == latest.Text && old.Language == latest.Language && old.Cast.Select(c => (c.CharacterId, c.Name)).SequenceEqual(latest.Cast.Select(c => (c.CharacterId, c.Name)))))
		{
			return;
		}

		CancelWarm();
		_source = null;
		_books.Clear();
		_lines.Clear();
		_linesFailed.Clear();
		_book = await TryBookAsync(latest, _language);
		if (_book is not null)
		{
			_choice = RecordChoices.Load(latest, _book, _language);
			_cut = null;
		}
	}

	private static View Column(View content)
	{
		content.HorizontalOptions = LayoutOptions.Fill;
		content.Margin = new Thickness(SideMargin, 0);
		// The holder fills the room it has, up to the maximum, from the first frame: a centred holder is only as wide as its content, so the button row's Star columns shrank to their labels and clipped them
		// (Slice 286). No width is set from a size event, so there is no narrow first frame and nothing stale after a resize or rotation (the Windows head and wide screens keep the maximum width).
		return new Grid { MaximumWidthRequest = ColumnMaxWidth + 2 * SideMargin, HorizontalOptions = LayoutOptions.Fill, Children = { content } };
	}

	// ---- drawing ----

	private void Render()
	{
		if (_story is not { } story)
		{
			return;
		}

		if (_book is not { } book)
		{
			RenderUnavailable(story);
			return;
		}

		bool has = StoryRecordings.Count(story, _language) > 0;
		_header.Title = AppResources.Record_YourVoice;
		_body.Children.Clear();
		_bottom.Children.Clear();
		_bottom.ColumnDefinitions = [];
		if (StoryLanguages.Current().HasChoice)
		{
			_body.Add(LanguageRows(story));
		}

		if (_unavailable is { } missing)
		{
			_body.Add(UnavailableBlock(missing));
		}

		if (has)
		{
			_body.Add(ProgressBlock(story, book));
		}

		if (!has)
		{
			AddNotesAndModes();
		}

		_body.Add(_choice.Mode == RecordMode.PerCharacter ? Checklist(story, book) : LongPagesLine());
		if (has)
		{
			_body.Add(ChangeLink()); // frame 192: below the list, out of view on a small phone
			if (_showChange)
			{
				AddNotesAndModes();
			}
		}

		AddButtons(story, book, has);
		Debug.WriteLine($"Dino Reader {PageName}: mode={_choice.Mode} ticked={_choice.Ticked.Count} recorded={StoryRecordings.Count(story, _language)} language={_language} lines={(_lines.TryGetValue(_language, out IReadOnlyList<RecordPiece>? own) ? own.Count : -1)}");
		if (_shown && PendingLines(story).Count > 0)
		{
			_ = WarmLinesAsync(story);
		}
	}

	// ---- each language's own line counts (frame 193) ----

	// The languages whose lines are needed and not cut yet: the picked one and those with recordings (their "x of y"); any other shows "Nothing yet" and is cut only when picked.
	// The picked language comes first, so its counts are not held up by another language's translation.
	private List<string> PendingLines(Story story) => StoryLanguages.Current().All
		.Where(l => (l == _language || StoryRecordings.Count(story, l) > 0) && !_lines.ContainsKey(l) && !_linesFailed.Contains(l))
		.OrderBy(l => l == _language ? 0 : 1).ToList();

	// Cuts the needed languages' lines off the UI thread, in the background, and draws the counts as each is ready; until then a count is blank (a quiet "..." after 500 ms), never another language's number.
	// Leaving the page or loading a newer version of the story cancels it and its results are dropped.
	private async Task WarmLinesAsync(Story story)
	{
		if (_warmCts is not null)
		{
			return;
		}

		var cts = new CancellationTokenSource();
		_warmCts = cts;
		_ = ShowCuttingAfterDelayAsync(cts);
		while (!cts.IsCancellationRequested && PendingLines(story) is { Count: > 0 } pending)
		{
			await CutLinesAsync(story, pending[0], cts.Token);
			if (!cts.IsCancellationRequested)
			{
				Render();
			}
		}

		bool current = ReferenceEquals(_warmCts, cts) && !cts.IsCancellationRequested;
		cts.Cancel(); // ends the 500 ms timer
		if (current)
		{
			_warmCts = null;
			_cutting = false;
			Render();
		}
	}

	private async Task ShowCuttingAfterDelayAsync(CancellationTokenSource cts)
	{
		try
		{
			await Task.Delay(CuttingDelay, cts.Token);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the cut finished or was cancelled before it took long; no loading mark is needed
			return;
		}

		if (ReferenceEquals(_warmCts, cts))
		{
			_cutting = true;
			Render();
		}
	}

	private bool CutPending(string language) => _cutting && !_lines.ContainsKey(language) && !_linesFailed.Contains(language);

	private async Task CutLinesAsync(Story story, string language, CancellationToken cancellation)
	{
		try
		{
			RecordBook source = _source ?? await Task.Run(() => RecordSplitBook.Of(story), cancellation);
			cancellation.ThrowIfCancellationRequested();
			_source ??= source;
			RecordBook? book = _books.GetValueOrDefault(language) ?? await RecordBooks.InAsync(_services, story, source, language, cancellation);
			cancellation.ThrowIfCancellationRequested();
			if (book is null)
			{
				_linesFailed.Add(language);
				return;
			}

			IReadOnlyList<RecordPiece> lines = await Task.Run(() => RecordBooks.AllLines(story, book, language), cancellation);
			cancellation.ThrowIfCancellationRequested();
			_books[language] = book;
			_lines[language] = lines;
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			// no-fallback: the page went away or the story changed; the half-made result is dropped
		}
		catch (Exception ex)
		{
			// no-fallback: that language shows a tap-to-retry hint instead of a count; nothing is lost
			Debug.WriteLine($"Dino Reader could not count the lines in {language} for the record page: {ex.Message}");
			if (!cancellation.IsCancellationRequested) // a translator that throws something else when cancelled must not mark the new story's language failed
			{
				_linesFailed.Add(language);
			}
		}
	}

	// ---- the story language to record in (Slice 286) ----

	// "Record in" and one card per story language (the app language first), frames 192 and 193: two languages sit in two columns, three in three narrower ones, so a name never wraps.
	private View LanguageRows(Story story)
	{
		IReadOnlyList<string> languages = StoryLanguages.Current().All;
		var list = new VerticalStackLayout { Spacing = 8 };
		list.Add(SmallHeading(AppResources.Record_Language));
		var cards = new Grid { ColumnSpacing = CardGap };
		for (int i = 0; i < languages.Count; i++)
		{
			cards.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			cards.Add(LanguageCard(story, languages[i]), i);
		}

		list.Add(cards);
		return list;
	}

	// The 12.5 px muted heading above a block ("Record in", "Who will you record?"), frames 192 to 193.
	private static Label SmallHeading(string text)
	{
		var heading = new Label { Text = text, FontSize = 12.5, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Muted), Margin = new Thickness(4, 0, 4, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
		return heading;
	}

	private View LanguageCard(Story story, string language)
	{
		bool selected = language == _language;
		var name = new Label
		{
			Text = LanguageNames.NativeName(language), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1,
		};
		OwnScript.InOwnScript(name);
		name.HorizontalOptions = LayoutOptions.Start; // frame 193: a Farsi name sits left, next to the flag, like English; only its own words read right to left
		string progress = LanguageProgressText(story, language);
		var count = new Label
		{
			Text = progress, FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1,
			TextColor = DinoTokens.Get(StoryRecordings.Count(story, language) == 0 || progress == AppResources.Record_LanguageRetry ? DinoTokens.Muted : DinoTokens.Body),
		};
		count.HorizontalOptions = LayoutOptions.Start;
		var texts = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, FlowDirection = FlowDirection.LeftToRight, Children = { name, count } };
		var content = new Grid { FlowDirection = FlowDirection.LeftToRight, ColumnSpacing = CardFlagGap, Padding = new Thickness(10, 0), ColumnDefinitions = [new ColumnDefinition(CardFlagSize), new ColumnDefinition(GridLength.Star)] };
		content.Add(new FlagRing(language, CardFlagSize, 1, Color.FromRgba(43, 37, 64, FlagRingAlpha)), 0);
		content.Add(texts, 1);
		var card = new Border
		{
			AutomationId = LanguageIdPrefix + language, HeightRequest = CardHeight, BackgroundColor = Colors.White, StrokeThickness = SelectedRing, Content = content,
			Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = ModeRadius },
		};
		SemanticProperties.SetDescription(card, $"{LanguageNames.NativeName(language)}, {progress}");
		Taps.AddTap(card, () => ChooseLanguageAsync(language));
		return card;
	}

	// "12 of 44" in the unit of that language's own way of recording (lines for one character at a time, pages for whole pages), no extra word; "Nothing yet" when none.
	private string LanguageProgressText(Story story, string language)
	{
		if (StoryRecordings.Count(story, language) == 0)
		{
			return AppResources.Record_LanguageNone;
		}

		RecordMode mode = RecordChoices.ModeOf(story, language); // the same source the book sheet uses, so card and sheet agree
		if (LanguageProgress(story, language, mode) is { } progress)
		{
			return AppResources.Format(AppResources.Record_LanguagePages, progress.Done, progress.Total);
		}

		if (_linesFailed.Contains(language))
		{
			if (mode == RecordMode.WholePage && _source is { } source)
			{
				RecordProgress pages = RecordSplitWalk.Progress(story, source, RecordMode.WholePage, language); // its words could not be made: the pages recorded, which every language shares
				return AppResources.Format(AppResources.Record_LanguagePages, pages.Done, pages.Total);
			}

			return AppResources.Record_LanguageRetry; // per-line lines cannot be counted without its words: a tap tries again, never a wrong "0 of N"
		}

		return CutPending(language) ? PendingCount : string.Empty; // still being cut: a quiet mark after 500 ms, no number rather than a wrong one
	}

	// The count of <language> in <mode> from that language's own pieces (pages are the same in every language); null while its lines are not cut yet.
	private RecordProgress? LanguageProgress(Story story, string language, RecordMode mode)
	{
		if (_books.GetValueOrDefault(language) is not { } book || (mode == RecordMode.PerCharacter && !_lines.ContainsKey(language)))
		{
			return null;
		}

		return RecordBooks.Progress(story, book, _lines.GetValueOrDefault(language) ?? [], mode, language);
	}

	// The picked language drives everything after it: the words, the pieces, the recordings. Its book is worked out under the wait overlay when that takes over 500 ms (Cancel stops it).
	// When the words cannot be made the page stays in the language it had and says so with Retry.
	private Task ChooseLanguageAsync(string language) => _busy.RunAsync(async () =>
	{
		if (_story is not { } story)
		{
			return;
		}

		if (_linesFailed.Remove(language))
		{
			Render(); // a tap on a language whose words failed tries them again
		}

		if (language == _language && _book is not null)
		{
			return;
		}

		RecordBook? book = await TryBookAsync(story, language);
		if (book is not null)
		{
			_book = book;
			_language = language;
			_choice = RecordChoices.Load(story, book, language);
			_cut = null;
			_showChange = false;
		}

		Render();
	});

	// The page when no book could be made for the language it opened in: the language rows and the unavailable line with Retry.
	private void RenderUnavailable(Story story)
	{
		_header.Title = AppResources.Record_YourVoice;
		_body.Children.Clear();
		_bottom.Children.Clear();
		_bottom.ColumnDefinitions = [];
		if (StoryLanguages.Current().HasChoice)
		{
			_body.Add(LanguageRows(story));
		}

		_body.Add(UnavailableBlock(_unavailable ?? _language));
	}

	// "<language>: could not get the words." and a Retry button that works the words out again.
	private View UnavailableBlock(string language)
	{
		var block = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 8) };
		block.Add(new Label { AutomationId = UnavailableId, Text = AppResources.Format(AppResources.Record_LanguageUnavailable, LanguageNames.NativeName(language)), FontSize = 15, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Ink) });
		block.Add(OutlinedButton(AppResources.StoryLanguages_Retry, RetryId, true, () => ChooseLanguageAsync(language)));
		return block;
	}

	private void AddNotesAndModes()
	{
		var notes = new VerticalStackLayout { Spacing = NoteSpacing };
		notes.Add(Note(IconKind.ReadAloud, AppResources.Record_NoteRead));
		notes.Add(Note(IconKind.SkipNext, AppResources.Record_NoteSkip));
		notes.Add(Note(IconKind.Phone, DeviceInfo.Idiom == DeviceIdiom.Phone ? AppResources.Record_NoteStays : AppResources.Record_NoteStaysDevice));
		_body.Add(WhiteCard(notes, BlockTopExtra));
		var modes = new Grid { ColumnSpacing = BlockSpacing, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		modes.Add(ModeCard(ModeCharacterId, AppResources.Record_ModeCharacter, RecordMode.PerCharacter), 0);
		modes.Add(ModeCard(ModePageId, AppResources.Record_ModePage, RecordMode.WholePage), 1);
		_body.Add(modes);
	}

	private static View Note(IconKind icon, string text)
	{
		var row = new Grid { ColumnSpacing = NoteGap, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		row.Add(new IconView { Kind = icon, Size = NoteIconSize, StrokeWidth = 2.2, Color = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 1, 0, 0) }, 0);
		row.Add(new Label { Text = text, FontSize = 13.5, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center }, 1);
		return row;
	}

	// A white card, radius 16, padding 12 14 (frames 192 and 193); topExtra is the mock's extra margin above it.
	private static Border WhiteCard(View content, double topExtra = 0) => new()
	{
		BackgroundColor = Colors.White, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = ModeRadius }, Padding = new Thickness(BlockPaddingSide, BlockPaddingVertical),
		Margin = new Thickness(0, topExtra, 0, 0), Content = content,
	};

	// Frame 193: the mode icon top left and the radio top right, then the label (14 px, 600) under them.
	private Border ModeCard(string id, string text, RecordMode mode)
	{
		bool selected = _choice.Mode == mode;
		var icon = new IconView
		{
			Kind = mode == RecordMode.PerCharacter ? IconKind.Speech : IconKind.Page, Size = ModeIconSize, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
			Color = DinoTokens.Get(selected ? DinoTokens.Brand : DinoTokens.Body),
		};
		var top = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		top.Add(icon, 0);
		top.Add(Radio(selected), 1);
		var content = new VerticalStackLayout { Spacing = ModeSpacing, Padding = new Thickness(12, 10), VerticalOptions = LayoutOptions.Center };
		content.Add(top);
		content.Add(new Label { Text = text, FontSize = 14, FontAttributes = FontAttributes.Bold, LineHeight = 1.2, TextColor = DinoTokens.Get(DinoTokens.Ink) });
		var card = new Border
		{
			AutomationId = id, BackgroundColor = Colors.White, Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent, StrokeThickness = SelectedRing,
			StrokeShape = new RoundRectangle { CornerRadius = ModeRadius }, Content = content,
		};
		SemanticProperties.SetDescription(card, text);
		Taps.AddTap(card, () => ChooseModeAsync(mode));
		return card;
	}

	private static View Radio(bool selected)
	{
		var ring = new Border
		{
			WidthRequest = RadioSize, HeightRequest = RadioSize, StrokeThickness = 2, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
			Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.CheckEmpty), StrokeShape = new RoundRectangle { CornerRadius = RadioSize / 2 },
		};
		if (selected)
		{
			ring.Content = new Border
			{
				WidthRequest = RadioSize / 2, HeightRequest = RadioSize / 2, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Brand),
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = RadioSize / 4 },
			};
		}

		return ring;
	}

	private Task ChooseModeAsync(RecordMode mode)
	{
		if (_choice.Mode != mode && _story is not null)
		{
			_choice = _choice with { Mode = mode };
			RecordChoices.Save(_story.Id, _language, _choice);
			Render();
		}

		return Task.CompletedTask;
	}

	private static View LongPagesLine() => new Label
	{
		Text = AppResources.Record_LongPages, FontSize = 14, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(0, 6, 0, 0),
	};

	// ---- the checklist (frames 178 and 189) ----

	private View Checklist(Story story, RecordBook book)
	{
		var list = new VerticalStackLayout { Spacing = BlockSpacing };
		var heading = SmallHeading(AppResources.Record_Who);
		heading.Margin = new Thickness(4, 2, 4, 0);
		list.Add(heading);
		var rows = new VerticalStackLayout { Spacing = 0 };
		// the picked language's own lines per speaker once they are cut; until then the rows show their names with no count (never another language's)
		IReadOnlyList<RecordPiece>? cut = _lines.GetValueOrDefault(_language);
		IReadOnlyDictionary<string, int> counts = cut is null ? RecordSplit.LineCounts(book.Sentences) : cut.GroupBy(p => p.Speaker).ToDictionary(g => g.Key, g => g.Count());
		bool has = StoryRecordings.Count(story, _language) > 0;
		foreach (RecordSpeaker speaker in book.Speakers.Where(s => counts.ContainsKey(s.Id)))
		{
			if (rows.Children.Count > 0)
			{
				rows.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb(DividerColor) });
			}

			rows.Add(SpeakerRow(story, book, speaker, cut is null ? null : counts[speaker.Id], has));
		}

		list.Add(new Border { BackgroundColor = Colors.White, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = ModeRadius }, Content = rows });
		return list;
	}

	private View SpeakerRow(Story story, RecordBook book, RecordSpeaker speaker, int? lines, bool has)
	{
		bool ticked = _choice.Ticked.Contains(speaker.Id);
		var row = new Grid { HeightRequest = RowHeight, ColumnSpacing = 12, Padding = new Thickness(BlockPaddingSide, 0), BackgroundColor = Colors.White, ColumnDefinitions = [new ColumnDefinition(BoxSize), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(Box(ticked), 0);
		var name = new Label { Text = RecordCard.NameOf(speaker), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(ticked ? DinoTokens.Ink : DinoTokens.Body), VerticalOptions = LayoutOptions.Center };
		OwnScript.InOwnScript(name);
		row.Add(name, 1);
		row.Add(CountLabel(story, book, speaker, lines, has), 2);
		row.AutomationId = SpeakerIdPrefix + (speaker.Id.Length == 0 ? "storyteller" : speaker.Id);
		SemanticProperties.SetDescription(row, RecordCard.NameOf(speaker));
		Taps.AddTap(row, () => ToggleAsync(speaker.Id));
		return row;
	}

	private static View Box(bool ticked)
	{
		var box = new Border
		{
			WidthRequest = BoxSize, HeightRequest = BoxSize, StrokeThickness = ticked ? 0 : 2, VerticalOptions = LayoutOptions.Center,
			BackgroundColor = ticked ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent, Stroke = DinoTokens.Get(DinoTokens.CheckEmpty),
			StrokeShape = new RoundRectangle { CornerRadius = BoxCorner },
		};
		if (ticked)
		{
			box.Content = new IconView { Kind = IconKind.Check, Size = 15, StrokeWidth = 3, Color = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		}

		return box;
	}

	// "24 lines" for a book with nothing recorded; with recordings the speaker's own progress ("0 of 24", green "9 of 9" when done).
	private View CountLabel(Story story, RecordBook book, RecordSpeaker speaker, int? lines, bool has)
	{
		if (lines is not { } count)
		{
			return new Label { Text = CutPending(_language) ? PendingCount : string.Empty, FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center }; // still being cut
		}

		if (!has)
		{
			return new Label { Text = AppResources.Format(AppResources.Record_Lines, count), FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center };
		}

		RecordProgress progress = RecordBooks.LinesProgress(story, _lines[_language], speaker.Id);
		bool done = progress.Total > 0 && progress.Done == progress.Total;
		return new Label
		{
			Text = AppResources.Format(AppResources.Record_OfCount, progress.Done, progress.Total), FontSize = 12.5, FontAttributes = done ? FontAttributes.Bold : FontAttributes.None,
			TextColor = done ? Color.FromArgb(DoneGreen) : DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center,
		};
	}

	private Task ToggleAsync(string speaker)
	{
		if (_story is null)
		{
			return Task.CompletedTask;
		}

		HashSet<string> ticked = [.. _choice.Ticked];
		if (!ticked.Remove(speaker))
		{
			ticked.Add(speaker);
		}

		_choice = _choice with { Ticked = ticked };
		RecordChoices.Save(_story.Id, _language, _choice);
		Render();
		return Task.CompletedTask;
	}

	// ---- the progress block of a book with recordings (frame 189) ----

	private View ProgressBlock(Story story, RecordBook book)
	{
		RecordProgress? ready = LanguageProgress(story, _language, _choice.Mode); // null while this language's lines are being cut: no number yet
		RecordProgress progress = ready ?? new RecordProgress(0, 0, _choice.Mode == RecordMode.PerCharacter ? RecordUnit.Lines : RecordUnit.Pages);
		string text = ready is null ? (CutPending(_language) ? PendingCount : string.Empty)
			: progress.Unit == RecordUnit.Lines
				? AppResources.Format(AppResources.Record_LinesRecorded, progress.Done, progress.Total)
				: AppResources.Format(AppResources.Record_PagesRecorded, progress.Done, progress.Total);
		var top = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		top.Add(new Label { AutomationId = ProgressId, Text = text, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center }, 0);
		top.Add(new Label
		{
			Text = _choice.Mode == RecordMode.PerCharacter ? AppResources.Record_ModeCharacter : AppResources.Record_ModePage, FontSize = 12.5,
			TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center,
		}, 1);
		var block = new VerticalStackLayout { Spacing = BlockSpacing };
		block.Add(top);
		block.Add(Bar(progress));
		block.Add(new Label { Text = DeviceInfo.Idiom == DeviceIdiom.Phone ? AppResources.Record_RestByDino : AppResources.Record_RestByDinoDevice, FontSize = 12.5, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body) });
		return WhiteCard(block, BlockTopExtra); // frame 192: the progress sits in a white card
	}

	private static View Bar(RecordProgress progress)
	{
		var bar = new Grid { HeightRequest = BarHeight, ColumnSpacing = 0 };
		double share = progress.Total == 0 ? 0 : (double)progress.Done / progress.Total;
		bar.Add(new Border { StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeShape = new RoundRectangle { CornerRadius = BarHeight / 2 } });
		var fill = new Border
		{
			StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Danger), HorizontalOptions = LayoutOptions.Start, StrokeShape = new RoundRectangle { CornerRadius = BarHeight / 2 },
		};
		bar.SizeChanged += (_, _) => fill.WidthRequest = bar.Width * share;
		bar.Add(fill);
		return bar;
	}

	private View ChangeLink()
	{
		var link = new Label
		{
			AutomationId = ChangeId, Text = AppResources.Record_Change, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
			HeightRequest = ChangeHeight, VerticalTextAlignment = TextAlignment.Center, HorizontalOptions = LayoutOptions.Start,
		};
		SemanticProperties.SetDescription(link, AppResources.Record_Change);
		Taps.AddTap(link, () =>
		{
			_showChange = !_showChange;
			Render();
			return Task.CompletedTask;
		});
		return link;
	}

	// ---- the buttons ----

	private void AddButtons(Story story, RecordBook book, bool has)
	{
		bool canStart = _choice.Mode == RecordMode.WholePage || _choice.Ticked.Count > 0;
		if (!has)
		{
			_bottom.Add(StartButton(AppResources.Record_Start, StartId, canStart, () => StartAsync(story, book, resume: false)), 0, 0);
			return;
		}

		_bottom.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
		bool canListen = canStart && RecordSplitWalk.Recorded(story, book, _choice.Mode, _choice.Ticked, _language).Count > 0; // nothing recorded in this scope: Listen would do nothing
		_bottom.Add(OutlinedButton(AppResources.Record_Listen, ListenId, canListen, () => ListenAsync(story, book)), 0, 0);
		bool allDone = canStart && CachedPieces() is { Count: > 0 } cut && RecordSplitWalk.FirstUnrecorded(story, cut) < 0;
		_bottom.Add(StartButton(allDone ? AppResources.Record_Again : AppResources.Record_Rest, RestId, canStart, () => StartAsync(story, book, resume: !allDone), OutlinedLabelSize), 1, 0);
		if (canStart && CachedPieces() is null)
		{
			_ = WarmPiecesAsync(story, book); // the whole book is measured in the background; the button reads "Record again" when it is done, if it is
		}
	}

	// ---- the pieces, cut off the UI thread ----

	private string CutKey() => $"{_language}|{_choice.Mode}|{string.Join(',', _choice.Ticked.Order(StringComparer.Ordinal))}";

	private IReadOnlyList<RecordPiece>? CachedPieces() => _cut is { } cut && cut.Key == CutKey() ? cut.Pieces : null;

	// Measuring a long book takes a while, so it runs on a worker thread; the result is kept for the mode and ticks it was made for.
	private async Task<IReadOnlyList<RecordPiece>> PiecesAsync(Story story, RecordBook book)
	{
		if (CachedPieces() is { } cached)
		{
			return cached;
		}

		string key = CutKey();
		RecordChoice choice = _choice;
		string language = _language;
		IReadOnlyList<RecordPiece> pieces = await Task.Run(() => RecordCard.Pieces(story, book, choice, language));
		_cut = (key, pieces);
		return pieces;
	}

	private async Task WarmPiecesAsync(Story story, RecordBook book)
	{
		if (_warming)
		{
			return;
		}

		_warming = true;
		try
		{
			await PiecesAsync(story, book);
		}
		catch (Exception ex)
		{
			// no-fallback: the button keeps reading "Record the rest"; Start measures again under the wait overlay
			Debug.WriteLine($"Dino Reader could not cut the book for the record page: {ex.Message}");
			return;
		}
		finally
		{
			_warming = false;
		}

		Render(); // shows the finished count; if the choice changed meanwhile, this starts the cut for the new one
	}

	private async Task<IReadOnlyList<RecordPiece>> PiecesUnderWaitAsync(Story story, RecordBook book)
	{
		_wait.Begin(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: false);
		try
		{
			return await PiecesAsync(story, book);
		}
		finally
		{
			_wait.End();
		}
	}


	private static Border StartButton(string text, string id, bool enabled, Func<Task> tapped, double labelSize = StartLabelSize)
	{
		var edge = DinoTokens.Get(DinoTokens.CoralEdge);
		var button = new Border
		{
			AutomationId = id, MinimumHeightRequest = ButtonHeight, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Coral), Opacity = enabled ? 1 : 0.4, IsEnabled = enabled,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			Shadow = new Shadow { Brush = new SolidColorBrush(edge), Offset = new Point(0, 5), Radius = 0, Opacity = 1f },
			Content = new Label { Text = text, FontSize = labelSize, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(button, text);
		Taps.AddTap(button, () => enabled ? tapped() : Task.CompletedTask);
		DesktopTips.Hover(button);
		return button;
	}

	private static Border OutlinedButton(string text, string id, bool enabled, Func<Task> tapped)
	{
		var label = new Label { Text = text, FontSize = OutlinedLabelSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center, MaxLines = 2 };
		var icon = new IconView { Kind = IconKind.Play, Filled = true, Size = PlayIconSize, Color = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Center };
		// Frame 192: the play icon, then the words, as one centred group in the half-width pill. The words keep the design's size and never clip: a narrow phone or a large font wraps them to a second line and the pill grows.
		var group = new Grid { ColumnSpacing = PlayIconGap, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		group.Add(icon, 0);
		group.Add(label, 1);
		var button = new Border
		{
			AutomationId = id, MinimumHeightRequest = ButtonHeight, StrokeThickness = OutlinedStroke, Stroke = Color.FromArgb(OutlinedStrokeColor), BackgroundColor = Colors.White, Opacity = enabled ? 1 : 0.4, IsEnabled = enabled,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			Padding = new Thickness(OutlinedPadding, OutlinedPaddingVertical),
			Content = group,
		};
		SemanticProperties.SetDescription(button, text);
		Taps.AddTap(button, () => enabled ? tapped() : Task.CompletedTask);
		DesktopTips.Hover(button);
		return button;
	}

	// ---- going on ----

	private Task StartAsync(Story story, RecordBook book, bool resume) => _busy.RunAsync(async () =>
	{
		RecordChoices.Save(story.Id, _language, _choice);
		TraceWindows($"Record intro: Start tapped mode={_choice.Mode} ticked={_choice.Ticked.Count} resume={resume}");
		IReadOnlyList<RecordPiece> pieces = await PiecesUnderWaitAsync(story, book);
		TraceWindows($"Record intro: pieces cut count={pieces.Count}");
		if (pieces.Count == 0)
		{
			await DisplayAlertAsync(AppResources.Record_Title, AppResources.Reading_ReadFailed, AppResources.Common_Ok); // nothing to record: say so instead of a silent tap
			return;
		}

		int start = resume ? Math.Max(0, RecordSplitWalk.FirstUnrecorded(story, pieces)) : 0;
		await OpenRecordPageAsync(new RecordSetup(story, book, _choice, pieces, Listening: false, _language), start);
	});

	// Release-visible on the Windows VM (window-trace.log); nothing elsewhere.
	[Conditional("WINDOWS")]
	private static void TraceWindows(string line)
	{
#if WINDOWS
		WindowTrace.Write(line);
#endif
	}

	private Task ListenAsync(Story story, RecordBook book) => _busy.RunAsync(async () =>
	{
		RecordChoices.Save(story.Id, _language, _choice);
		IReadOnlyList<RecordPiece> pieces = RecordSplitWalk.Recorded(story, book, _choice.Mode, _choice.Ticked, _language);
		if (pieces.Count > 0)
		{
			await OpenRecordPageAsync(new RecordSetup(story, book, _choice, pieces, Listening: true, _language), 0);
		}
	});

	private async Task OpenRecordPageAsync(RecordSetup setup, int start)
	{
		if (!Navigation.NavigationStack.Contains(this))
		{
			return; // the intro was left while the book was being cut (system back, Esc): never push the Record page onto a page that is gone
		}

		var record = _services.GetRequiredService<RecordPage>();
		record.LaunchedFromIntro = LaunchedFromIntro;
		record.Load(setup, start);
		await Navigation.PushAsync(record);
		_record = record; // only once pushed: until then there is nothing to wait for on return
	}
}
