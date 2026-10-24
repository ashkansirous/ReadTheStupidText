using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The parts of the Record screen (Slice 111, mock frames 44 and 45), built as plain views so <see cref="RecordPage"/> only decides which to show.</summary>
internal static class RecordViews
{
	private const double RoundButton = 104;
	private const double RecordEdgeDrop = 8;
	private const double RecordRing = 8;
	private const double StopSquare = 34;
	private const double StopCorner = 8;
	private const double NavSize = 52;
	private const double DotSize = 9;
	private const double CurrentDotWidth = 26;
	private const double DotGap = 6;
	private const int MaxDots = 14;
	private const double CardRadius = 16;
	private const double PageRadius = 14;
	private const double PlaySize = 44;

	public static Border PageView(string? picturePath, string text, string language)
	{
		View content = picturePath is not null
			? new Image { Source = ImageSource.FromFile(picturePath), Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center }
			: TextPage(text, language);
		return new Border
		{
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = PageRadius },
			Content = content,
		};
	}

	// A page with no picture is shown as the child sees it, its words on a white card.
	private static View TextPage(string text, string language) => new Border
	{
		StrokeThickness = 0,
		BackgroundColor = Colors.White,
		Padding = new Thickness(16),
		StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
		Content = new ScrollView
		{
			Content = new Label
			{
				Text = text, FontFamily = DinoFonts.ReadingFor(language), FontSize = 20, LineHeight = 1.5,
				FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			},
		},
	};

	public static Border ScriptCard() => new()
	{
		StrokeThickness = 0,
		BackgroundColor = Colors.White,
		Padding = new Thickness(14, 12),
		StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
		Content = new Label { Text = AppResources.Rec_Script, FontFamily = DinoFonts.Reading, FontSize = 17, LineHeight = 1.45 },
	};

	/// <summary>The "Your recording" card (frame 45): play, its length and language, Re-record and delete.</summary>
	public static Border RecordedCard(PageRecording recording, bool playing, Func<Task> play, Func<Task> redo, Func<Task> delete)
	{
		var row = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(PlayButton(playing, play), 0);
		row.Add(RecordedTexts(recording), 1);
		row.Add(RedoButton(redo), 2);
		row.Add(DeleteButton(delete), 3);
		return new Border
		{
			StrokeThickness = 0,
			BackgroundColor = Colors.White,
			Padding = new Thickness(12, 10),
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			Content = row,
		};
	}

	private static VerticalStackLayout RecordedTexts(PageRecording recording) => new()
	{
		VerticalOptions = LayoutOptions.Center,
		Children =
		{
			new Label { Text = AppResources.Rec_Yours, FontSize = 15, FontAttributes = FontAttributes.Bold },
			new Label
			{
				Text = AppResources.Format(AppResources.Rec_Detail, StoryRecordings.Clock(TimeSpan.FromSeconds(recording.Seconds)), LanguageNames.NativeName(recording.Language)),
				FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted),
			},
		},
	};

	private static Border PlayButton(bool playing, Func<Task> play)
	{
		var button = new Border
		{
			WidthRequest = PlaySize, HeightRequest = PlaySize, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = PlaySize / 2 },
			Content = new IconView { Kind = playing ? IconKind.Pause : IconKind.Play, Filled = true, Size = 16, Color = DinoTokens.Get(DinoTokens.Brand) },
		};
		SemanticProperties.SetDescription(button, playing ? ReadingText.Pause : AppResources.Rec_Play);
		CastViews.AddTap(button, play);
		return button;
	}

	private static Border RedoButton(Func<Task> redo)
	{
		var button = new Border
		{
			HeightRequest = PlaySize, Padding = new Thickness(10, 0), StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new Label
			{
				Text = AppResources.Rec_Redo, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
				VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(button, AppResources.Rec_Redo);
		CastViews.AddTap(button, redo);
		return button;
	}

	private static Border DeleteButton(Func<Task> delete)
	{
		var button = new Border
		{
			WidthRequest = PlaySize, HeightRequest = PlaySize, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Trash, Size = 20, StrokeWidth = 2.2, Color = DinoTokens.Get(DinoTokens.Danger) },
		};
		SemanticProperties.SetDescription(button, AppResources.Rec_Delete);
		CastViews.AddTap(button, delete);
		return button;
	}

	/// <summary>The big red button (frame 44): a microphone to start, a white square to stop, with its caption underneath.</summary>
	public static VerticalStackLayout RecordButton(bool recording, Func<Task> tapped)
	{
		var front = Circle(RoundButton, DinoTokens.Get(DinoTokens.Danger));
		front.Content = recording
			? new Border
			{
				WidthRequest = StopSquare, HeightRequest = StopSquare, StrokeThickness = 0, BackgroundColor = Colors.White,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
				StrokeShape = new RoundRectangle { CornerRadius = StopCorner },
			}
			: new IconView { Kind = IconKind.Mic, Color = Colors.White, Size = 40, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		string caption = recording ? AppResources.Rec_Stop : AppResources.Rec_Start;
		SemanticProperties.SetDescription(front, caption);
		CastViews.AddTap(front, tapped);
		return new VerticalStackLayout
		{
			Spacing = 10,
			HorizontalOptions = LayoutOptions.Center,
			Children = { LayeredButton(front, recording), new Label { Text = caption, FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalOptions = LayoutOptions.Center } },
		};
	}

	// The button's lower edge (and, while recording, the ring around it) as in the mock: shadow 0 8px 0 and an 8px ring.
	private static AbsoluteLayout LayeredButton(Border front, bool ring)
	{
		var layout = new AbsoluteLayout { WidthRequest = RoundButton, HeightRequest = RoundButton + RecordEdgeDrop };
		if (ring)
		{
			Border halo = Circle(RoundButton + 2 * RecordRing, DinoTokens.Get(DinoTokens.RecordHalo));
			halo.InputTransparent = true;
			AbsoluteLayout.SetLayoutBounds(halo, new Rect(-RecordRing, -RecordRing, RoundButton + 2 * RecordRing, RoundButton + 2 * RecordRing));
			layout.Add(halo);
		}

		Border edge = Circle(RoundButton, DinoTokens.Get(DinoTokens.RecordEdge));
		edge.InputTransparent = true;
		AbsoluteLayout.SetLayoutBounds(edge, new Rect(0, RecordEdgeDrop, RoundButton, RoundButton));
		AbsoluteLayout.SetLayoutBounds(front, new Rect(0, 0, RoundButton, RoundButton));
		layout.Add(edge);
		layout.Add(front);
		return layout;
	}

	/// <summary>The red dot and the running time above the Stop button.</summary>
	public static HorizontalStackLayout RecordingLabel(Label time) => new()
	{
		Spacing = 8,
		HorizontalOptions = LayoutOptions.Center,
		Children =
		{
			new Border { WidthRequest = 10, HeightRequest = 10, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Danger), VerticalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = 5 } },
			time,
		},
	};

	/// <summary>Page dots (frame 45): green for a recorded page, the current one wide; none for a book with too many pages to read as dots.</summary>
	public static HorizontalStackLayout Dots(int count, int current, Func<int, bool> recorded)
	{
		var dots = new HorizontalStackLayout { Spacing = DotGap, HorizontalOptions = LayoutOptions.Center, InputTransparent = true };
		for (int i = 0; i < count && count <= MaxDots; i++)
		{
			Color colour = recorded(i) ? DinoTokens.Get(DinoTokens.RecordedDot) : i == current ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.DotIdle);
			dots.Children.Add(new Border
			{
				WidthRequest = i == current ? CurrentDotWidth : DotSize, HeightRequest = DotSize, StrokeThickness = 0, BackgroundColor = colour,
				StrokeShape = new RoundRectangle { CornerRadius = DotSize / 2 },
			});
		}

		return dots;
	}

	/// <summary>The previous-page circle and the "Next page" button (frame 45); on the last page the button says Done.</summary>
	public static Grid NavRow(bool hasPrevious, bool isLast, Func<Task> previous, Func<Task> next)
	{
		var row = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		var back = new Border
		{
			WidthRequest = NavSize, HeightRequest = NavSize, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			Opacity = hasPrevious ? 1 : 0.4, StrokeShape = new RoundRectangle { CornerRadius = NavSize / 2 },
			Content = new IconView { Kind = IconKind.Back, Size = 22, StrokeWidth = 2.6, Color = DinoTokens.Get(DinoTokens.Brand) },
		};
		SemanticProperties.SetDescription(back, AppResources.Rec_Previous);
		CastViews.AddTap(back, () => hasPrevious ? previous() : Task.CompletedTask);
		row.Add(back, 0);
		row.Add(NextButton(isLast, next), 1);
		return row;
	}

	private static Grid NextButton(bool isLast, Func<Task> next)
	{
		string text = isLast ? AppResources.Common_Done : AppResources.Rec_Next;
		var edge = new Border { HeightRequest = NavSize, Margin = new Thickness(0, 5, 0, 0), StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.CoralEdge), StrokeShape = new RoundRectangle { CornerRadius = NavSize / 2 }, InputTransparent = true };
		var front = new Border
		{
			HeightRequest = NavSize, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Coral), VerticalOptions = LayoutOptions.Start,
			StrokeShape = new RoundRectangle { CornerRadius = NavSize / 2 },
			Content = new Label { Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(front, text);
		CastViews.AddTap(front, next);
		return new Grid { Children = { edge, front }, HeightRequest = NavSize + 5 };
	}

	private static Border Circle(double size, Color fill) => new()
	{
		WidthRequest = size, HeightRequest = size, StrokeThickness = 0, BackgroundColor = fill,
		StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
	};
}
