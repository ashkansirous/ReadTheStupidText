using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The small pieces the cast screen and its merge sheet are built from (mock screens 10 and 11).</summary>
internal static class CastViews
{
	private const double ChipHeight = 32;
	private const double GenderCellHeight = 28;
	private const double SelectedShadowOpacity = 0.15;
	private static readonly Gender[] GenderChoices = [Gender.Male, Gender.Female, Gender.Unknown];

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

	/// <summary>The rounded drop-down look of a voice: its name and a down chevron.</summary>
	public static Border VoiceChip(string voiceName) => new()
	{
		HeightRequest = ChipHeight,
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
		StrokeThickness = 0,
		Padding = new Thickness(10, 0),
		StrokeShape = new RoundRectangle { CornerRadius = 10 },
		Content = new HorizontalStackLayout
		{
			Spacing = 5,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = voiceName, FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center },
				new IconView { Kind = IconKind.ChevronDown, Color = DinoTokens.Get(DinoTokens.Body), Size = 12, StrokeWidth = 1.6, VerticalOptions = LayoutOptions.Center },
			},
		},
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

	/// <summary>The Boy / Girl / ? control: a Sand track with the chosen cell raised in white.</summary>
	public static Border GenderControl(Gender selected, Func<Gender, Task> picked)
	{
		var cells = new HorizontalStackLayout();
		foreach (Gender gender in GenderChoices)
		{
			cells.Children.Add(GenderCell(gender, gender == selected, picked));
		}

		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeThickness = 0,
			Padding = 2,
			HorizontalOptions = LayoutOptions.Start,
			StrokeShape = new RoundRectangle { CornerRadius = 9 },
			Content = cells,
		};
	}

	private static Border GenderCell(Gender gender, bool selected, Func<Gender, Task> picked)
	{
		var cell = new Border
		{
			HeightRequest = GenderCellHeight,
			Padding = new Thickness(8, 0),
			StrokeThickness = 0,
			BackgroundColor = selected ? Colors.White : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 7 },
			Content = new Label
			{
				Text = CastText.GenderLabel(gender), FontSize = 12, FontAttributes = FontAttributes.Bold,
				VerticalOptions = LayoutOptions.Center,
				TextColor = DinoTokens.Get(selected ? DinoTokens.Ink : DinoTokens.Muted),
			},
		};
		if (selected)
		{
			cell.Shadow = new Shadow
			{
				Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Opacity = (float)SelectedShadowOpacity,
				Offset = new Point(0, 1), Radius = 1,
			};
		}

		SemanticProperties.SetDescription(cell, CastText.GenderLabel(gender));
		AddTap(cell, () => picked(gender));
		return cell;
	}

	public static void AddTap(View view, Func<Task> tapped)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await tapped();
		view.GestureRecognizers.Add(tap);
	}
}
