using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The book the intro opens. Every step's art is drawn from art that ships in the app (Dino on plain colour blocks, Slice 261; the Record step shows the Dino icon, Slice 325 part E), so no book page is read for it.
/// </summary>
public static class IntroPictures
{
	/// <summary>The book the intro opens (Slice 261): the first one the grown-up picked on the first-run book page, else the shortest catalogue book, else the shortest book at all; null on an empty shelf.</summary>
	public static async Task<Story?> FirstBookAsync(IStoryLibrary library) => FirstOf(await library.ListAsync());

	private static Story? FirstOf(IReadOnlyList<Story> stories)
	{
		string? picked = FirstBooksGate.FirstPick;
		Story? chosen = picked is null ? null : stories.FirstOrDefault(s => string.Equals(s.SourceId, picked, StringComparison.OrdinalIgnoreCase) && s.Pages.Count > 0);
		return chosen ?? ShortestOf(stories);
	}

	private static Story? ShortestOf(IReadOnlyList<Story> stories)
	{
		IEnumerable<Story> catalogue = stories.Where(s => UsageEvent.ShelfId(s.SourceId) is { } id && StarterStories.IsCatalogueId(id));
		IEnumerable<Story> pool = catalogue.Any() ? catalogue : stories;
		return pool.Where(s => s.Pages.Count > 0).OrderBy(s => s.Pages.Count).ThenBy(s => s.Created).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault(); // ties go to the book added first, not to the shelf order
	}
}

/// <summary>The 260 px picture block of each intro step (Batch 32 mock frames 74-76, Batch 47 frame 168): pictures on a tinted ellipse with a round badge; the Places and Read steps draw Dino on plain colour blocks.</summary>
public static class IntroArt
{
	private const double Side = 260;
	private const double BadgeSize = 60;
	private const double PlacesWidth = 250;
	private const double PlacesHeight = 210;
	private const double MiniPhoneWidth = 112;
	private const double MiniPhoneHeight = 196;
	private const double MiniPhoneBorder = 4;
	private const double RecordDinoSize = 130;
	private const double ReadPhoneWidth = 150;
	private const double ReadPhoneHeight = 240;
	private const double ReadPhoneBorder = 5;
	private const double ReadPhoneRadius = 22;
	private const double DinoSize = 64;
	private const double PlayDiscSize = 34;
	private const double CaptionSize = 9;
	private const int CaptionMaxLines = 4;
	private static readonly Color SkyBlock = Color.FromArgb("#7FD0FF");
	private static readonly Color ReadAura = Color.FromArgb("#FFE9B8");
	private static readonly Color CaptionHighlight = Color.FromArgb("#FFE27A");
	private static readonly Color RecordTint = Color.FromArgb("#FFE3D9");
	private static readonly Color RecordBadge = Color.FromArgb("#FF7A59");

	public static View Build(string step) => step switch
	{
		IntroSteps.Places => Places(),
		IntroSteps.Read => Read(),
		_ => Record(),
	};

	// Onboarding mock frame O6 (Slice 325 part E): two mini phones (112 x 196, 4 px ink border, radius 18) tilted -4 and +4 degrees, redrawn from the new screens: the kids' shelf (cream; the Dino icon, a flag and a
	// grown-ups pill over colour-block covers) and the grown-ups' corner (an ink header with the coral pill over covers and a three-tab bar). Covers are colour blocks because no book art ships in the app.
	private static AbsoluteLayout Places()
	{
		AbsoluteLayout art = Canvas(PlacesWidth, PlacesHeight);
		Place(art, MiniPhone(DinoTokens.Get(DinoTokens.Cream), MiniShelfContent()), 12, 7, MiniPhoneWidth, MiniPhoneHeight, rotation: -4);
		Place(art, MiniPhone(DinoTokens.Get(DinoTokens.Cream), MiniCornerContent()), PlacesWidth - MiniPhoneWidth - 12, 7, MiniPhoneWidth, MiniPhoneHeight, rotation: 4);
		return art;
	}

	private static Border MiniPhone(Color fill, View content) => new()
	{
		BackgroundColor = fill,
		Stroke = DinoTokens.Get(DinoTokens.Ink),
		StrokeThickness = MiniPhoneBorder,
		StrokeShape = new RoundRectangle { CornerRadius = 18 },
		Content = content,
	};

