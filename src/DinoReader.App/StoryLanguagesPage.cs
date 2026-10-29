using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The list of languages a parent ticks story languages from (Slice 173, mock frames 86 and 87): the app language first, ticked and locked, then the other
/// fourteen, each a tickable row. Two ticked fills the set and greys the rest. With <see cref="StoryLanguagePacks"/> a ticked language starts its voice
/// download and shows its progress with Cancel (Settings); without, it only holds the picks (the intro, which starts the downloads on Done).
/// </summary>
public sealed class StoryLanguagePicker : ContentView
{
	private const double RowMinHeight = 54;
	private const double CheckSize = 26;
	private const double CheckCorner = 8;
	private const double BarHeight = 8;
	private const double NameSize = 15;
	private const double NoteSize = 12.5;
	private const double CardCorner = 18;
	private const double DisabledOpacity = 0.45;
	private const int PercentScale = 100;

	private static readonly Color ReadyGreen = Color.FromArgb("#3E9A4E");
	private static readonly Color LockedGrey = Color.FromArgb("#C9C3D6");

	private readonly StoryLanguagePacks? _packs;
	private readonly Dictionary<string, (ProgressBar Bar, Label Status)> _live = [];
	private readonly Dictionary<string, LanguageVoiceState> _shown = [];

	public StoryLanguagePicker(StoryLanguageSet initial, StoryLanguagePacks? packs)
	{
		Set = initial;
		_packs = packs;
		Rebuild();
	}

	/// <summary>The set as ticked now.</summary>
	public StoryLanguageSet Set { get; private set; }

	/// <summary>Raised after a tick or an untick with the language and whether it is now ticked.</summary>
	public event Action<string, bool>? Toggled;

	/// <summary>Raised when a language's voice should be fetched: it was just ticked, or its "Download" was tapped. The owner asks about mobile data and starts it.</summary>
	public event Action<string>? DownloadRequested;

	/// <summary>Moves a running download's bar without rebuilding; true when nothing but progress changed. False means the rows must be rebuilt.</summary>
	public bool TryUpdateProgress()
	{
		if (_packs is null || _shown.Any(shown => _packs.StateOf(shown.Key) != shown.Value))
		{
			return false;
		}

		foreach ((string code, (ProgressBar bar, Label status)) in _live)
		{
			double progress = _packs.ProgressOf(code);
			bar.Progress = progress;
			status.Text = AppResources.Format(AppResources.StoryLanguages_Getting, (int)(progress * PercentScale));
		}

		return true;
	}

	/// <summary>Unticks the given languages (a failed download unticked them behind this picker's back) and shows it.</summary>
	public void Untick(IEnumerable<string> languages)
	{
		StoryLanguageSet before = Set;
		Set = languages.Aggregate(Set, (set, code) => set.WithoutSide(code));
		if (!Set.Sides.SequenceEqual(before.Sides))
		{
			Rebuild();
		}
	}

