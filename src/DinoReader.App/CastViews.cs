using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The small pieces the cast screen and its merge sheet are built from (mock screens 10 and 11).</summary>
internal static class CastViews
{
	private const double ChipHeight = 32;

	public static Border Card() => new()
	{
		BackgroundColor = Colors.White,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = 18 },
	};

	public static BoxView Divider() => new() { HeightRequest = 1, Color = DinoTokens.Get(DinoTokens.Hairline) };

	public static Border Avatar(string initial, Color fill, Color ink, double size, double fontSize) => new()
	{
		WidthRequest = size,
		HeightRequest = size,
		BackgroundColor = fill,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
		Content = new Label
		{
			Text = initial, FontSize = fontSize, FontAttributes = FontAttributes.Bold, TextColor = ink,
			HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
		},
	};

	public static Border Badge(string text, Color ink, Color fill) => new()
	{
		BackgroundColor = fill,
		StrokeThickness = 0,
		Padding = new Thickness(6, 1),
		VerticalOptions = LayoutOptions.Center,
		StrokeShape = new RoundRectangle { CornerRadius = 6 },
		Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = ink },
	};

	/// <summary>The pill under the header that says which language's cast is on screen (Slice 108, mock frame 40).</summary>
	public static Border LanguageChip(string text) => new()
	{
		HeightRequest = ChipHeight,
		BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
		StrokeThickness = 0,
		Padding = new Thickness(14, 0),
		HorizontalOptions = LayoutOptions.Start,
		StrokeShape = new RoundRectangle { CornerRadius = 16 },
		Content = new HorizontalStackLayout
		{
			Spacing = 6,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new IconView { Kind = IconKind.Globe, Color = DinoTokens.Get(DinoTokens.BrandEdge), Size = 15, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center },
				new Label
				{
					Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.BrandEdge),
					VerticalOptions = LayoutOptions.Center,
				},
			},
		},
	};

	public static Border PreviewButton(string description) => new()
	{
		WidthRequest = 36,
		HeightRequest = 36,
		BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = 18 },
		Content = new IconView { Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16 },
		AutomationId = description,
	};

	public static void AddTap(View view, Func<Task> tapped)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await tapped();
		view.GestureRecognizers.Add(tap);
	}
}
