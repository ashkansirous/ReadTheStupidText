using DinoReader.Core;

namespace DinoReader;

/// <summary>The two stacked layers a page is drawn into: boxes underneath, text on top.</summary>
public sealed record PageLayers(AbsoluteLayout Boxes, AbsoluteLayout Text);

/// <summary>Lays out and draws the pages of one reading mode for one card size (mock screens 3 and 4).</summary>
public interface IPageRenderer
{
	int PageCount { get; }

	/// <summary>Whether the page shown is zoomed in, so a swipe pans it instead of turning the page.</summary>
	bool IsZoomed => false;

	double CornerRadius { get; }

	/// <summary>Lays the story out for a card of this size (dp). Call before drawing. Pure geometry with
	/// no UI objects, so it may run on a worker thread.</summary>
	void Compose(Size card);

	/// <summary>Re-colours: the layout stays, the next draw uses the new theme.</summary>
	void SetTheme(ReadingTheme theme);

	/// <summary>The page holding the sentence, or -1. A sentence that runs over several pages is on the one
	/// holding <paramref name="position"/> (a story index inside it) when given, else on its first page.</summary>
	int PageOf(int sentence, int position = -1);

	/// <summary>The story index of the page's first word, or -1.</summary>
	int FirstPositionOf(int page);

	Color CardColour(int page);

	/// <summary>The first sentence on the page, or -1.</summary>
	int FirstSentenceOf(int page);

	/// <summary>Draws the page's lines into the layers (clearing what was there) with no sentence marked yet.</summary>
	void DrawPage(PageLayers layers, int page);

	/// <summary>Marks the sentence being spoken on the page that is drawn.</summary>
	void Mark(PageLayers layers, int page, int sentence);

	/// <summary>The sentence under a point of the card (dp), or -1.</summary>
	int SentenceAt(int page, Point point);
}

/// <summary>The type metrics of the reading pages (Dino Reader v2 mock: Andika 25 on both modes).</summary>
public static class ReadingMetrics
{
	public const double FontSize = 25;
	public const double PicturePaddingX = 22;
	public const double PicturePaddingY = 24;
	public const double PictureLineHeight = FontSize * 1.55;
	public const double PictureLineGap = 3;
	public const double PictureOutset = 6;
	public const double PictureCorner = 24;
	public const double TextPadding = 22;
	public const double TextLineHeight = FontSize * 1.6;
	public const double TextCorner = 26;
	public const double BoxCorner = 9;
	public const double BoxStrokeWidth = 2;

	// A real page (mock screen 2): a 6 dp card, the picture inset 18/18/16, each line's box reaching 6 dp
	// sideways and a touch above and below so it covers the letters of a slightly drifted OCR box.
	public const double ScanCorner = 6;
	public const double ScanPaddingX = 18;
	public const double ScanPaddingTop = 18;
	public const double ScanPaddingBottom = 16;
	public const double ScanBoxOutsetX = 6;
	public const double ScanBoxOutsetY = 2;
	public const double MaxZoom = 4;

	// Lines are wrapped a little short of the card's text width, so a label that draws a hair wider
	// than it measured still fits instead of being clipped.
	public const double WrapSlack = 0.96;

	public static Label LineLabel(string font, double height, double width) => new()
	{
		FontFamily = font,
		FontSize = FontSize,
		FontAutoScalingEnabled = false,
		LineBreakMode = LineBreakMode.NoWrap,
		VerticalTextAlignment = TextAlignment.Center,
		HeightRequest = height,
		WidthRequest = width,
		InputTransparent = true,
	};
}

/// <summary>Widens every measurement by a fixed factor, which wraps lines earlier by the inverse.</summary>
public sealed class ScaledMeasurer(ITextMeasurer inner, double factor) : ITextMeasurer
{
	public double Width(string text) => inner.Width(text) * factor;
}
