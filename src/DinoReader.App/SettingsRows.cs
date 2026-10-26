using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The rows of the white cards on the add and edit screens (mock 9 and 8b): a title, a value or control at the end, an optional chevron.</summary>
public static class SettingsRows
{
	private const double RowHeight = 50;
	private const double RowPadding = 14;
	private const double RowFontSize = 14.5;
	private const double ChevronSize = 14;

	public static Label ValueLabel(string text) => new()
	{
		Text = text,
		FontSize = RowFontSize,
		FontAttributes = FontAttributes.Bold,
		TextColor = DinoTokens.Get(DinoTokens.Body),
		HorizontalTextAlignment = TextAlignment.End,
		VerticalOptions = LayoutOptions.Center,
	};

	public static IconView Chevron() => new()
	{
		Kind = IconKind.Chevron,
		Color = DinoTokens.Get(DinoTokens.Muted),
		Size = ChevronSize,
		StrokeWidth = 1.6,
		VerticalOptions = LayoutOptions.Center,
	};

	/// <summary>One row: <paramref name="title"/> at the start, <paramref name="end"/> after it, a chevron when <paramref name="tapped"/> is given.</summary>
	public static Grid Row(string title, View end, Func<Task>? tapped, BusyGuard busy)
	{
		var row = new Grid
		{
			MinimumHeightRequest = RowHeight,
			Padding = new Thickness(RowPadding, 0),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
			ColumnSpacing = 8,
		};
		row.Add(new Label { Text = title, FontSize = RowFontSize, VerticalOptions = LayoutOptions.Center }, 0);
		row.Add(end, 1);
		if (tapped is not null)
		{
			row.Add(Chevron(), 2);
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await busy.RunAsync(tapped);
			row.GestureRecognizers.Add(tap);
		}

		return row;
	}

	public static BoxView Divider() => new() { HeightRequest = 1, Color = DinoTokens.Get(DinoTokens.Hairline) };
}

/// <summary>
/// The "Help make Dino better" card of the grown-up settings (Slice 133, Decision 141, mock frame 62): its own white card with a title, a
/// helper line saying what is counted, and the usage-counts switch. On by default; off sends nothing. A grown-up page puts it in its column of cards.
/// </summary>
public static class UsageCountsCard
{
	private const double CardRadius = 18;
	private const double CardMinHeight = 64;
	private const double CardPadding = 14;
	private const double TitleSize = 15;
	private const double HelperSize = 12.5;

	public static Border Build(UsageCounter counter)
	{
		var toggle = new ToggleSwitch { IsOn = counter.Enabled, AccessibleName = AppResources.Usage_Title, VerticalOptions = LayoutOptions.Center };
		toggle.Toggled += (_, _) => counter.Enabled = toggle.IsOn;
		var text = new VerticalStackLayout
		{
			Spacing = 2,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = AppResources.Usage_Title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold },
				new Label { Text = AppResources.Usage_Helper, FontSize = HelperSize, TextColor = DinoTokens.Get(DinoTokens.Body) },
			},
		};
		var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 12 };
		row.Add(text, 0);
		row.Add(toggle, 1);
		return new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			MinimumHeightRequest = CardMinHeight,
			Padding = CardPadding,
			Content = row,
		};
	}
}
