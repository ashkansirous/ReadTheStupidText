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
