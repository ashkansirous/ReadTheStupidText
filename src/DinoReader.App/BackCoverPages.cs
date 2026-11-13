using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace DinoReader;

/// <summary>What the back cover shows (Slice 237): its lines already worded in the reading language, the book's colour and the small cover picture (a file path, or null for a colour cover).
/// Slice 359: <see cref="BackPicture"/> is the book's drawn back-cover picture (a file path); with it the credits sit over the picture's middle, and without it (Good Enough) the live page above is drawn.</summary>
public sealed record BackCoverView(IReadOnlyList<string> Lines, Color Colour, string? CoverPicture, string? BackPicture = null);

/// <summary>
/// The silent last page of a book (Slice 237): the story's own renderer with one more page after its last. The back cover has no sentences, so it is never read,
/// marked, tapped or counted by the reading; it shows in every reading mode. Pages before it are the inner renderer's, untouched.
/// </summary>
public sealed class BackCoverPages(IPageRenderer inner, BackCoverView view, string font, bool rightToLeft) : IPageRenderer
{
	/// <summary>The AutomationId of the back cover, so a UI test finds it by identity.</summary>
	public const string BackCoverId = "BackCover";

	private const double CoverWidthShare = 0.34;
	private const double CoverAspect = 0.75; // the shipped pictures are 3:4
	private const double CoverCorner = 14;
	private const double LineFontSize = 20;
	private const double MinLineFontSize = 13; // the credits shrink to this before they would clip
	private const double FontStep = 1;
	private const double AverageGlyphWidth = 0.6; // of the font size; wide enough for Cyrillic and CJK-free text, so the estimate errs towards more lines
	private const double LineGap = 8;
	private const double CoverGap = 22;
	private const double SidePadding = 24;
	private const double LineHeightFactor = 1.4;
	private const double MinPictureHeight = 72; // a shorter card drops the picture and keeps the credits
	private const double BackPictureAspect = 0.75; // the drawn back covers are 3:4
	private const double ScrimPadding = 14;
	private const float ScrimAlpha = 0.6f; // the credits' backing is the page colour at this strength: readable on a light or a dark picture, the drawing still shows through
	private const double ScrimCorner = 16;
	private const double BandTop = 0.30; // the credits stay in this band of the picture's height: the drawings keep their middle open (Slice 359)
	private const double BandBottom = 0.62;

	private ReadingTheme? _theme;
	private Size _card;
	private double _fontSize = LineFontSize;

	/// <summary>The index of the back cover page: right after the inner renderer's last.</summary>
	public int BackPage => inner.PageCount;

	public int PageCount => inner.PageCount + 1;

	public bool IsZoomed => inner.IsZoomed;

	public double CornerRadius => inner.CornerRadius;

	public void Compose(Size card)
	{
		_card = card;
		inner.Compose(card);
	}

	public void SetTheme(ReadingTheme theme)
	{
		_theme = theme;
		inner.SetTheme(theme);
	}

	public int PageOf(int sentence, int position = -1) => inner.PageOf(sentence, position);

	public int FirstPositionOf(int page) => page == BackPage ? -1 : inner.FirstPositionOf(page);

	public int FirstSentenceOf(int page) => page == BackPage ? -1 : inner.FirstSentenceOf(page);

	public Color CardColour(int page) =>
		page != BackPage ? inner.CardColour(page) : _theme is { IsBedtime: true } ? _theme.PageCard(0) : view.Colour;

