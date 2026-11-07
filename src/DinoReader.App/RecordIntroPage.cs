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
/// and the characters to record. A book that already has recordings leads with how much is recorded and offers "Listen and change" and "Record the rest". The choice is remembered per book.
/// None of this text is repeated on the recording pages.
/// </summary>
public sealed class RecordIntroPage : ContentPage, IGrownUpPage
{
	private const double ColumnMaxWidth = 560;
	private const double SideMargin = 20;
	private const double RowHeight = 44;
	private const double BoxSize = 24;
	private const double BoxCorner = 7;
	private const double RadioSize = 20;
	private const double ModeRadius = 16;
	private const double ButtonHeight = 52;
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
	private readonly VerticalStackLayout _body = new() { Spacing = 14 };
	private readonly Grid _bottom = new() { ColumnSpacing = 10, Padding = new Thickness(SideMargin, 8, SideMargin, 16) };
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
		_header.Title = AppResources.Record_Title;
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
	// speakers named as that language's cast names them. Both share the story's sentence numbers, so a clip's key means the same sentences in every language. Worked out once per language;
	// null when the translation could not be made.
	private async Task<RecordBook?> BookInAsync(Story story, string language, CancellationToken cancellation)
	{
		if (_books.TryGetValue(language, out RecordBook? cached))
		{
			return cached;
		}

		_source ??= await Task.Run(() => RecordSplitBook.Of(story), cancellation);
		RecordBook? book = _source;
		if (_services.GetRequiredService<StoryTranslator>().Applies(story, language))
		{
			TranslatedStory? translated = await _services.GetRequiredService<StoryVoicing>().TranslatedDisplayAsync(story, RecordSplitBook.SentencesOf(story), language, cancellation);
			book = translated is null ? null : RecordSplitBook.WithTranslatedText(_source, translated);
			book = book is not null && story.HasCastIn(language) ? RecordSplitBook.WithSpeakerNames(book, story.CastIn(language)) : book;
		}

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
		await RefreshStoryAsync();
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

		_source = null;
		_books.Clear();
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
		var holder = new Grid { MaximumWidthRequest = ColumnMaxWidth + 2 * SideMargin, HorizontalOptions = LayoutOptions.Center, Children = { content } };
		return holder;
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
		_header.Title = has ? AppResources.Record_YourVoice : AppResources.Record_Title;
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
			_body.Add(ChangeLink());
		}

		if (!has || _showChange)
		{
			AddNotesAndModes();
		}

