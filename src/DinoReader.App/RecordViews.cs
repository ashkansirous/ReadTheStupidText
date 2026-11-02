using DinoReader.Core;
using DinoReader.Resources.Strings;
using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The parts of the Record screen (Slice 111, mock frames 44 and 45), built as plain views so <see cref="RecordPage"/> only decides which to show.</summary>
internal static class RecordViews
{
	private const double RoundButton = 104;
	private const double MicShare = 40.0 / 104;
	private const double RailButtonHeight = 40;
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
	private const double PictureBand = 210;
	private const double PictureShare = 0.4;

	/// <summary>
	/// AutomationIds of the big button (its microphone and its stop square), the running clock and the rail's Done, for the scripted check. They are set only on the sideways views
	/// (Slice 232 part C), so a flow that looks for them fails on the upright layout.
	/// </summary>
	internal const string RecordButtonId = "RecordButton";
	internal const string StopButtonId = "RecordStopButton";
	internal const string ClockId = "RecordClock";
	internal const string RailDoneId = "RecordRailDone";

	/// <summary>The AutomationId of the page's words label, for the scripted check.</summary>
	internal const string PageWordsId = "RecordPageWords";

	/// <summary>The page as the child sees it (Slice 231, mock frame 106): a picture band with the page's words on a white card under it; the words alone when there is no picture, the picture alone when there are no words.</summary>
	public static Border PageView(string? picturePath, StoryPage page, string language)
	{
		string words = PageWords(page);
		Debug.WriteLine($"Dino Reader Record page layout: {(picturePath is null ? "words only" : words.Length == 0 ? "picture only" : "picture and words")} ({words.Length} characters)");
		View content = (picturePath, words) switch
		{
			(null, _) => TextPage(words, language),
			(not null, "") => PictureOnly(picturePath),
			_ => PictureAndWords(picturePath, words, language),
		};
		return new Border
		{
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = PageRadius },
			Content = content,
		};
	}

	/// <summary>The words of a page: its <c>Text</c>, the same text the reading screen splits into sentences and reads, so the two cannot disagree.</summary>
	internal static string PageWords(StoryPage page) => page.Text.Trim();

	/// <summary>The page's picture fitted whole in its own column, rounded (Slice 232 part C, mock frames 116 and 129).</summary>
	internal static View PictureColumn(string picturePath) => new Border
	{
		StrokeThickness = 0,
		BackgroundColor = Colors.Transparent,
		StrokeShape = new RoundRectangle { CornerRadius = PageRadius },
		Content = PictureOnly(picturePath),
	};

	private static View PictureOnly(string picturePath) => new Image { Source = ImageSource.FromFile(picturePath), Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center };

	// Frame 106: a picture band (cropped from the top, never taller than a share of the space) over the words card, which scrolls when the page is long.
	private static View PictureAndWords(string picturePath, string words, string language)
	{
		double aspect = PageImageSize.Read(picturePath) is { Height: > 0 } size ? size.Width / size.Height : 0;
		var image = new Image { Source = ImageSource.FromFile(picturePath), Aspect = Aspect.AspectFill };
		var host = new AbsoluteLayout { Children = { image } };
		AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(image, new Rect(0, 0, 1, 1));
		host.SizeChanged += (_, _) => CoverTop(host, image, aspect);
		var band = new Border
		{
			HeightRequest = PictureBand, StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = PageRadius },
			Content = host,
		};
		var grid = new Grid { RowSpacing = 10, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		grid.SizeChanged += (_, _) => band.HeightRequest = Math.Min(PictureBand, grid.Height * PictureShare);
		grid.Add(band, 0, 0);
		grid.Add(TextPage(words, language), 0, 1);
		return grid;
	}

	// The picture covers the band and keeps its top (the books' art leaves the bottom free for words); an unknown shape stays filled and cropped.
	private static void CoverTop(AbsoluteLayout host, Image image, double aspect)
	{
		var frame = PictureTextBook.CoverFromTop(host.Width, host.Height, aspect);
		AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(image, new Rect(frame.X, frame.Y, frame.Width, frame.Height));
		image.Aspect = aspect > 0 ? Aspect.Fill : Aspect.AspectFill;
	}

	// A page with no picture is shown as the child sees it, its words on a white card.
	// Held sideways (Slice 232 part C) the same card takes the shape's own words size (RecordLayout.WordsSize).
	internal static View TextPage(string text, string language, double fontSize = RecordLayout.UprightWordsSize) => new Border
	{
		StrokeThickness = 0,
		BackgroundColor = Colors.White,
		Padding = new Thickness(16, 14),
		StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
		Content = new ScrollView
		{
			Content = new Label
			{
				AutomationId = PageWordsId,
				Text = text, FontFamily = DinoFonts.ReadingFor(language), FontSize = fontSize, LineHeight = 1.5,
				FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			},
		},
	};

	/// <summary>The AutomationId of the "recordings stay on this phone" line, for the scripted check.</summary>
	internal const string StayNoteId = "RecordingsStayNote";

	/// <summary>The line that says where recordings go (Slice 230, mock frames 106 and 129): they stay on this phone or tablet, Dino never uploads or backs them up, so the grown-up keeps a copy.</summary>
	internal static string StayNoteText() => StayNoteFor(DeviceInfo.Idiom);

	private static string StayNoteFor(DeviceIdiom idiom) =>
		idiom == DeviceIdiom.Phone ? AppResources.Recordings_StayHere_Phone
		: idiom == DeviceIdiom.Tablet ? AppResources.Recordings_StayHere_Tablet
		: AppResources.Recordings_StayHere_Device;

	/// <summary>The line as a small label, 12.5 px in the body colour, for the Record page and the intro's record step.</summary>
	public static Label StayNote() => new()
	{
		AutomationId = StayNoteId, Text = StayNoteText(), FontSize = 12.5, LineHeight = 1.4,
		TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 0),
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
	public static VerticalStackLayout RecordButton(bool recording, Func<Task> tapped, double size = RoundButton, bool sideways = false)
	{
		var front = Circle(size, DinoTokens.Get(DinoTokens.Danger));
		if (sideways)
		{
			front.AutomationId = recording ? StopButtonId : RecordButtonId;
		}
		front.Content = recording
			? new Border
			{
				WidthRequest = StopSquare, HeightRequest = StopSquare, StrokeThickness = 0, BackgroundColor = Colors.White,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
				StrokeShape = new RoundRectangle { CornerRadius = StopCorner },
			}
			: new IconView { Kind = IconKind.Mic, Color = Colors.White, Size = size * MicShare, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		string caption = recording ? AppResources.Rec_Stop : AppResources.Rec_Start;
		SemanticProperties.SetDescription(front, caption);
		CastViews.AddTap(front, tapped);
		return new VerticalStackLayout
		{
			Spacing = 10,
			HorizontalOptions = LayoutOptions.Center,
			Children = { LayeredButton(front, recording, size), new Label { Text = caption, FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalOptions = LayoutOptions.Center } },
		};
	}

	// The button's lower edge (and, while recording, the ring around it) as in the mock: shadow 0 8px 0 and an 8px ring.
	private static AbsoluteLayout LayeredButton(Border front, bool ring, double size)
	{
		var layout = new AbsoluteLayout { WidthRequest = size, HeightRequest = size + RecordEdgeDrop };
		if (ring)
		{
			Border halo = Circle(size + 2 * RecordRing, DinoTokens.Get(DinoTokens.RecordHalo));
			halo.InputTransparent = true;
			AbsoluteLayout.SetLayoutBounds(halo, new Rect(-RecordRing, -RecordRing, size + 2 * RecordRing, size + 2 * RecordRing));
			layout.Add(halo);
		}

		Border edge = Circle(size, DinoTokens.Get(DinoTokens.RecordEdge));
		edge.InputTransparent = true;
		AbsoluteLayout.SetLayoutBounds(edge, new Rect(0, RecordEdgeDrop, size, size));
		AbsoluteLayout.SetLayoutBounds(front, new Rect(0, 0, size, size));
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

	/// <summary>The Done (or Continue, from the intro) pill at the top of the phone's rail (frame 116); it leaves the screen the way the header's button does.</summary>
	public static Border RailDone(bool isContinue, Func<Task> done)
	{
		string text = isContinue ? AppResources.Common_Continue : AppResources.Common_Done;
		var pill = new Border
		{
			AutomationId = RailDoneId, HeightRequest = RailButtonHeight, Padding = new Thickness(18, 0), StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.Brand), HorizontalOptions = LayoutOptions.End,
			StrokeShape = new RoundRectangle { CornerRadius = RailButtonHeight / 2 },
			Content = new Label { Text = text, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(pill, text);
		CastViews.AddTap(pill, done);
		return pill;
	}

	/// <summary>The phone rail's page arrows: previous and next, two small circles side by side at the foot of the rail (on the last page next leaves the screen, as "Done" does upright).</summary>
	public static HorizontalStackLayout RailNav(bool hasPrevious, bool isLast, Func<Task> previous, Func<Task> next)
	{
		var back = RailArrow(IconKind.Back, AppResources.Rec_Previous, hasPrevious ? 1 : 0.4);
		CastViews.AddTap(back, () => hasPrevious ? previous() : Task.CompletedTask);
		var forward = isLast ? RailArrow(IconKind.Check, AppResources.Common_Done, 1) : RailArrow(IconKind.Chevron, AppResources.Rec_Next, 1);
		CastViews.AddTap(forward, next);
		return new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, Children = { back, forward } };
	}

	private static Border RailArrow(IconKind kind, string description, double opacity)
	{
		var arrow = new Border
		{
			WidthRequest = RailButtonHeight, HeightRequest = RailButtonHeight, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			Opacity = opacity, StrokeShape = new RoundRectangle { CornerRadius = RailButtonHeight / 2 },
			Content = new IconView { Kind = kind, Size = 18, StrokeWidth = 2.6, Color = DinoTokens.Get(DinoTokens.Brand) },
		};
		SemanticProperties.SetDescription(arrow, description);
		return arrow;
	}

	private static Border Circle(double size, Color fill) => new()
	{
		WidthRequest = size, HeightRequest = size, StrokeThickness = 0, BackgroundColor = fill,
		StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
	};
}
