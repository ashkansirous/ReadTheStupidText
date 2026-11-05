using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// A book's options sheet (Slice 110, mock frame 43; Slice 189, mock frame 85b): a white bottom sheet over the dimmed shelf, opened from the "⋯" on a book's cover in grown-up mode (the reading screen has no
/// grown-ups button). The header names the book and where it came from. It holds "Read with my voice" once a page is recorded
/// (Slice 111, Decision 115), the Record and Cast pills, Edit pages (own books), Export (developer switch), "Remove from the shelf" and Done.
/// It lives inside the shelf page rather than on the navigation stack, so leaving the app cannot strand it.
/// </summary>
internal sealed class BookSheet : Grid
{
	/// <summary>The "Read with my voice" row: how many pages are recorded, of how many, and the switch.</summary>
	public sealed record VoiceOffer(int Recorded, int Pages, bool On);

	/// <summary>What the sheet shows for one book: its name, where it came from, its cover, which rows apply and the voice switch's state.</summary>
	public sealed record Content(string Title, string OriginText, string? CoverImagePath, Color CoverFill, BookSheetPlan Plan, VoiceOffer? Voice);

	private const double SheetCorner = 28;
	private const double CardCorner = 26;
	private const double CardCoverCorner = 14;
	private const double CardPadding = 16;
	private const double CardRowSpacing = 9;
	private const double CardReadNowHeight = 52;
	private const double CardActionHeight = 44;
	private const double RowHeight = 64;
	private const double RowCorner = 16;
	private const double PillHeight = 48;
	private const double PillStroke = 2;
	private const double ReadNowHeight = 60;
	private const double ActionHeight = 48;
	private const double ThumbWidth = 44;
	private const double ThumbHeight = 58;
	private const double ThumbCorner = 8;
	private const double CloseSize = 48; // Android's 48 dp minimum touch target (the mock drew 36)
	private const double GrabWidth = 40;
	private const double GrabHeight = 5;
	private static readonly Color GrabColour = Color.FromArgb("#e3dccd");
	private static readonly Color RemoveInk = Color.FromArgb("#C8402A");
	private const double HintSize = 12.5;
	private const double PhoneTitleSize = 19;
	private const double TabletTitleSize = 26;
	private const double TabletReadNowFont = 19;
	private const double TabletCardCorner = 32;
	private const double TabletCardGap = 16;