	/// <summary>Builds the rows again from the current ticks and download states.</summary>
	public void Rebuild()
	{
		if (_packs is not null)
		{
			Set = StoryLanguages.Current(); // Settings saves every tick at once; a failed download may have unticked a language behind the page's back
		}

		_live.Clear();
		_shown.Clear();
		var rows = new VerticalStackLayout();
		foreach ((string code, _) in LanguageNames.All.OrderBy(l => l.Code == Set.Main ? 0 : 1))
		{
			if (rows.Children.Count > 0)
			{
				rows.Children.Add(SettingsRows.Divider());
			}

			rows.Children.Add(Row(UiLanguage.Resolve(code)));
		}

		Content = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardCorner },
			Content = rows,
		};
	}

	private View Row(string code)
	{
		bool main = code == Set.Main;
		bool ticked = Set.Contains(code);
		bool disabled = !ticked && Set.IsFull;
		var grid = new Grid
		{
			MinimumHeightRequest = RowMinHeight,
			Padding = new Thickness(14, 8),
			ColumnSpacing = 12,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			Opacity = disabled ? DisabledOpacity : 1,
		};
		grid.Add(Check(ticked, main), 0);
		grid.Add(NameAndProgress(code, ticked && !main), 1);
		View? end = EndView(code, main, ticked);
		if (end is not null)
		{
			grid.Add(end, 2);
		}

		string name = LanguageNames.NativeName(code);
		SemanticProperties.SetDescription(grid, main ? CastText.Pair(name, AppResources.StoryLanguages_AppLanguage) : name);
		if (!main && !disabled)
		{
			CastViews.AddTap(grid, () => ToggleAsync(code));
		}

		return grid;
	}

	private static Border Check(bool ticked, bool main)
	{
		var box = new Border
		{
			WidthRequest = CheckSize,
			HeightRequest = CheckSize,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = CheckCorner },
			BackgroundColor = main ? LockedGrey : ticked ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent,
			Stroke = DinoTokens.Get(DinoTokens.CheckEmpty),
			StrokeThickness = ticked ? 0 : 2,
		};
		if (ticked)
		{
			box.Content = new IconView { Kind = IconKind.Check, Color = Colors.White, Size = 14, StrokeWidth = 3, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		}

		return box;
	}

	private View NameAndProgress(string code, bool sideTicked)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var stack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		stack.Children.Add(new Label
		{
			Text = LanguageNames.NativeName(code),
			FontSize = NameSize,
			FontAttributes = code == Set.Main || sideTicked ? FontAttributes.Bold : FontAttributes.None,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		});
		if (_packs is not null && sideTicked)
		{
			_shown[code] = _packs.StateOf(code);
			if (_shown[code] == LanguageVoiceState.Downloading)
			{
				AddProgress(stack, code);
			}
			else if (_shown[code] == LanguageVoiceState.NotInstalled && _packs.IsWaitingForInternet(code))
			{
				AddWaiting(stack, code);
			}
		}

		return stack;
	}

	private void AddProgress(VerticalStackLayout stack, string code)
	{
		double progress = _packs!.ProgressOf(code);
		var bar = new ProgressBar { Progress = progress, ProgressColor = DinoTokens.Get(DinoTokens.Brand), HeightRequest = BarHeight };
		var status = new Label
		{
			Text = AppResources.Format(AppResources.StoryLanguages_Getting, (int)(progress * PercentScale)),
			FontSize = 12,
			TextColor = DinoTokens.Get(DinoTokens.Muted),
		};
		_live[code] = (bar, status);
		stack.Children.Add(bar);
		stack.Children.Add(status);
	}

	// Mock frame 95: the bar keeps its share but goes grey, the line says what it waits for, a note says it carries on by itself, and Retry tries at once.
	private void AddWaiting(VerticalStackLayout stack, string code)
	{
		double progress = _packs!.ProgressOf(code);
		int percent = (int)(progress * PercentScale);
		string template = _packs.WaitingNeedsWifi(code) ? AppResources.StoryLanguages_WaitingWifi : AppResources.StoryLanguages_Waiting;
		stack.Children.Add(new ProgressBar { Progress = progress, ProgressColor = DinoTokens.Get(DinoTokens.Muted), HeightRequest = BarHeight });
		bool needsRetry = _packs.NeedsRetry(code); // the automatic retry failed too: no "carries on by itself", only Retry
		string line = needsRetry ? AppResources.StoryLanguages_Failed : AppResources.Format(template, percent);
		stack.Children.Add(new Label { Text = line, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) });
		if (!needsRetry)
		{
			stack.Children.Add(new Label { Text = AppResources.StoryLanguages_WaitingNote, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) });
		}

		Label retry = Note(AppResources.StoryLanguages_Retry, DinoTokens.Get(DinoTokens.Brand), bold: true);
		CastViews.AddTap(retry, () =>
		{
			DownloadRequested?.Invoke(code);
			return Task.CompletedTask;
		});
		stack.Children.Add(retry);
	}

	private View? EndView(string code, bool main, bool ticked)
	{
		if (main)
		{
			return Note(AppResources.StoryLanguages_AppLanguage, DinoTokens.Get(DinoTokens.Muted));
		}

		if (ShowsRetry(code, ticked))
		{
			return RetryNote(code, retick: !StoryLanguages.IsWanted(code));
		}

		if (_packs is null || !ticked)
		{
			return null;
		}

		return _packs.StateOf(code) switch
		{
			LanguageVoiceState.Ready => Note(AppResources.Language_Ready, ReadyGreen, bold: true),
			LanguageVoiceState.Downloading => CancelButton(code),
			_ when _packs.IsWaitingForInternet(code) => CancelButton(code), // frame 95: Cancel still unticks the language
			_ => RetryNote(code, retick: false),
		};
	}

	private static Label Note(string text, Color colour, bool bold = false) => new()
	{
		Text = text,
		FontSize = NoteSize,
		FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
		TextColor = colour,
		VerticalOptions = LayoutOptions.Center,
	};

	// A book in this language was read with the phone's voice (its pack is missing), or the last download failed and unticked it. Never on a full set: the tap would have nothing to tick.
	private bool ShowsRetry(string code, bool ticked)
	{
		if (_packs is null || ticked || _packs.StateOf(code) != LanguageVoiceState.NotInstalled)
		{
			return false;
		}

		return StoryLanguages.IsWanted(code) || (_packs.HasFailed(code) && !Set.IsFull);
	}

	private Label RetryNote(string code, bool retick)
	{
		string text = _packs!.HasFailed(code) ? AppResources.StoryLanguages_Failed : _packs.IsWaitingForWifi(code) ? AppResources.StoryLanguages_WifiWaiting : AppResources.Language_Download;
		Label note = Note(text, DinoTokens.Get(DinoTokens.Brand), bold: true);
		CastViews.AddTap(note, () => retick && !Set.Contains(code) ? ToggleAsync(code) : RequestDownload(code)); // a failed, unticked row's Retry ticks it again; the phone-voice "Download" fetches without ticking
		return note;
	}

	private Task RequestDownload(string code)
	{
		DownloadRequested?.Invoke(code);
		return Task.CompletedTask;
	}

	private Border CancelButton(string code)
	{
		var cancel = new Border
		{
			HeightRequest = 36,
			Padding = new Thickness(14, 0),
			StrokeShape = new RoundRectangle { CornerRadius = 18 },
			BackgroundColor = Colors.White,
			Stroke = DinoTokens.Get(DinoTokens.Hairline),
			StrokeThickness = 2,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label { Text = AppResources.Common_Cancel, FontSize = 13.5, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(cancel, AppResources.Common_Cancel);
		CastViews.AddTap(cancel, () => ToggleAsync(code)); // Cancel unticks (mock 87)
		return cancel;
	}

	private Task ToggleAsync(string code)
	{
		if (Set.Contains(code))
		{
			Set = Set.WithoutSide(code);
			Toggled?.Invoke(code, false); // save the untick first: Cancel raises Changed, and the page re-syncs from the saved set
			_packs?.Cancel(code);
		}
		else if (Set.CanAdd(code))
		{
			Set = Set.WithSide(code);
			Toggled?.Invoke(code, true);
			if (_packs is not null)
			{
				DownloadRequested?.Invoke(code);
			}
		}

		Rebuild();
		return Task.CompletedTask;
	}
}

/// <summary>
/// Settings, Story languages (Slice 173, mock frame 87): the picker with its explanation and, once three are picked, the line that says to untick one first.
/// A tick saves at once and starts that language's voice download; leaving counts the change (<see cref="UsageEventName.StoryLanguagesSet"/>, and one
/// <see cref="UsageEventName.StoryLanguageAdded"/> per language added).
/// </summary>
public sealed class StoryLanguagesPage : SettingsSubPage
{
	private readonly StoryLanguagePacks _packs;
	private readonly UsageCounter _usage;
	private readonly IStoryLibrary _library;
	private readonly StoryTranslator _translator;
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly StoryLanguagePicker _picker;
	private readonly Label _full;
	private readonly List<string> _added = [];
	private StoryLanguageSet _opened = StoryLanguages.Current();

	public StoryLanguagesPage(StoryLanguagePacks packs, UsageCounter usage, IStoryLibrary library, StoryTranslator translator)
		: base(AppResources.StoryLanguages_SettingsTitle)
	{
		_packs = packs;
		_usage = usage;
		_library = library;
		_translator = translator;
		_wait = new WaitController(_waitOverlay);
		AddOverlay(_waitOverlay);
		_picker = new StoryLanguagePicker(_opened, packs);
		_picker.Toggled += OnToggled;
		_picker.DownloadRequested += OnDownloadRequested;
		_full = new Label { Text = AppResources.StoryLanguages_Full, FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), Margin = new Thickness(4, 0), LineHeight = 1.2 };
		Body.Children.Add(new Label { Text = AppResources.StoryLanguages_SettingsLine, FontSize = 13.5, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 0), LineHeight = 1.2 });
		Body.Children.Add(_picker);
		Body.Children.Add(_full);
		ShowFull();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		SetTitle(AppResources.StoryLanguages_SettingsTitle);
		_packs.Changed += OnPacksChanged;
		_opened = StoryLanguages.Current();
		_picker.Rebuild();
		_ = _translator.ReconcilePacksAsync(); // a grown-up screen: adds the packs an older install already has to the record (loads ML Kit)
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_packs.Changed -= OnPacksChanged;
		CountChange();
	}

	private void OnPacksChanged(object? sender, EventArgs e)
	{
		if (!_picker.Set.Sides.SequenceEqual(StoryLanguages.Current().Sides) || !_picker.TryUpdateProgress())
		{
			_picker.Rebuild();
			ShowFull(); // a background failure may have unticked a language
		}
	}

	private void OnToggled(string language, bool ticked)
	{
		StoryLanguages.SaveSides(_picker.Set);
		if (ticked && !_added.Contains(language))
		{
			_added.Add(language);
		}

		ShowFull();
	}

	// A tick (or "Download") fetches the voice; on mobile data the grown-up is asked first (mock 87, frame 50). The parent's own books that still need their
	// translation into the language are then offered its translation pack (the child's reader never offers a download).
	private async void OnDownloadRequested(string language)
	{
		try
		{
			await _packs.RequestAsync(Window?.Page ?? this, [language], _wait);
		}
		catch (OperationCanceledException)
		{
			return; // the grown-up cancelled or dismissed the mobile-data prompt: not a failure
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not start the {language} voice: {ex}");
			PackDownloadLog.LanguageFailure(language, PackDownloadResult.Failed, PackFailureAction.UntickAndShowRetry);
			_packs.GiveUp(language);
			return;
		}

		await OfferOwnBooksSafelyAsync(language);
	}

	private async Task OfferOwnBooksSafelyAsync(string language)
	{
		try
		{
			await OfferOwnBooksAsync(language);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not offer the {language} translation: {ex}"); // the voice download is running; this offer is optional
		}
	}

	private async Task OfferOwnBooksAsync(string language)
	{
		if (!_translator.IsAvailable || !StoryLanguages.Current().Contains(language))
		{
			return; // no engine on this build (Windows), or the language was unticked meanwhile
		}

		var prompt = new TranslationPackPrompt(_translator, _library);
		await prompt.OfferForLanguageAsync(Window?.Page ?? this, language, (stage, line) => _wait.BeginCancellable(stage, line), () => _wait.End());
	}

	private void ShowFull() => _full.IsVisible = _picker.Set.IsFull;

	// Counted once, on leaving, when the set differs from the one the page opened with.
	private void CountChange()
	{
		StoryLanguageSet now = StoryLanguages.Current();
		if (now.Sides.SequenceEqual(_opened.Sides))
		{
			return;
		}

		StoryLanguageCounts.RecordSet(_usage, now, UsageDetail.LanguagesFromSettings, _added.Where(now.Sides.Contains));
		_added.Clear();
		_opened = now;
	}
}

