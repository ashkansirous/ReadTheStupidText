using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The picture files the intro's step art is drawn from: the shelf's own starter books (Batch 32 mock: "the pictures are the starter-book images"), so nothing extra is bundled. A null file draws a plain tinted block.</summary>
public sealed record IntroPictures(IReadOnlyList<string?> Covers, string? RecordPage, IReadOnlyList<string?> AddPhotos)
{
	private const int CoverCount = 3;
	private const int PhotoCount = 2;

	public static readonly IntroPictures None = new([null, null, null], null, [null, null]);

	/// <summary>Reads the shelf and picks the art: three covers, a page of the shortest starter, two pages of another book.</summary>
	public static async Task<IntroPictures> LoadAsync(IStoryLibrary library)
	{
		try
		{
			IReadOnlyList<Story> stories = await library.ListAsync();
			string?[] covers = Enumerable.Range(0, CoverCount).Select(i => CoverOf(library, stories, i)).ToArray();
			Story? shortest = ShortestOf(stories);
			Story? other = stories.FirstOrDefault(s => s.Id != shortest?.Id && PagePictures(s).Count >= PhotoCount);
			string? record = shortest is null ? null : PagePictures(shortest).Select(f => library.TryResolveFile(shortest.Id, f)).FirstOrDefault(p => p is not null);
			string?[] photos = other is null ? [null, null] : PagePictures(other).Take(PhotoCount).Select(f => library.TryResolveFile(other.Id, f)).ToArray();
			return new IntroPictures(covers, record, photos);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not pick the intro pictures: {ex}");
			return None;
		}
	}

	/// <summary>The shortest book that came with the app or the store, else the shortest book at all; null on an empty shelf.</summary>
	public static async Task<Story?> ShortestStarterAsync(IStoryLibrary library) => ShortestOf(await library.ListAsync());

	private static Story? ShortestOf(IReadOnlyList<Story> stories)
	{
		IEnumerable<Story> starters = stories.Where(s => UsageEvent.ShelfId(s.SourceId) is { } id && StarterStories.IsCatalogueId(id));
		IEnumerable<Story> pool = starters.Any() ? starters : stories;
		return pool.Where(s => s.Pages.Count > 0).OrderBy(s => s.Pages.Count).FirstOrDefault();
	}

	// A book with no cover picture of its own shows its first page picture instead.
	private static string? CoverOf(IStoryLibrary library, IReadOnlyList<Story> stories, int index)
	{
		if (stories.Count == 0)
		{
			return null;
		}

		Story story = stories[index % stories.Count];
		return library.TryResolveFile(story.Id, story.CoverImageFile ?? PagePictures(story).FirstOrDefault());
	}

	private static List<string> PagePictures(Story story) => story.Pages.Select(p => p.ImageFile).OfType<string>().ToList();
}

/// <summary>The 260 px picture block of each intro step (Batch 32 mock frames 74-76): pictures on a tinted ellipse with a round badge.</summary>
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
	private static readonly Color ReadTint = Color.FromArgb("#DCE3FF");
	private static readonly Color ReadBadge = Color.FromArgb("#5B57E8");
	private static readonly Color StoreTint = Color.FromArgb("#DFF5E3");
	private static readonly Color RecordTint = Color.FromArgb("#FFE3D9");
	private static readonly Color AddTint = Color.FromArgb("#FFF0C2");
	private static readonly Color StoreBadge = Color.FromArgb("#4FC060");
	private static readonly Color RecordBadge = Color.FromArgb("#FF7A59");
	private static readonly Color AddBadge = Color.FromArgb("#5B57E8");

	public static View Build(string step, IntroPictures pictures, bool windows) => step switch
	{
		IntroSteps.Places => Places(pictures),
		IntroSteps.Read => Read(pictures),
		IntroSteps.Store => Store(pictures),
		IntroSteps.Record => Record(pictures),
		_ => Add(pictures, windows),
	};

	// Batch 43 frame 101: two small phones tilted -4 and +4 degrees, the kids' shelf (cream, a cover) and the grown-ups' corner (sand, an ink bar with an amber lock).
	private static AbsoluteLayout Places(IntroPictures pictures)
	{
		AbsoluteLayout art = Canvas(Side, PhoneHeight + 20);
		Place(art, MiniPhone(DinoTokens.Get(DinoTokens.Cream), header: null, Picture(pictures.Covers[0], 10)), 6, 10, PhoneWidth, PhoneHeight, rotation: -4);
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

	// Batch 43 frame 101b: a page of a starter book with a play badge.
	private static AbsoluteLayout Read(IntroPictures pictures)
	{
		AbsoluteLayout art = Canvas(NarrowSide, Side);
		Place(art, Ellipse(ReadTint), 10, 0, NarrowSide - 20, Side);
		Place(art, Picture(pictures.RecordPage, 18), 25, 17, 170, 226);
		Place(art, Badge(ReadBadge, IconKind.Play, 2.4), NarrowSide - 6 - BadgeSize, Side - 6 - BadgeSize, BadgeSize, BadgeSize);
		return art;
	}

	private static AbsoluteLayout Store(IntroPictures pictures)
	{
		AbsoluteLayout art = Canvas(Side, Side);
		Place(art, Ellipse(StoreTint), 10, 20, 240, 220);
		Place(art, Picture(pictures.Covers[1], 14, rotation: -8), 14, 56, 118, 158);
		Place(art, Picture(pictures.Covers[2], 14, rotation: 8), 128, 56, 118, 158);
		Place(art, Picture(pictures.Covers[0], 16), 64, 42, 132, 176);
		Place(art, Badge(StoreBadge, IconKind.Download, 2.8), Side - 52 - BadgeSize, Side - 14 - BadgeSize, BadgeSize, BadgeSize);
		return art;
	}

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
