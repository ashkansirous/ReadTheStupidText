using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// A book's options sheet (Slice 110, mock frame 43; Slice 189, mock frame 85b): a white bottom sheet over the dimmed shelf, opened from the "⋯" on a book's cover in grown-up mode (the reading screen has no
/// grown-ups button). The header names the book and where it came from. It holds the per-book "Show original text" switch (Decision 112) for a translated book, "Read with my voice" once a page is recorded
/// (Slice 111, Decision 115), the Record and Cast pills, Edit pages (own books), Export (developer switch), "Remove from the shelf" and Done.
/// It lives inside the shelf page rather than on the navigation stack, so leaving the app cannot strand it.
/// </summary>
internal sealed class BookSheet : Grid
{
	/// <summary>The "Read with my voice" row: how many pages are recorded, of how many, and the switch.</summary>
	public sealed record VoiceOffer(int Recorded, int Pages, bool On);

	/// <summary>What the sheet shows for one book: its name, where it came from, its cover, which rows apply and the two switches' states.</summary>
	public sealed record Content(string Title, string OriginText, string? CoverImagePath, Color CoverFill, BookSheetPlan Plan, bool ShowOriginal, string OriginalHint, VoiceOffer? Voice);

	private const double SheetCorner = 28;
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

	private readonly Label _title = new() { FontSize = 19, FontAttributes = FontAttributes.Bold, LineHeight = 1.2, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
	private readonly Label _subtitle = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Border _thumb = new();
	private readonly Border _editRow = new();
	private readonly Border _exportRow = new();
	private readonly Label _editLabel = new();
	private readonly Label _exportLabel = new();
	private readonly Label _removeLabel = new();
	private readonly Label _readNowLabel = new();
	private readonly Label _hint = new() { FontSize = HintSize, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly ToggleSwitch _original = new();
	private readonly Border _row = new();
	private readonly Label _voiceHint = new() { FontSize = HintSize, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly ToggleSwitch _voice = new();
	private readonly Border _voiceRow = new();
	private readonly Border _removeRow = new();
	private readonly Border _readNowRow = new();
	private readonly ScrollView _scroll = new();
	private bool _loading;

	public BookSheet()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Dismiss(); return Task.CompletedTask; });
		var sheet = new Border
		{
			VerticalOptions = LayoutOptions.End,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(18, 12, 18, 26),
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCorner, SheetCorner, 0, 0) },
			Content = ScrolledRows(), // a short screen scrolls the rows instead of cutting them off
		};
		CastViews.AddTap(sheet, () => Task.CompletedTask);
		// No Done row (Slice 228): a tap outside or the labelled close button in the header closes the sheet; a swipe down is not used, it fought the scrolling rows.
		Children.Add(sheet);
		_original.Toggled += (_, _) => OnToggled();
		_voice.Toggled += (_, _) => OnVoiceToggled();
	}

	/// <summary>Raised with the new state of "Show original text".</summary>
	public event Func<bool, Task>? OriginalChanged;

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

	private ScrollView ScrolledRows()
	{
		_scroll.Content = Rows();
		return _scroll;
	}

	public void Show(Content content)
	{
		_ = _scroll.ScrollToAsync(0, 0, false); // the next book's sheet opens at its header
		_title.Text = content.Title;
		_subtitle.Text = content.OriginText;
		ShowThumb(content);
		_hint.Text = content.OriginalHint;
		_row.IsVisible = content.Plan.ShowOriginalText;
		_voiceRow.IsVisible = content.Plan.ShowVoice && content.Voice is not null;
		_editRow.IsVisible = content.Plan.ShowEditPages;
		_exportRow.IsVisible = content.Plan.ShowExport;
		_loading = true;
		_original.IsOn = content.ShowOriginal;
		_voice.IsOn = content.Voice?.On ?? false;
		_loading = false;
		_voiceHint.Text = content.Voice is null ? string.Empty : ReadingText.UseMyVoiceHint(content.Voice.Recorded, content.Voice.Pages);
		ApplyTexts();
		IsVisible = true;
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
		SemanticProperties.SetDescription(_readNowRow, _readNowLabel.Text);	}

	// The cover's picture, or its colour when the book has none.
	private void ShowThumb(Content content)
	{
		_thumb.BackgroundColor = content.CoverFill;
		_thumb.Content = content.CoverImagePath is { } path ? new Image { Source = ImageSource.FromFile(path), Aspect = Aspect.AspectFill } : null;
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

	private void OnToggled()
	{
		if (!_loading && OriginalChanged is { } changed)
		{
			_ = changed(_original.IsOn);
		}
	}

	private void OnVoiceToggled()
	{
		if (!_loading && VoiceChanged is { } changed)
		{
			_ = changed(_voice.IsOn);
		}
	}

	private VerticalStackLayout Rows() =>
		new() { Spacing = 12, Children = { GrabHandle(), Header(), ReadNowRow(), OriginalRow(), VoiceRow(), Pills(), QuietRow(_editRow, _editLabel, () => EditRequested, "BookSheetEdit"), QuietRow(_exportRow, _exportLabel, () => ExportRequested, "BookSheetExport"), RemoveRow() } };

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
		CastViews.AddTap(row, async () =>
		{
			Dismiss(); // not Hide: the page's Dismissed handler also rebuilds the tiles when a switch was saved in the sheet
			if (ReadRequested is { } requested)
			{
				await requested();
			}
		});
		return row;
	}

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

	// "Read with my voice" (frame 43): the same card as "Show original text", with the count of recorded pages under it.
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

	// The Record and Cast pills side by side (frame 43); both leave the reading page for their own screen.
	private Grid Pills()
	{
		var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		grid.Add(RecordPill(), 0);
		grid.Add(CastPill(), 1);
		return grid;
	}

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

	private Border OriginalRow()
	{
		var texts = new VerticalStackLayout
		{
			Spacing = 2,
			VerticalOptions = LayoutOptions.Center,
			Children = { new Label { Text = ReadingText.ShowOriginal, FontSize = 15, FontAttributes = FontAttributes.Bold }, _hint },
		};
		var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		grid.Add(texts, 0);
		_original.VerticalOptions = LayoutOptions.Center;
		AutomationProperties.SetIsInAccessibleTree(_original, false);
		grid.Add(_original, 1);
		_row.BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_row.StrokeThickness = 0;
		_row.MinimumHeightRequest = RowHeight;
		_row.Padding = new Thickness(16, 10);
		_row.StrokeShape = new RoundRectangle { CornerRadius = RowCorner };
		_row.Content = grid;
		SemanticProperties.SetDescription(_row, ReadingText.ShowOriginal);
		CastViews.AddTap(_row, () => { _original.IsOn = !_original.IsOn; return Task.CompletedTask; });
		return _row;
	}

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
