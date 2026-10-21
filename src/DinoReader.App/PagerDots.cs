using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>Display-only page dots under the page (mock screen 3): one per page, the current one a wide pill.
/// A story with more pages than <see cref="MaxDots"/> shows none: a row of dots stops being readable.</summary>
public sealed class PagerDots : HorizontalStackLayout
{
	private const int MaxDots = 12;
	private const double Gap = 8;
	private const double Idle = 9;
	private const double CurrentWidth = 26;

	private int _count = -1;
	private int _current = -1;
	private ReadingTheme? _theme;

	public PagerDots()
	{
		Spacing = Gap;
		HorizontalOptions = LayoutOptions.Center;
		Padding = new Thickness(0, 12, 0, 2);
		InputTransparent = true;
		SemanticProperties.SetDescription(this, string.Empty);
	}

	public void Show(int count, int current, ReadingTheme theme)
	{
		if (count == _count && current == _current && ReferenceEquals(theme, _theme))
		{
			return;
		}

		(_count, _current, _theme) = (count, current, theme);
		Children.Clear();
		IsVisible = count > 1 && count <= MaxDots;
		for (int i = 0; i < count && IsVisible; i++)
		{
			Children.Add(Dot(i == current, theme));
		}
	}

	private static Border Dot(bool isCurrent, ReadingTheme theme) => new()
	{
		WidthRequest = isCurrent ? CurrentWidth : Idle,
		HeightRequest = Idle,
		BackgroundColor = isCurrent ? theme.DotCurrent : theme.DotIdle,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = Idle / 2 },
	};
}