	private readonly Label _title = new() { FontSize = PhoneTitleSize, FontAttributes = FontAttributes.Bold, LineHeight = 1.2, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
	private readonly Label _subtitle = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Border _thumb = new();
	private readonly Border _editRow = new();
	private readonly Border _exportRow = new();
	private readonly Label _editLabel = new();
	private readonly Label _exportLabel = new();
	private readonly Label _removeLabel = new();
	private readonly Label _readNowLabel = new();
	private readonly Label _voiceHint = new() { FontSize = HintSize, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly ToggleSwitch _voice = new();
	private readonly Border _voiceRow = new();
	private readonly Border _removeRow = new();
	private readonly Border _readNowRow = new();
	private readonly ScrollView _scroll = new();
	private readonly Border _sheet = new();
	private readonly VerticalStackLayout _sheetRows = new() { Spacing = 12 };
	private readonly VerticalStackLayout _cardRows = new() { Spacing = CardRowSpacing };
	private readonly Grid _pills = new() { ColumnSpacing = 8 };
	private readonly Grid _card = new() { ColumnSpacing = CardPadding };
	private readonly Border _cover = new(); // the card's big cover; the sheet's header has the small thumbnail
	private View _grab = null!;
	private Border _closeButton = null!;
	private Grid _header = null!;
	private Border _recordPill = null!;
	private Border _castPill = null!;
	private BookSheetShape _shape = BookSheetShape.BottomSheet;
	private bool _loading;

	public BookSheet()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Dismiss(); return Task.CompletedTask; });
		_sheet.BackgroundColor = Colors.White;
		_sheet.StrokeThickness = 0;
		_sheet.Content = _scroll; // a short screen scrolls the rows instead of cutting them off
		BuildParts();
		Arrange(BookSheetShape.BottomSheet);
		CastViews.AddTap(_sheet, () => Task.CompletedTask);
		// No Done row (Slice 228): a tap outside or the labelled close button in the header closes the sheet; a swipe down is not used, it fought the scrolling rows.
		Children.Add(_sheet);
		_voice.Toggled += (_, _) => OnVoiceToggled();
		SizeChanged += (_, _) => FitToWindow(Width, Height);
	}

	// Each row is built once and re-parented by Arrange, so a rotation never doubles a tap handler.
	private void BuildParts()
	{
		_grab = GrabHandle();
		_header = Header();
		_recordPill = RecordPill();
		_castPill = CastPill();
		ReadNowRow();
		VoiceRow();
		QuietRow(_editRow, _editLabel, () => EditRequested, "BookSheetEdit");
		QuietRow(_exportRow, _exportLabel, () => ExportRequested, "BookSheetExport");
		RemoveRow();
		_cover.StrokeThickness = 0;
		_cover.WidthRequest = BookSheetPlan.CardCoverWidth;
		_cover.HeightRequest = BookSheetPlan.CardCoverWidth / LandscapeLayout.DefaultPictureAspect;
		_cover.VerticalOptions = LayoutOptions.Start;
		_cover.StrokeShape = new RoundRectangle { CornerRadius = CardCoverCorner };
	}

	// Slice 232 part B (mock Batch 44 frame 112): held sideways a bottom sheet would cover a 360 dp screen, so the sheet is one centred card (cover left, actions right); upright it is the bottom sheet.
	private void FitToWindow(double width, double height)
	{
		BookSheetShape shape = BookSheetPlan.ShapeFor(width, height);
		if (shape != _shape)
		{
			Arrange(shape);
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] book sheet card={shape.Card} width={shape.CardWidth} coverOnTop={shape.CoverOnTop}");
#endif
		}
	}

	private void Arrange(BookSheetShape shape)
	{
		_scroll.Content = null;
		_sheetRows.Children.Clear();
		_cardRows.Children.Clear();
		_card.Children.Clear();
		_pills.Children.Clear();
		if (shape.Card)
		{
			ArrangeCard(shape);
		}
		else
		{
			ArrangeSheet();
		}

		_shape = shape;
	}

	private void ArrangeSheet()
	{
		SizeRows(readNow: ReadNowHeight, pill: PillHeight, readNowFont: 18);
		_pills.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
		_pills.Add(_recordPill, 0);
		_pills.Add(_castPill, 1);
		_thumb.IsVisible = true;
		_title.FontSize = PhoneTitleSize;
		_header.VerticalOptions = LayoutOptions.Fill;
		foreach (View row in new View[] { _grab, _header, _readNowRow, _voiceRow, _pills, _editRow, _exportRow, _removeRow })
		{
			_sheetRows.Children.Add(row);
		}

		_sheet.VerticalOptions = LayoutOptions.End;
		_sheet.HorizontalOptions = LayoutOptions.Fill;
		_sheet.WidthRequest = -1;
		_sheet.MaximumHeightRequest = double.PositiveInfinity;
		_sheet.Padding = new Thickness(18, 12, 18, 26);
		_sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCorner, SheetCorner, 0, 0) };
		_scroll.Content = _sheetRows;
	}

	private void ArrangeCard(BookSheetShape shape)
	{
		bool coverOnTop = shape.CoverOnTop;
		if (coverOnTop)
		{
			SizeRows(readNow: TabletCardLayout.BookSheetReadNowHeight, pill: TabletCardLayout.BookSheetActionHeight, readNowFont: TabletReadNowFont);
		}
		else
		{
			SizeRows(readNow: CardReadNowHeight, pill: CardActionHeight, readNowFont: 17);
		}

		_pills.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
		_pills.Add(_recordPill, 0);
		_pills.Add(_castPill, 1);
		_thumb.IsVisible = false; // the big cover takes its place
		if (!coverOnTop)
		{
			_cardRows.Children.Add(_header);
		}

		foreach (View row in new View[] { _readNowRow, _voiceRow, _pills, _editRow, _exportRow, _removeRow })
		{
			_cardRows.Children.Add(row);
		}

		if (coverOnTop)
		{
			ArrangeCoverOnTop();
		}
		else
		{
			ArrangeCoverLeft();
		}

		_sheet.VerticalOptions = LayoutOptions.Center;
		_sheet.HorizontalOptions = LayoutOptions.Center;
		_sheet.WidthRequest = shape.CardWidth;
		_sheet.MaximumHeightRequest = shape.CardMaxHeight;
		_sheet.Padding = new Thickness(coverOnTop ? TabletCardLayout.BookSheetPadding : CardPadding);
		_sheet.StrokeShape = new RoundRectangle { CornerRadius = coverOnTop ? TabletCardCorner : CardCorner };
		_scroll.Content = _card;
	}

	// Held sideways (Slice 232): the cover on the card's left, the header and the actions in one column beside it.
	private void ArrangeCoverLeft()
	{
		_title.FontSize = PhoneTitleSize;
		_cover.WidthRequest = BookSheetPlan.CardCoverWidth;
		_cover.HeightRequest = BookSheetPlan.CardCoverWidth / LandscapeLayout.DefaultPictureAspect;
		_card.RowDefinitions = [];
		_card.RowSpacing = 0;
		_header.VerticalOptions = LayoutOptions.Fill;
		_card.ColumnDefinitions = [new ColumnDefinition(new GridLength(BookSheetPlan.CardCoverWidth)), new ColumnDefinition(GridLength.Star)];
		_card.Add(_cover, 0);
		_card.Add(_cardRows, 1);
		Grid.SetColumnSpan(_cardRows, 1);
	}

	// A tablet held upright (Slice 234 part B, mock Batch 45 frame 136): the cover on top at the left with the book's name beside it, the actions in a column under both.
	private void ArrangeCoverOnTop()
	{
		_title.FontSize = TabletTitleSize;
		_cover.WidthRequest = TabletCardLayout.BookSheetCoverWidth;
		_cover.HeightRequest = TabletCardLayout.BookSheetCoverWidth / LandscapeLayout.DefaultPictureAspect;
		_card.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		_card.RowSpacing = TabletCardGap;
		_header.VerticalOptions = LayoutOptions.End;
		_card.ColumnDefinitions = [new ColumnDefinition(new GridLength(TabletCardLayout.BookSheetCoverWidth)), new ColumnDefinition(GridLength.Star)];
		_card.Add(_cover, 0, 0);
		_card.Add(_header, 1, 0);
		_card.Add(_cardRows, 0, 1);
		Grid.SetColumnSpan(_cardRows, 2);
	}

	// The heights that differ between the sheet and the card; Remove is the last row on both the card and the sheet.
	private void SizeRows(double readNow, double pill, double readNowFont)
	{
		_readNowRow.HeightRequest = readNow;
		((Microsoft.Maui.Controls.Shapes.RoundRectangle)_readNowRow.StrokeShape!).CornerRadius = readNow / 2;
		_readNowLabel.FontSize = readNowFont;
		_recordPill.HeightRequest = pill;
		_castPill.HeightRequest = pill;
		_removeRow.HeightRequest = pill;
	}

	/// <summary>Raised with the new state of "Read with my voice".</summary>
	public event Func<bool, Task>? VoiceChanged;

	/// <summary>Raised for "Read now" (Slice 228); the sheet closes first and the page leaves grown-up mode to open the book.</summary>
	public event Func<Task>? ReadRequested;

	/// <summary>Raised when the grown-up asks for the cast screen; the sheet closes first.</summary>
	public event Func<Task>? CastRequested;

	/// <summary>Raised when the grown-up asks for the Record screen; the sheet closes first.</summary>
	public event Func<Task>? RecordRequested;

	/// <summary>Raised when the grown-up asks to edit the book's pages (own books); the sheet closes first.</summary>
	public event Func<Task>? EditRequested;

	/// <summary>Raised for the developer-only Export; the sheet closes first.</summary>
	public event Func<Task>? ExportRequested;

	/// <summary>Raised for "Remove from the shelf"; the sheet stays open under the question and the page closes it once the book is gone.</summary>
	public event Func<Task>? RemoveRequested;

	/// <summary>Raised when the grown-up closes the sheet to go on with the story (Back, a tap on the dimmed page or the system back button), not when it closes for the cast screen.</summary>
	public event Action? Dismissed;

	public void Show(Content content)
	{
		_ = _scroll.ScrollToAsync(0, 0, false); // the next book's sheet opens at its header
		_title.Text = content.Title;
		_subtitle.Text = content.OriginText;
		ShowThumb(content);
		_voiceRow.IsVisible = content.Plan.ShowVoice && content.Voice is not null;
		_editRow.IsVisible = content.Plan.ShowEditPages;
		_exportRow.IsVisible = content.Plan.ShowExport;
		_loading = true;
		_voice.IsOn = content.Voice?.On ?? false;
		_loading = false;
		_voiceHint.Text = content.Voice is null ? string.Empty : ReadingText.UseMyVoiceHint(content.Voice.Recorded, content.Voice.Pages);
		ApplyTexts();
		FitToParent(); // a hidden sheet gets no SizeChanged, so a rotation while it was closed is applied before it shows
		IsVisible = true;
		FocusReadNow();
	}

	// Slice 235 part B (mock Batch 46 frame 156): a keyboard user lands on "Read now" when the sheet opens. The row is a tapped Border, which WinUI does not focus by itself, so it is made a tab stop
	// first; the focus goes in once the shown sheet has a native view. Windows only: nothing happens elsewhere.
	private void FocusReadNow()
	{
#if WINDOWS
		Dispatcher.Dispatch(() =>
		{
			if (IsVisible && _readNowRow.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement element)
			{
				element.IsTabStop = true;
				element.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
			}
		});
#endif
	}

	private void FitToParent()
	{
		if (Parent is VisualElement { Width: > 0, Height: > 0 } page)
		{
			FitToWindow(page.Width, page.Height);
		}
	}

	// The labels are set on every show, so a changed app language reaches this cached sheet.
	private void ApplyTexts()
	{
		_editLabel.Text = AppResources.Book_EditPages;
		_exportLabel.Text = AppResources.Gallery_ExportStory;
		_removeLabel.Text = AppResources.Book_Remove;
		_readNowLabel.Text = AppResources.Book_ReadNow;
		SemanticProperties.SetDescription(_editRow, _editLabel.Text);
		SemanticProperties.SetDescription(_exportRow, _exportLabel.Text);
		SemanticProperties.SetDescription(_removeRow, _removeLabel.Text);
		SemanticProperties.SetDescription(_readNowRow, _readNowLabel.Text);
		DesktopTips.Set(_closeButton, AppResources.Common_Close, DinoKey.Escape);
	}

	// The cover's picture, or its colour when the book has none.
	private void ShowThumb(Content content)
	{
		foreach (Border cover in new[] { _thumb, _cover })
		{
			cover.BackgroundColor = content.CoverFill;
			cover.Content = content.CoverImagePath is { } path ? new Image { Source = ImageSource.FromFile(path), Aspect = Aspect.AspectFill } : null;
		}
	}

	public void Hide() => IsVisible = false;

	/// <summary>Closes the sheet and tells the page the story can go on; nothing when it is not open.</summary>
	public void Dismiss()
	{
		if (!IsVisible)
		{
			return;
		}

		Hide();
		Dismissed?.Invoke();
	}

	private void OnVoiceToggled()
	{
		if (!_loading && VoiceChanged is { } changed)
		{
			_ = changed(_voice.IsOn);
		}
	}

	// Read now (Slice 228, mock Batch 43 frame 100): the first and biggest row, for every book. The sheet closes first, then the page leaves grown-up mode and opens the reader.
	private Border ReadNowRow()
	{
		_readNowLabel.FontSize = 18;
		_readNowLabel.FontAttributes = FontAttributes.Bold;
		_readNowLabel.TextColor = Colors.White;
		_readNowLabel.VerticalOptions = LayoutOptions.Center;
		var content = new HorizontalStackLayout
		{
			Spacing = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children = { new IconView { Kind = IconKind.Play, Color = Colors.White, Filled = true, Size = 20, VerticalOptions = LayoutOptions.Center }, _readNowLabel },
		};
		Border row = _readNowRow;
		row.HeightRequest = ReadNowHeight;
		row.StrokeThickness = 0;
		row.BackgroundColor = DinoTokens.Get(DinoTokens.Coral);
		row.StrokeShape = new RoundRectangle { CornerRadius = ReadNowHeight / 2 };
		row.Content = content;
		row.AutomationId = "BookSheetReadNow";
		CastViews.AddTap(row, ReadNowAsync);
		return row;
	}

	private async Task ReadNowAsync()
	{
		Dismiss(); // not Hide: the page's Dismissed handler also rebuilds the tiles when a switch was saved in the sheet
		if (ReadRequested is { } requested)
		{
			await requested();
		}
	}

	/// <summary>
	/// Slice 235 part B: Enter or Space on the open sheet reads the book, as a tap on "Read now" does, unless one of the sheet's real controls (a switch or a button: voice switch, Record, Cast,
	/// Remove, close) has the keyboard focus; the key then goes on to that control. Read now is a tapped Border that WinUI cannot focus, so it never has to hold the focus. False off Windows.
	/// </summary>
	public bool ActivateFocused()
	{
#if WINDOWS
		if (IsVisible && _readNowRow.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement element && !SheetControlHasFocus(element.XamlRoot))
		{
			WindowTrace.Write("Dino keyboard: the sheet's Enter or Space activated Read now");
			_ = ReadNowAsync();
			return true;
		}
#endif
		return false;
	}