/// <summary>The counts about a saved set of story languages (Slice 173), shared by the intro and Settings.</summary>
public static class StoryLanguageCounts
{
	/// <summary>One <see cref="UsageEventName.StoryLanguagesSet"/> (app language, how many languages, where) and one <see cref="UsageEventName.StoryLanguageAdded"/> per added language.</summary>
	public static void RecordSet(UsageCounter usage, StoryLanguageSet set, string where, IEnumerable<string> added)
	{
		usage.RecordGeneral(UsageEventName.StoryLanguagesSet, set.Main, UsageDetail.StoryLanguages(set.All.Count, where));
		foreach (string language in added)
		{
			usage.RecordGeneral(UsageEventName.StoryLanguageAdded, language);
		}
	}
}

/// <summary>
/// "Which one goes?" (Slice 173, mock frame 90): a bottom sheet shown when the app language changes to one that is not a story language yet and both
/// side languages are taken. The old app language and both sides are listed with nothing picked; the red-free "Remove" button stays off until a pick is made.
/// Cancel, a tap outside, or back keeps the old app language. The copy is a draft.
/// </summary>
public sealed class DropLanguageSheet : ContentPage
{
	private const double SheetCorner = 28;
	private const double RowMinHeight = 54;
	private const double RadioSize = 24;
	private const double ButtonHeight = 52;
	private const double DisabledOpacity = 0.4;