	// The shelf: Dino's icon, a flag and the grown-ups pill in a header row, then the covers.
	private static VerticalStackLayout MiniShelfContent()
	{
		var icon = new Border { WidthRequest = 18, HeightRequest = 18, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 5 }, Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill } };
		var pill = new Border { WidthRequest = 30, HeightRequest = 12, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 6 }, BackgroundColor = DinoTokens.Get(DinoTokens.Ink), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
		var header = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 6, Padding = new Thickness(8, 10, 8, 4) };
		header.Add(icon, 0, 0);
		header.Add(new FlagRing("en", 14, 1, Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.12)), 1, 0);
		header.Add(pill, 2, 0);
		return new VerticalStackLayout { Spacing = 6, Children = { header, CoverBlocks(8) } };
	}

	// The corner: an ink header with the coral pill, covers, and the three-tab bar at the foot (the first tab on).
	private static Grid MiniCornerContent()
	{
		var coral = new Border { WidthRequest = 36, HeightRequest = 12, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 6 }, BackgroundColor = DinoTokens.Get(DinoTokens.Coral), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 8, 0) };
		var header = new Border { BackgroundColor = DinoTokens.Get(DinoTokens.Ink), StrokeThickness = 0, HeightRequest = 34, Content = coral };
		var tabs = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], BackgroundColor = Colors.White, HeightRequest = 26, VerticalOptions = LayoutOptions.End };
		for (int i = 0; i < 3; i++)
		{
			tabs.Add(new Border { WidthRequest = 20, HeightRequest = 6, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 3 }, BackgroundColor = DinoTokens.Get(i == 0 ? DinoTokens.Brand : DinoTokens.DotIdle), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }, i, 0);
		}

		var body = new VerticalStackLayout { Spacing = 6, Children = { header, new ContentView { Padding = new Thickness(0, 4, 0, 0), Content = CoverBlocks(8) } } };
		return new Grid { Children = { body, tabs } };
	}

	// Three colour-block covers (#FFC24D, #7FD0FF, #A9A2FF) in a two-wide grid.
	private static Grid CoverBlocks(double inset)
	{
		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)], ColumnSpacing = 6, RowSpacing = 6, Padding = new Thickness(inset, 0) };
		string[] colours = ["#FFC24D", "#7FD0FF", "#A9A2FF"];
		for (int i = 0; i < colours.Length; i++)
		{
			grid.Add(new Border { HeightRequest = 48, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 7 }, BackgroundColor = Color.FromArgb(colours[i]) }, i % 2, i / 2);
		}

		return grid;
	}

	// Batch 47 frame 168: a 150x240 mini phone (5 dp ink border) with Dino on a sky-blue block, the caption as lines with one highlighted, and a coral play disc; a warm ellipse behind.
	private static AbsoluteLayout Read()
	{
		AbsoluteLayout art = Canvas(Side, Side);
		Place(art, Ellipse(ReadAura), 10, 10, Side - 20, Side - 20);
		Place(art, ReadPhone(), (Side - ReadPhoneWidth) / 2, (Side - ReadPhoneHeight) / 2, ReadPhoneWidth, ReadPhoneHeight);
		return art;
	}

	private static Border ReadPhone()
	{
		var screen = new Grid { RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)], BackgroundColor = DinoTokens.Get(DinoTokens.Cream) };
		screen.Add(DinoBlock(0), 0, 0);
		screen.Add(Caption(), 0, 1);
		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Cream), Stroke = DinoTokens.Get(DinoTokens.Ink), StrokeThickness = ReadPhoneBorder,
			StrokeShape = new RoundRectangle { CornerRadius = ReadPhoneRadius }, Content = screen,
		};
	}

	// The caption under Dino: the real words of the story ("Dino opened the book. “Once upon a time…”", a string of every language) on the highlight colour, and the coral play disc.
	private static Grid Caption()
	{
		var words = new FormattedString { Spans = { new Span { Text = AppResources.Intro_ReadCaption, BackgroundColor = CaptionHighlight, TextColor = DinoTokens.Get(DinoTokens.Ink), FontSize = CaptionSize } } };
		var lines = new Label { FormattedText = words, VerticalOptions = LayoutOptions.Center, MaxLines = CaptionMaxLines, LineBreakMode = LineBreakMode.TailTruncation };
		var disc = new Border
		{
			WidthRequest = PlayDiscSize, HeightRequest = PlayDiscSize, StrokeThickness = 0, StrokeShape = new Ellipse(), BackgroundColor = DinoTokens.Get(DinoTokens.Coral),
			HorizontalOptions = LayoutOptions.End, Content = new IconView { Kind = IconKind.Play, Color = Colors.White, Size = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var caption = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], Padding = new Thickness(10, 8), ColumnSpacing = 8 };
		caption.Add(lines, 0, 0);
		caption.Add(disc, 1, 0);
		return caption;
	}

	// Dino (the app icon, shipped in the app) on a plain sky-blue block.
	private static Border DinoBlock(double radius) => new()
	{
		StrokeThickness = 0, BackgroundColor = SkyBlock, StrokeShape = new RoundRectangle { CornerRadius = radius },
		Content = new Image { Source = "dino_icon", WidthRequest = DinoSize, HeightRequest = DinoSize, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
	};

	// Onboarding mock frame O8 (Slice 325 part E): Dino and a microphone on the warm ellipse. It replaces a page of the first book, so the art no longer depends on a book being on the shelf.
	private static AbsoluteLayout Record()
	{
		AbsoluteLayout art = Canvas(Side, Side);
		Place(art, Ellipse(RecordTint), 10, 10, Side - 20, Side - 20);
		var dino = new Image { Source = "dino_icon", Aspect = Aspect.AspectFit };
		Place(art, dino, (Side - RecordDinoSize) / 2, (Side - RecordDinoSize) / 2 - 8, RecordDinoSize, RecordDinoSize);
		Place(art, Badge(RecordBadge, IconKind.Mic, 2.6), Side - 30 - BadgeSize, Side - 30 - BadgeSize, BadgeSize, BadgeSize);
		return art;
	}

	private static AbsoluteLayout Canvas(double width, double height) => new() { WidthRequest = width, HeightRequest = height, HorizontalOptions = LayoutOptions.Center };

	private static void Place(AbsoluteLayout art, View view, double x, double y, double width, double height, double rotation = 0)
	{
		view.Rotation = rotation;
		AbsoluteLayout.SetLayoutBounds(view, new Rect(x, y, width, height));
		art.Children.Add(view);
	}

	private static Border Ellipse(Color fill) => new() { BackgroundColor = fill, StrokeThickness = 0, StrokeShape = new Ellipse() };

	private static Border Badge(Color fill, IconKind icon, double stroke) => new()
	{
		StrokeThickness = 0,
		BackgroundColor = fill,
		StrokeShape = new Ellipse(),
		Content = new IconView { Kind = icon, Color = Colors.White, Size = 28, StrokeWidth = stroke, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
	};
}