#if WINDOWS
	// True when the focused element is a button or switch inside this sheet (not the shelf behind it).
	private bool SheetControlHasFocus(Microsoft.UI.Xaml.XamlRoot? root)
	{
		object? focused = root is null ? null : Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root);
		if (focused is not (Microsoft.UI.Xaml.Controls.Primitives.ButtonBase or Microsoft.UI.Xaml.Controls.ToggleSwitch or Microsoft.UI.Xaml.Controls.CheckBox)
			|| Handler?.PlatformView is not Microsoft.UI.Xaml.DependencyObject sheet)
		{
			return false;
		}

		for (var node = focused as Microsoft.UI.Xaml.DependencyObject; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
		{
			if (node == sheet)
			{
				return true;
			}
		}

		return false;
	}
#endif

	private static BoxView GrabHandle() => new()
	{
		WidthRequest = GrabWidth, HeightRequest = GrabHeight, CornerRadius = GrabHeight / 2, Color = GrabColour, HorizontalOptions = LayoutOptions.Center,
	};

	// The cover thumbnail with the book's name and where it came from (frame 85b).
	private Grid Header()
	{
		_thumb.WidthRequest = ThumbWidth;
		_thumb.HeightRequest = ThumbHeight;
		_thumb.StrokeThickness = 0;
		_thumb.StrokeShape = new RoundRectangle { CornerRadius = ThumbCorner };
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		var names = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _subtitle } };
		var left = new HorizontalStackLayout { Spacing = 12, Children = { _thumb, names } };
		var header = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		header.Add(left, 0);
		header.Add(CloseButton(), 1);
		return header;
	}

	// The accessible way to close the sheet (the scrim tap has no semantics): a small round button with a screen-reader label.
	private Border CloseButton()
	{
		Border close = new()
		{
			WidthRequest = CloseSize,
			HeightRequest = CloseSize,
			VerticalOptions = LayoutOptions.Start,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeShape = new RoundRectangle { CornerRadius = CloseSize / 2 },
			Content = new IconView { Kind = IconKind.ChevronDown, Color = DinoTokens.Get(DinoTokens.Ink), Size = 18, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
			AutomationId = "BookSheetClose",
		};
		SemanticProperties.SetDescription(close, AppResources.Common_Close);
		_closeButton = close;
		CastViews.AddTap(close, () => { Dismiss(); return Task.CompletedTask; });
		return close;
	}

	// Edit pages and Export: quiet full-width rows under the pills; each closes the sheet before its screen opens.
	private Border QuietRow(Border row, Label label, Func<Func<Task>?> handler, string automationId)
	{
		label.FontSize = 15;
		label.FontAttributes = FontAttributes.Bold;
		label.TextColor = DinoTokens.Get(DinoTokens.Brand);
		label.HorizontalOptions = LayoutOptions.Center;
		label.VerticalOptions = LayoutOptions.Center;
		row.HeightRequest = ActionHeight;
		row.StrokeThickness = 0;
		row.BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		row.StrokeShape = new RoundRectangle { CornerRadius = ActionHeight / 2 };
		row.Content = label;
		row.IsVisible = false;
		row.AutomationId = automationId;
		CastViews.AddTap(row, async () =>
		{
			Dismiss(); // tells the page, so a switch saved while the sheet was open refreshes the tiles
			if (handler() is { } requested)
			{
				await requested();
			}
		});
		return row;
	}

	// Remove from the shelf: red text with a trash icon at the bottom of the rows. The question comes first, so the sheet stays open until the book is gone.
	private Border RemoveRow()
	{
		_removeLabel.FontSize = 15;
		_removeLabel.FontAttributes = FontAttributes.Bold;
		_removeLabel.TextColor = RemoveInk;
		_removeLabel.VerticalOptions = LayoutOptions.Center;
		_removeLabel.MaxLines = 1;
		_removeLabel.LineBreakMode = LineBreakMode.TailTruncation;
		var content = new HorizontalStackLayout
		{
			Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children = { new IconView { Kind = IconKind.Trash, Color = RemoveInk, Size = 18, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center }, _removeLabel },
		};
		Border row = _removeRow;
		row.HeightRequest = ActionHeight;
		row.StrokeThickness = 0;
		row.BackgroundColor = Colors.Transparent;
		row.Content = content;
		row.AutomationId = "BookSheetRemove";
		SemanticProperties.SetDescription(row, AppResources.Book_Remove);
		CastViews.AddTap(row, async () =>
		{
			if (RemoveRequested is { } requested)
			{
				await requested();
			}
		});
		return row;
	}

	// "Read with my voice" (frame 43): the same card style as the other sheet rows, with the count of recorded pages under it.
	private Border VoiceRow()
	{
		var texts = new VerticalStackLayout
		{
			Spacing = 2,
			VerticalOptions = LayoutOptions.Center,
			Children = { new Label { Text = ReadingText.UseMyVoice, FontSize = 15, FontAttributes = FontAttributes.Bold }, _voiceHint },
		};
		var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		grid.Add(texts, 0);
		_voice.VerticalOptions = LayoutOptions.Center;
		AutomationProperties.SetIsInAccessibleTree(_voice, false);
		grid.Add(_voice, 1);
		_voiceRow.BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_voiceRow.StrokeThickness = 0;
		_voiceRow.MinimumHeightRequest = RowHeight;
		_voiceRow.Padding = new Thickness(16, 10);
		_voiceRow.StrokeShape = new RoundRectangle { CornerRadius = RowCorner };
		_voiceRow.Content = grid;
		_voiceRow.IsVisible = false;
		SemanticProperties.SetDescription(_voiceRow, ReadingText.UseMyVoice);
		CastViews.AddTap(_voiceRow, () => { _voice.IsOn = !_voice.IsOn; return Task.CompletedTask; });
		return _voiceRow;
	}

	// The Record and Cast pills side by side (frame 43; on the landscape card too, with Remove on its own last row); both leave the reading page for their own screen.
	private Border RecordPill()
	{
		var content = new HorizontalStackLayout
		{
			Spacing = 6,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new IconView { Kind = IconKind.Mic, Color = DinoTokens.Get(DinoTokens.Danger), Size = 18, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center },
				new Label { Text = ReadingText.RecordButton, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Center },
			},
		};
		Border pill = OutlinedPill(content);
		SemanticProperties.SetDescription(pill, ReadingText.RecordButton);
		CastViews.AddTap(pill, async () =>
		{
			Hide();
			if (RecordRequested is { } requested)
			{
				await requested();
			}
		});
		return pill;
	}

	private static Border OutlinedPill(View content) => new()
	{
		HeightRequest = PillHeight,
		StrokeThickness = PillStroke,
		Stroke = DinoTokens.Get(DinoTokens.MergeBorder),
		BackgroundColor = Colors.White,
		StrokeShape = new RoundRectangle { CornerRadius = PillHeight / 2 },
		Content = content,
	};

	private Border CastPill()
	{
		Border pill = OutlinedPill(new Label
		{
			Text = ReadingText.CastButton, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
			HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
		});
		SemanticProperties.SetDescription(pill, ReadingText.CastButton);
		CastViews.AddTap(pill, async () =>
		{
			Hide();
			if (CastRequested is { } requested)
			{
				await requested();
			}
		});
		return pill;
	}
}
