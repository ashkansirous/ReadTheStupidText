using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The list of languages a parent ticks story languages from (Slice 173, mock frames 86 and 87): the app language first, ticked and locked, then the other
/// fourteen, each a tickable row. Two ticked fills the set and greys the rest. With the <see cref="ShelfAudioPacks"/> (Settings) a tick is saved at once and starts the
/// language's pre-made audio, shown as a card with Cancel; without, it only holds the picks (the intro, which starts the audio on Done). Slice 278 part B: no voice is downloaded.
/// </summary>
public sealed class StoryLanguagePicker : ContentView
{
	private const double RowMinHeight = 54;
	private const double CheckSize = 26;
	private const double CheckCorner = 8;
	private const double NameSize = 15;
	private const double NoteSize = 12.5;
	private const double CardCorner = 18;
	private const double DisabledOpacity = 0.45;

	private static readonly Color LockedGrey = Color.FromArgb("#C9C3D6");

	private readonly ShelfAudioPacks? _shelf;
	private readonly Dictionary<string, VerticalStackLayout> _shelfHosts = [];
	private readonly Dictionary<string, View> _shelfCards = [];

	public StoryLanguagePicker(StoryLanguageSet initial, ShelfAudioPacks? shelf = null)
	{
		Set = initial;
		_shelf = shelf;
		Rebuild();
	}

	/// <summary>The set as ticked now.</summary>
	public StoryLanguageSet Set { get; private set; }

	/// <summary>Raised after a tick or an untick with the language and whether it is now ticked.</summary>
	public event Action<string, bool>? Toggled;

	/// <summary>Builds the rows again from the current ticks.</summary>
	public void Rebuild()
	{
		if (_shelf is not null)
		{
			Set = StoryLanguages.Current(); // Settings saves every tick at once
		}

		_shelfHosts.Clear();
		_shelfCards.Clear();
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
		grid.Add(NameAndCard(code, ticked && !main), 1);
		View? end = EndView(main);
		if (end is not null)
		{
			grid.Add(end, 2);
		}

		string name = LanguageNames.NativeName(code);
		SemanticProperties.SetDescription(grid, main ? OwnScript.Pair(name, AppResources.StoryLanguages_AppLanguage) : name);
		if (!main && !disabled)
		{
			Taps.AddTap(grid, () => ToggleAsync(code));
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

	private View NameAndCard(string code, bool sideTicked)
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
		if (sideTicked)
		{
			AddShelfCard(stack, code); // Slice 262 (frame 169): the shelf books' audio card (with Cancel) under the name in the same row
		}

		return stack;
	}

	private void AddShelfCard(VerticalStackLayout stack, string code)
	{
		if (_shelf is null)
		{
			return;
		}

		_shelfHosts[code] = stack;
		if (ShelfAudioPacksCard.For(code, _shelf.StateOf(code), () => CancelShelfAudio(code)) is { } card)
		{
			_shelfCards[code] = card;
			stack.Children.Add(card);
		}
	}

	/// <summary>Draws again only the shelf audio cards (a book is done, a wait begins): the rest of the picker and any open prompt stay as they are.</summary>
	public void UpdateShelfCards()
	{
		if (!MainThread.IsMainThread)
		{
			MainThread.BeginInvokeOnMainThread(UpdateShelfCards); // the card dictionaries are touched on the main thread only
			return;
		}

		foreach ((string code, VerticalStackLayout stack) in _shelfHosts.ToArray())
		{
			if (_shelfCards.Remove(code, out View? old))
			{
				stack.Children.Remove(old);
			}

			AddShelfCard(stack, code);
		}
	}

	private Task CancelShelfAudio(string code)
	{
		_shelf?.Cancel(code); // keeps the language and the packs already in
		return Task.CompletedTask;
	}

	private static View? EndView(bool main) =>
		main ? Note(AppResources.StoryLanguages_AppLanguage, DinoTokens.Get(DinoTokens.Muted)) : null;

	private static Label Note(string text, Color colour) => new()
	{
		Text = text,
		FontSize = NoteSize,
		TextColor = colour,
		VerticalOptions = LayoutOptions.Center,
	};

	private Task ToggleAsync(string code)
	{
		if (Set.Contains(code))
		{
			Set = Set.WithoutSide(code);
			Toggled?.Invoke(code, false); // saved first: the untick stops that language's shelf audio run (ShelfAudioPacks listens for the saved set)
		}
		else if (Set.CanAdd(code))
		{
			Set = Set.WithSide(code);
			Toggled?.Invoke(code, true);
		}

		Rebuild();
		return Task.CompletedTask;
	}
}

/// <summary>
/// Settings, Story languages (Slice 173, mock frame 87): the picker with its explanation and, once three are picked, the line that says to untick one first.
/// A tick saves at once and starts that language's pre-made audio for the shelf books; leaving counts the change (<see cref="UsageEventName.StoryLanguagesSet"/>, and one
/// <see cref="UsageEventName.StoryLanguageAdded"/> per language added).
/// </summary>
public sealed class StoryLanguagesPage : SettingsSubPage
{
	private readonly ShelfAudioPacks _shelf;
	private readonly UsageCounter _usage;
	private readonly StoryLanguagePicker _picker;
	private readonly Label _full;
	private readonly List<string> _added = [];
	private StoryLanguageSet _opened = StoryLanguages.Current();

	public StoryLanguagesPage(ShelfAudioPacks shelf, UsageCounter usage)
		: base(AppResources.StoryLanguages_SettingsTitle)
	{
		_shelf = shelf;
		_usage = usage;
		_picker = new StoryLanguagePicker(_opened, shelf);
		_picker.Toggled += OnToggled;
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
		_shelf.Changed += OnShelfChanged;
		_opened = StoryLanguages.Current();
		_picker.Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_shelf.Changed -= OnShelfChanged;
		CountChange();
	}

	// A shelf audio step has no bar to move in place: only the rows' cards are drawn again (a book is done at most every few seconds), never the whole picker.
	private void OnShelfChanged(object? sender, EventArgs e) => _picker.UpdateShelfCards();

	// A tick saves at once and fetches the language's pre-made audio for the shelf books (Slice 262). On mobile data only, the grown-up is asked first whether to download now
	// or wait for Wi-Fi (ShelfAudioAsk); the run is started with the answer.
	private void OnToggled(string language, bool ticked)
	{
		StoryLanguages.SaveSides(_picker.Set);
		if (ticked && !_added.Contains(language))
		{
			_added.Add(language);
		}

		if (ticked)
		{
			_ = StartAudioAsync(language);
		}

		ShowFull();
	}

	private async Task StartAudioAsync(string language)
	{
		VoiceRequestOutcome outcome = VoiceRequestOutcome.NothingAsked;
		try
		{
			outcome = await ShelfAudioAsk.AskAsync(this, [language]);
		}
		catch (Exception ex)
		{
			// no-fallback: the question could not be shown; the audio then follows the ordinary Wi-Fi rule
			AudioPackLog.Write($"the mobile data question failed ({ex.GetType().Name})");
		}

		if (StoryLanguages.Current().Sides.Contains(language))
		{
			_shelf.Start(language, outcome); // unticked while the question was open: nothing is fetched for it
		}
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
		Taps.AddTap(scrim, () => CloseAsync(null));
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
		Taps.AddTap(sheet, () => Task.CompletedTask);
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
		Taps.AddTap(cancel, () => CloseAsync(null));
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
		Taps.AddTap(_remove, () => _picked is null ? Task.CompletedTask : CloseAsync(_picked));
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
		Taps.AddTap(row, () =>
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