	private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly string _oldMain;
	private readonly IReadOnlyList<string> _choices;
	private readonly VerticalStackLayout _rows = new();
	private readonly Border _remove = new();
	private readonly Label _removeLabel = new();
	private string? _picked;
	private bool _closing;

	private DropLanguageSheet(string newMain, string oldMain, IReadOnlyList<string> choices)
	{
		_oldMain = oldMain;
		_choices = choices;
		BackgroundColor = Colors.Transparent;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = SafeAreaEdges.None;
		FlowDirection = UiFlow.Screen;
		var scrim = new Grid { BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim) };
		CastViews.AddTap(scrim, () => CloseAsync(null));
		scrim.Add(Sheet(newMain));
		Content = scrim;
		Render();
	}

	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(null);
		return true;
	}

	private Border Sheet(string newMain)
	{
		var grabber = new BoxView { WidthRequest = 40, HeightRequest = 5, CornerRadius = 3, Color = DinoTokens.Get(DinoTokens.Hairline), HorizontalOptions = LayoutOptions.Center };
		var title = new Label { Text = AppResources.Format(AppResources.StoryLanguages_DropTitle, LanguageNames.NativeName(newMain)), FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 4, 0, 0) };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var body = new Label { Text = AppResources.StoryLanguages_DropBody, FontSize = 14, TextColor = DinoTokens.Get(DinoTokens.Body), LineHeight = 1.2 };
		var list = new Border { StrokeThickness = 1.5, Stroke = DinoTokens.Get(DinoTokens.Hairline), StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = _rows };
		var buttons = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 4, 0, 0), ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		buttons.Add(CancelButton(), 0);
		buttons.Add(RemoveButton(), 1);
		var column = new VerticalStackLayout { Spacing = 12, Children = { grabber, title, body, list, buttons } };
		var sheet = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(18, 12, 18, 26),
			VerticalOptions = LayoutOptions.End,
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCorner, SheetCorner, 0, 0) },
			Content = column,
		};
		CastViews.AddTap(sheet, () => Task.CompletedTask);
		return sheet;
	}

	private Border CancelButton()
	{
		var cancel = new Border
		{
			HeightRequest = ButtonHeight,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			BackgroundColor = Colors.White,
			Stroke = DinoTokens.Get(DinoTokens.Hairline),
			StrokeThickness = 2,
			Content = new Label { Text = AppResources.Common_Cancel, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(cancel, AppResources.Common_Cancel);
		CastViews.AddTap(cancel, () => CloseAsync(null));
		return cancel;
	}

	private Border RemoveButton()
	{
		_removeLabel.FontSize = 16;
		_removeLabel.FontAttributes = FontAttributes.Bold;
		_removeLabel.TextColor = Colors.White;
		_removeLabel.HorizontalOptions = LayoutOptions.Center;
		_removeLabel.VerticalOptions = LayoutOptions.Center;
		_removeLabel.LineBreakMode = LineBreakMode.TailTruncation;
		_remove.HeightRequest = ButtonHeight;
		_remove.StrokeThickness = 0;
		_remove.BackgroundColor = DinoTokens.Get(DinoTokens.Brand);
		_remove.StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 };
		_remove.Padding = new Thickness(8, 0);
		_remove.Content = _removeLabel;
		CastViews.AddTap(_remove, () => _picked is null ? Task.CompletedTask : CloseAsync(_picked));
		return _remove;
	}

	private void Render()
	{
		_rows.Children.Clear();
		foreach (string language in _choices)
		{
			if (_rows.Children.Count > 0)
			{
				_rows.Children.Add(SettingsRows.Divider());
			}

			_rows.Children.Add(Row(language));
		}

		_remove.Opacity = _picked is null ? DisabledOpacity : 1;
		_removeLabel.Text = AppResources.Format(AppResources.StoryLanguages_Remove, _picked is null ? string.Empty : LanguageNames.NativeName(_picked)).Trim();
		SemanticProperties.SetDescription(_remove, _removeLabel.Text);
	}

	private View Row(string language)
	{
		bool picked = language == _picked;
		bool rtl = DinoFonts.IsRightToLeft(language);
		var radio = new Border
		{
			WidthRequest = RadioSize,
			HeightRequest = RadioSize,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new Ellipse(),
			Stroke = picked ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.CheckEmpty),
			StrokeThickness = picked ? 7 : 2,
			BackgroundColor = Colors.White,
		};
		var name = new Label
		{
			Text = LanguageNames.NativeName(language),
			FontSize = 15,
			FontAttributes = picked ? FontAttributes.Bold : FontAttributes.None,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
		};
		var row = new Grid
		{
			MinimumHeightRequest = RowMinHeight,
			Padding = new Thickness(14, 0),
			ColumnSpacing = 12,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			BackgroundColor = picked ? DinoTokens.Get(DinoTokens.SelectedRow) : Colors.Transparent,
		};
		row.Add(radio, 0);
		row.Add(name, 1);
		if (language == _oldMain)
		{
			row.Add(new Label { Text = AppResources.StoryLanguages_WasApp, FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center }, 2);
		}

		SemanticProperties.SetDescription(row, name.Text);
		CastViews.AddTap(row, () =>
		{
			_picked = language;
			Render();
			return Task.CompletedTask;
		});
		return row;
	}

	private async Task CloseAsync(string? dropped)
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		try
		{
			await Navigation.PopModalAsync(animated: false);
		}
		finally
		{
			_result.TrySetResult(dropped);
		}
	}

	/// <summary>Asks which of <paramref name="choices"/> goes when <paramref name="newMain"/> becomes the app language; null when the parent cancelled.</summary>
	public static async Task<string?> AskAsync(INavigation navigation, string newMain, string oldMain, IReadOnlyList<string> choices)
	{
		var sheet = new DropLanguageSheet(newMain, oldMain, choices);
		await navigation.PushModalAsync(sheet, animated: false);
		return await sheet._result.Task;
	}
}