	public void DrawPage(PageLayers layers, int page)
	{
		if (page != BackPage)
		{
			inner.DrawPage(layers, page);
			return;
		}

		layers.Boxes.Children.Clear();
		layers.Text.Children.Clear();
		layers.Text.Children.Add(BuildPage());
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"BACKCOVER shown lines={view.Lines.Count} picture={(view.BackPicture is null ? "live" : "drawn")}");
#endif
	}

	public void Mark(PageLayers layers, int page, int sentence)
	{
		if (page != BackPage)
		{
			inner.Mark(layers, page, sentence);
		}
	}

	public void MarkPage(PageLayers layers, int page, int first, int last)
	{
		if (page != BackPage)
		{
			inner.MarkPage(layers, page, first, last);
		}
	}

	public int SentenceAt(int page, Point point) => page == BackPage ? -1 : inner.SentenceAt(page, point);

	private View BuildPage()
	{
		if (view.BackPicture is { } drawn)
		{
			return BuildDrawnPage(drawn);
		}

		var stack = new VerticalStackLayout
		{
			Spacing = LineGap,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Fill,
			Padding = new Thickness(SidePadding, 0),
			InputTransparent = true,
		};
		_fontSize = FitFontSize(_card, SidePadding, 0);
		if (view.CoverPicture is { } file && CoverPicture(file) is { } picture)
		{
			stack.Add(picture);
		}

		foreach (string line in view.Lines)
		{
			stack.Add(LineLabel(line));
		}

		return Host(stack);
	}

	private static View Host(View content)
	{
		var host = new Grid { AutomationId = BackCoverId, InputTransparent = true, Children = { content } };
		AbsoluteLayout.SetLayoutFlags(host, AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(host, new Rect(0, 0, 1, 1));
		return host;
	}

	// Slice 359: the drawn back cover. Portrait: the picture is shown whole (3:4, centred on the card) and the credit lines sit in its open middle band (BandTop to BandBottom of the
	// PICTURE's height, not the page's) on a light strip of the page colour just behind the text. Landscape (frame 148 of Slice 237): the picture whole on the left and the lines on the
	// right on the page colour, no overlay. In every reading mode, in either direction.
	private View BuildDrawnPage(string file)
	{
		// Side by side only when the whole picture at full height still leaves at least as much width again for the credits; otherwise the credits go over the picture.
		bool sideBySide = _card.Width >= 2 * _card.Height * BackPictureAspect;
		double pictureHeight = sideBySide ? _card.Height : Math.Min(_card.Height, _card.Width / BackPictureAspect);
		var picture = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFill, InputTransparent = true, WidthRequest = pictureHeight * BackPictureAspect, HeightRequest = pictureHeight };
		return Host(sideBySide ? BuildSideBySide(picture, pictureHeight) : BuildOverPicture(picture, pictureHeight));
	}

	// The credits' band: BandTop to BandBottom of the picture, grown (kept centred on that band, up to the whole picture) when the credits at the smallest font would not fit it.
	private (double Top, double Bottom) BandFor(double pictureWidth, double pictureHeight)
	{
		double inset = SidePadding + ScrimPadding;
		double needed = CreditsHeight(_fontSize, new Size(pictureWidth, pictureHeight), inset) + 2 * ScrimPadding;
		double height = Math.Min(pictureHeight, Math.Max(pictureHeight * (BandBottom - BandTop), needed));
		double top = Math.Clamp(pictureHeight * (BandTop + BandBottom) / 2 - height / 2, 0, pictureHeight - height);
		return (top / pictureHeight, (top + height) / pictureHeight);
	}

	private View BuildOverPicture(Image picture, double pictureHeight)
	{
		double pictureWidth = pictureHeight * BackPictureAspect;
		double bandHeight = pictureHeight * (BandBottom - BandTop);
		_fontSize = FitFontSize(new Size(pictureWidth, bandHeight), SidePadding + ScrimPadding, 2 * ScrimPadding);
		(double bandTop, double bandBottom) = BandFor(pictureWidth, pictureHeight);
		var framed = new Grid
		{
			WidthRequest = pictureWidth,
			HeightRequest = pictureHeight,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			InputTransparent = true,
			RowDefinitions =
			{
				new RowDefinition(new GridLength(Math.Max(bandTop, 0.0001), GridUnitType.Star)),
				new RowDefinition(new GridLength(bandBottom - bandTop, GridUnitType.Star)),
				new RowDefinition(new GridLength(Math.Max(1 - bandBottom, 0.0001), GridUnitType.Star)),
			},
		};
		framed.Add(picture);
		Grid.SetRowSpan(picture, 3);
		Border scrim = BuildScrim(pictureWidth);
		framed.Add(scrim);
		Grid.SetRow(scrim, 1);
		return framed;
	}

	private View BuildSideBySide(Image picture, double pictureHeight)
	{
		double textWidth = Math.Max(0, _card.Width - pictureHeight * BackPictureAspect);
		_fontSize = FitFontSize(new Size(textWidth, _card.Height), SidePadding, 2 * ScrimPadding);
		var credits = new VerticalStackLayout { Spacing = LineGap, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(SidePadding, 0), InputTransparent = true };
		foreach (string line in view.Lines)
		{
			credits.Add(LineLabel(line));
		}

		var row = new Grid
		{
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill,
			InputTransparent = true,
			ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
		};
		row.Add(picture);
		row.Add(credits);
		Grid.SetColumn(credits, 1);
		return row;
	}

	private Border BuildScrim(double pictureWidth)
	{
		var credits = new VerticalStackLayout { Spacing = LineGap, InputTransparent = true };
		foreach (string line in view.Lines)
		{
			credits.Add(LineLabel(line));
		}

		return new Border
		{
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			MaximumWidthRequest = pictureWidth - 2 * SidePadding,
			Padding = new Thickness(ScrimPadding),
			BackgroundColor = CardColour(BackPage).WithAlpha(ScrimAlpha),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = ScrimCorner },
			InputTransparent = true,
			Content = credits,
		};
	}

	// The credits' height at a font size: each line's wrapped line count is estimated from its length and the room's width (less the inset each side), so a long language on a narrow card gets the room it needs.
	private double CreditsHeight(double fontSize, Size area, double inset)
	{
		double charsPerLine = Math.Max(1, (area.Width - 2 * inset) / (fontSize * AverageGlyphWidth));
		double height = 0;
		foreach (string line in view.Lines)
		{
			double wrapped = Math.Max(1, Math.Ceiling(line.Length / charsPerLine));
			height += wrapped * fontSize * LineHeightFactor + LineGap;
		}

		return height;
	}

	// The text area grows first and the picture shrinks (or drops); only when the credits alone would overflow does the font shrink, to a minimum.
	private double FitFontSize(Size area, double inset, double reservedHeight)
	{
		double size = LineFontSize;
		while (size > MinLineFontSize && CreditsHeight(size, area, inset) > area.Height - reservedHeight)
		{
			size -= FontStep;
		}

		return size;
	}

	// The picture is as big as the card's width share allows, but never so tall that the credit lines (as they wrap at the chosen font size) do not fit under it.
	private double PictureHeight()
	{
		double byWidth = _card.Width * CoverWidthShare / CoverAspect;
		double byHeight = _card.Height - CreditsHeight(_fontSize, _card, SidePadding) - CoverGap;
		return Math.Min(byWidth, byHeight);
	}

	private View? CoverPicture(string file)
	{
		double height = PictureHeight();
		if (height < MinPictureHeight)
		{
			return null;
		}

		return new Border
		{
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 0, 0, CoverGap),
			WidthRequest = height * CoverAspect,
			HeightRequest = height,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CoverCorner },
			InputTransparent = true,
			Content = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFill, InputTransparent = true },
		};
	}

	private Label LineLabel(string text) => new()
	{
		Text = text,
		FontFamily = font,
		FontSize = _fontSize,
		FontAutoScalingEnabled = false,
		TextColor = _theme?.Ink ?? Colors.Black,
		HorizontalTextAlignment = TextAlignment.Center,
		FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		InputTransparent = true,
	};
}
