using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The "This book" sheet of the reading screen (Slice 110, mock frame 43): a white bottom sheet over the dimmed page, opened from the add-book button
/// behind the grown-up gate. It holds the per-book "Show original text" switch (Decision 112), a way to the cast and "Back to the story".
/// It lives inside the reading page rather than on the navigation stack, so leaving the app cannot strand it.
/// </summary>
internal sealed class BookSheet : Grid
{
	private const double SheetCorner = 28;
	private const double RowHeight = 64;
	private const double RowCorner = 16;
	private const double PillHeight = 48;
	private const double PillStroke = 2;
	private const double BackHeight = 50;
	private const double HintSize = 12.5;

	private readonly Label _subtitle = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Label _hint = new() { FontSize = HintSize, LineHeight = 1.3, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly ToggleSwitch _original = new();
	private readonly Border _row = new();
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
			Padding = new Thickness(18, 22, 18, 22),
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCorner, SheetCorner, 0, 0) },
			Content = Content(),
		};
		CastViews.AddTap(sheet, () => Task.CompletedTask);
		Children.Add(sheet);
		_original.Toggled += (_, _) => OnToggled();
	}

	/// <summary>Raised with the new state of "Show original text".</summary>
	public event Func<bool, Task>? OriginalChanged;

	/// <summary>Raised when the grown-up asks for the cast screen; the sheet closes first.</summary>
	public event Func<Task>? CastRequested;

	/// <summary>Raised when the grown-up closes the sheet to go on with the story (Back, a tap on the dimmed page or the system back button), not when it closes for the cast screen.</summary>
	public event Action? Dismissed;

	public void Show(string bookTitle, bool offerOriginal, bool showOriginal, string hint)
	{
		_subtitle.Text = bookTitle;
		_hint.Text = hint;
		_row.IsVisible = offerOriginal;
		_loading = true;
		_original.IsOn = showOriginal;
		_loading = false;
		IsVisible = true;
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

	private VerticalStackLayout Content()
	{
		var title = new Label { Text = ReadingText.BookTitle, FontSize = 20, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var header = new VerticalStackLayout { Spacing = 2, Children = { title, _subtitle } };
		return new VerticalStackLayout { Spacing = 12, Children = { header, OriginalRow(), CastPill(), BackButton() } };
	}

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
		var pill = new Border
		{
			HeightRequest = PillHeight,
			StrokeThickness = PillStroke,
			Stroke = DinoTokens.Get(DinoTokens.MergeBorder),
			BackgroundColor = Colors.White,
			StrokeShape = new RoundRectangle { CornerRadius = PillHeight / 2 },
			Content = new Label
			{
				Text = ReadingText.CastButton, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			},
		};
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

	private Border BackButton()
	{
		var back = new Border
		{
			HeightRequest = BackHeight,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeShape = new RoundRectangle { CornerRadius = BackHeight / 2 },
			Content = new Label
			{
				Text = ReadingText.BackToStory, FontSize = 15.5, FontAttributes = FontAttributes.Bold,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(back, ReadingText.BackToStory);
		CastViews.AddTap(back, () => { Dismiss(); return Task.CompletedTask; });
		return back;
	}
}