		_body.Add(_choice.Mode == RecordMode.PerCharacter ? Checklist(story, book) : LongPagesLine());
		AddButtons(story, book, has);
		Debug.WriteLine($"Dino Reader {PageName}: mode={_choice.Mode} ticked={_choice.Ticked.Count} recorded={StoryRecordings.Count(story, _language)} language={_language}");
	}

	// ---- the story language to record in (Slice 286) ----

	// One row per story language (the app language first): a radio, the name in its own script and how much of the book is recorded in it.
	private View LanguageRows(Story story)
	{
		var list = new VerticalStackLayout { Spacing = 0 };
		var heading = new Label { Text = AppResources.Record_Language, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), Margin = new Thickness(0, 6, 0, 6) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
		list.Add(heading);
		foreach (string language in StoryLanguages.Current().All)
		{
			list.Add(LanguageRow(story, language));
		}

		return list;
	}

	private View LanguageRow(Story story, string language)
	{
		var row = new Grid { HeightRequest = RowHeight, ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(RadioSize), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(Radio(language == _language), 0);
		var name = new Label { Text = LanguageNames.NativeName(language), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center };
		OwnScript.InOwnScript(name);
		row.Add(name, 1);
		row.Add(new Label { Text = LanguageProgressText(story, language), FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center }, 2);
		row.AutomationId = LanguageIdPrefix + language;
		SemanticProperties.SetDescription(row, LanguageNames.NativeName(language));
		Taps.AddTap(row, () => ChooseLanguageAsync(language));
		return row;
	}

	private static string LanguageProgressText(Story story, string language)
	{
		int pages = StoryRecordings.PagesRecorded(story, language);
		return pages == 0 ? AppResources.Record_LanguageNone : AppResources.Format(AppResources.Record_LanguagePages, pages, story.Pages.Count);
	}

	// The picked language drives everything after it: the words, the pieces, the recordings. Its book is worked out under the wait overlay when that takes over 500 ms (Cancel stops it).
	// When the words cannot be made the page stays in the language it had and says so with Retry.
	private Task ChooseLanguageAsync(string language) => _busy.RunAsync(async () =>
	{
		if (_story is not { } story || (language == _language && _book is not null))
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
		_header.Title = AppResources.Record_Title;
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
		_body.Add(Note(IconKind.Book, AppResources.Record_NoteRead));
		_body.Add(Note(IconKind.SkipForward, AppResources.Record_NoteSkip));
		_body.Add(Note(IconKind.Lock, DeviceInfo.Idiom == DeviceIdiom.Phone ? AppResources.Record_NoteStays : AppResources.Record_NoteStaysDevice));
		var modes = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		modes.Add(ModeCard(ModeCharacterId, AppResources.Record_ModeCharacter, RecordMode.PerCharacter), 0);
		modes.Add(ModeCard(ModePageId, AppResources.Record_ModePage, RecordMode.WholePage), 1);
		_body.Add(modes);
	}

	private static View Note(IconKind icon, string text)
	{
		var row = new HorizontalStackLayout { Spacing = 12 };
		row.Add(new IconView { Kind = icon, Size = 20, StrokeWidth = 2, Color = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 2, 0, 0) });
		row.Add(new Label { Text = text, FontSize = 15, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center, WidthRequest = ColumnMaxWidth - 60 });
		return row;
	}

	private Border ModeCard(string id, string text, RecordMode mode)
	{
		bool selected = _choice.Mode == mode;
		var content = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(14, 14), VerticalOptions = LayoutOptions.Center };
		content.Add(Radio(selected));
		content.Add(new Label { Text = text, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink) });
		var card = new Border
		{
			AutomationId = id, BackgroundColor = Colors.White, Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Hairline), StrokeThickness = selected ? 2.5 : 1,
			StrokeShape = new RoundRectangle { CornerRadius = ModeRadius }, MinimumHeightRequest = RowHeight + 40, Content = content,
		};
		SemanticProperties.SetDescription(card, text);
		Taps.AddTap(card, () => ChooseModeAsync(mode));
		return card;
	}

	private static View Radio(bool selected)
	{
		var ring = new Border
		{
			WidthRequest = RadioSize, HeightRequest = RadioSize, StrokeThickness = 2, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Start,
			Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Hairline), StrokeShape = new RoundRectangle { CornerRadius = RadioSize / 2 },
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
			RecordChoices.Save(_story.Id, _choice);
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
		var list = new VerticalStackLayout { Spacing = 0 };
		var heading = new Label { Text = AppResources.Record_Who, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), Margin = new Thickness(0, 6, 0, 6) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
		list.Add(heading);
		IReadOnlyDictionary<string, int> counts = RecordSplit.LineCounts(book.Sentences);
		bool has = StoryRecordings.Count(story, _language) > 0;
		foreach (RecordSpeaker speaker in book.Speakers.Where(s => counts.ContainsKey(s.Id)))
		{
			list.Add(SpeakerRow(story, book, speaker, counts[speaker.Id], has));
		}

		return list;
	}

	private View SpeakerRow(Story story, RecordBook book, RecordSpeaker speaker, int lines, bool has)
	{
		bool ticked = _choice.Ticked.Contains(speaker.Id);
		var row = new Grid { HeightRequest = RowHeight, ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(BoxSize), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(Box(ticked), 0);
		var name = new Label { Text = RecordCard.NameOf(speaker), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center };
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
	private View CountLabel(Story story, RecordBook book, RecordSpeaker speaker, int lines, bool has)
	{
		if (!has)
		{
			return new Label { Text = AppResources.Format(AppResources.Record_Lines, lines), FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center };
		}

		RecordProgress progress = RecordSplitWalk.SpeakerProgress(story, book, speaker.Id, _language);
		bool done = progress.Total > 0 && progress.Done == progress.Total;
		return new Label
		{
			Text = AppResources.Format(AppResources.Record_OfCount, progress.Done, progress.Total), FontSize = 12.5, FontAttributes = done ? FontAttributes.Bold : FontAttributes.None,
			TextColor = done ? Color.FromArgb("#2E7D32") : DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center,
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
		RecordChoices.Save(_story.Id, _choice);
		Render();
		return Task.CompletedTask;
	}

	// ---- the progress block of a book with recordings (frame 189) ----

	private View ProgressBlock(Story story, RecordBook book)
	{
		RecordProgress progress = RecordSplitWalk.Progress(story, book, _choice.Mode, _language);
		string text = progress.Unit == RecordUnit.Lines
			? AppResources.Format(AppResources.Record_LinesRecorded, progress.Done, progress.Total)
			: AppResources.Format(AppResources.Record_PagesRecorded, progress.Done, progress.Total);
		var top = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		top.Add(new Label { AutomationId = ProgressId, Text = text, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center }, 0);
		top.Add(new Label
		{
			Text = _choice.Mode == RecordMode.PerCharacter ? AppResources.Record_ModeCharacter : AppResources.Record_ModePage, FontSize = 12.5,
			TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center,
		}, 1);
		var block = new VerticalStackLayout { Spacing = 8 };
		block.Add(top);
		block.Add(Bar(progress));
		block.Add(new Label { Text = DeviceInfo.Idiom == DeviceIdiom.Phone ? AppResources.Record_RestByDino : AppResources.Record_RestByDinoDevice, FontSize = 13, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body) });
		return block;
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
		_bottom.Add(StartButton(allDone ? AppResources.Record_Again : AppResources.Record_Rest, RestId, canStart, () => StartAsync(story, book, resume: !allDone)), 1, 0);
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


	private static Border StartButton(string text, string id, bool enabled, Func<Task> tapped)
	{
		var edge = DinoTokens.Get(DinoTokens.CoralEdge);
		var button = new Border
		{
			AutomationId = id, HeightRequest = ButtonHeight, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Coral), Opacity = enabled ? 1 : 0.4, IsEnabled = enabled,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			Shadow = new Shadow { Brush = new SolidColorBrush(edge), Offset = new Point(0, 5), Radius = 0, Opacity = 1f },
			Content = new Label { Text = text, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(button, text);
		Taps.AddTap(button, () => enabled ? tapped() : Task.CompletedTask);
		DesktopTips.Hover(button);
		return button;
	}

	private static Border OutlinedButton(string text, string id, bool enabled, Func<Task> tapped)
	{
		var button = new Border
		{
			AutomationId = id, HeightRequest = ButtonHeight, StrokeThickness = 2, Stroke = DinoTokens.Get(DinoTokens.Brand), BackgroundColor = Colors.White, Opacity = enabled ? 1 : 0.4, IsEnabled = enabled,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			Content = new Label { Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 },
		};
		SemanticProperties.SetDescription(button, text);
		Taps.AddTap(button, () => enabled ? tapped() : Task.CompletedTask);
		DesktopTips.Hover(button);
		return button;
	}

	// ---- going on ----

	private Task StartAsync(Story story, RecordBook book, bool resume) => _busy.RunAsync(async () =>
	{
		RecordChoices.Save(story.Id, _choice);
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
		RecordChoices.Save(story.Id, _choice);
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
