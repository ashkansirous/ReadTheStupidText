using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The picture files the intro's step art is drawn from. The Places and Read steps are drawn from art that ships in the app (Dino on plain colour blocks, Slice 261, mock Batch 47 frame 168);
/// the Record and Add steps show a page of the grown-up's first book and two pages of another one from the shelf. A null file draws a plain tinted block.
/// </summary>
public sealed record IntroPictures(string? RecordPage, IReadOnlyList<string?> AddPhotos)
{
	private const int PhotoCount = 2;

	public static readonly IntroPictures None = new(null, [null, null]);

	/// <summary>Reads the shelf and picks the art: a page of the first book, two pages of another book.</summary>
	public static async Task<IntroPictures> LoadAsync(IStoryLibrary library)
	{
		try
		{
			IReadOnlyList<Story> stories = await library.ListAsync();
			Story? first = FirstOf(stories);
			Story? other = stories.FirstOrDefault(s => s.Id != first?.Id && PagePictures(s).Count >= PhotoCount);
			string? record = first is null ? null : PagePictures(first).Select(f => library.TryResolveFile(first.Id, f)).FirstOrDefault(p => p is not null);
			string?[] photos = other is null ? [null, null] : PagePictures(other).Take(PhotoCount).Select(f => library.TryResolveFile(other.Id, f)).ToArray();
			return new IntroPictures(record, photos);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not pick the intro pictures: {ex}");
			return None;
		}
	}

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
		return pool.Where(s => s.Pages.Count > 0).OrderBy(s => s.Pages.Count).FirstOrDefault();
	}

	private static List<string> PagePictures(Story story) => story.Pages.Select(p => p.ImageFile).OfType<string>().ToList();
}

/// <summary>The 260 px picture block of each intro step (Batch 32 mock frames 74-76, Batch 47 frame 168): pictures on a tinted ellipse with a round badge; the Places and Read steps draw Dino on plain colour blocks.</summary>
public static class IntroArt
{
	private const double Side = 260;
	private const double NarrowSide = 220;
	private const double BadgeSize = 60;
	private const double ShadowRadius = 20;
	private const double BorderWidth = 5;
	private const double PhoneWidth = 120;
	private const double PhoneHeight = 200;
	private const double PhoneBorder = 4;
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
	private static readonly Color AddTint = Color.FromArgb("#FFF0C2");
	private static readonly Color RecordBadge = Color.FromArgb("#FF7A59");
	private static readonly Color AddBadge = Color.FromArgb("#5B57E8");

	public static View Build(string step, IntroPictures pictures, bool windows) => step switch
	{
		IntroSteps.Places => Places(),
		IntroSteps.Read => Read(),
		IntroSteps.Record => Record(pictures),
		_ => Add(pictures, windows),
	};

	// Batch 43 frame 101: two small phones tilted -4 and +4 degrees, the kids' shelf (cream, a cover) and the grown-ups' corner (sand, an ink bar with an amber lock).
	private static AbsoluteLayout Places()
	{
		AbsoluteLayout art = Canvas(Side, PhoneHeight + 20);
		Place(art, MiniPhone(DinoTokens.Get(DinoTokens.Cream), header: null, DinoBlock(10)), 6, 10, PhoneWidth, PhoneHeight, rotation: -4);
		var bar = new Border { BackgroundColor = DinoTokens.Get(DinoTokens.Ink), StrokeThickness = 0, HeightRequest = 34, Content = new IconView { Kind = IconKind.Lock, Color = Color.FromArgb("#FFC043"), Size = 18, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(10, 0), VerticalOptions = LayoutOptions.Center } };
		Place(art, MiniPhone(DinoTokens.Get(DinoTokens.Sand), bar, null), Side - PhoneWidth - 6, 10, PhoneWidth, PhoneHeight, rotation: 4);
		return art;
	}

	private static Border MiniPhone(Color fill, View? header, View? cover)
	{
		var content = new VerticalStackLayout { Spacing = 8 };
		if (header is not null)
		{
			content.Children.Add(header);
		}

		if (cover is not null)
		{
			cover.HeightRequest = 90;
			cover.Margin = new Thickness(14, 24, 14, 0);
			content.Children.Add(cover);
		}

		return new Border
		{
			BackgroundColor = fill,
			Stroke = DinoTokens.Get(DinoTokens.Ink),
			StrokeThickness = PhoneBorder,
			StrokeShape = new RoundRectangle { CornerRadius = 18 },
			Content = content,
		};
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

	private static AbsoluteLayout Record(IntroPictures pictures)
	{
		AbsoluteLayout art = Canvas(NarrowSide, Side);
		Place(art, Ellipse(RecordTint), 10, 0, NarrowSide - 20, Side);
		Place(art, Picture(pictures.RecordPage, 18), 25, 17, 170, 226);
		Place(art, Badge(RecordBadge, IconKind.Mic, 2.6), NarrowSide - 6 - BadgeSize, Side - 6 - BadgeSize, BadgeSize, BadgeSize);
		return art;
	}

	private static AbsoluteLayout Add(IntroPictures pictures, bool windows)
	{
		AbsoluteLayout art = Canvas(NarrowSide, Side);
		Place(art, Ellipse(AddTint), 10, 0, NarrowSide - 20, Side);
		Place(art, Photo(pictures.AddPhotos[0], -6), 34, 40, 124, 166);
		Place(art, Photo(pictures.AddPhotos[1], 5), NarrowSide - 34 - 124, 54, 124, 166);
		Place(art, Badge(AddBadge, windows ? IconKind.Picture : IconKind.Camera, 2.4), NarrowSide - 30 - BadgeSize, Side - 14 - BadgeSize, BadgeSize, BadgeSize);
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

	private static Border Picture(string? file, double radius, double rotation = 0) => new()
	{
		StrokeThickness = 0,
		Rotation = rotation,
		BackgroundColor = Colors.White,
		StrokeShape = new RoundRectangle { CornerRadius = radius },
		Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 8), Radius = (float)ShadowRadius, Opacity = 0.2f },
		Content = Image(file),
	};

	// The Add step's photos: a white edge round the picture.
	private static Border Photo(string? file, double rotation)
	{
		Border photo = Picture(file, 12, rotation);
		photo.Stroke = Colors.White;
		photo.StrokeThickness = BorderWidth;
		return photo;
	}

	private static Image Image(string? file) => new() { Aspect = Aspect.AspectFill, Source = file is null ? null : ImageSource.FromFile(file) };

	private static Border Badge(Color fill, IconKind icon, double stroke) => new()
	{
		StrokeThickness = 0,
		BackgroundColor = fill,
		StrokeShape = new Ellipse(),
		Content = new IconView { Kind = icon, Color = Colors.White, Size = 28, StrokeWidth = stroke, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
	};
}
