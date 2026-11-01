using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace DinoReader;

/// <summary>What the back cover shows (Slice 237): its lines already worded in the reading language, the book's colour and the small cover picture (a file path, or null for a colour cover).</summary>
public sealed record BackCoverView(IReadOnlyList<string> Lines, Color Colour, string? CoverPicture);

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
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"BACKCOVER shown lines={view.Lines.Count}");
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
		var stack = new VerticalStackLayout
		{
			Spacing = LineGap,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Fill,
			Padding = new Thickness(SidePadding, 0),
			InputTransparent = true,
		};
		_fontSize = FitFontSize();
		if (view.CoverPicture is { } file && CoverPicture(file) is { } picture)
		{
			stack.Add(picture);
		}

		foreach (string line in view.Lines)
		{
			stack.Add(LineLabel(line));
		}

		var host = new Grid { AutomationId = BackCoverId, InputTransparent = true, Children = { stack } };
		AbsoluteLayout.SetLayoutFlags(host, AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(host, new Rect(0, 0, 1, 1));
		return host;
	}

	// The credits' height at a font size: each line's wrapped line count is estimated from its length and the card's width, so a long language on a narrow card gets the room it needs.
	private double CreditsHeight(double fontSize)
	{
		double charsPerLine = Math.Max(1, (_card.Width - 2 * SidePadding) / (fontSize * AverageGlyphWidth));
		double height = 0;
		foreach (string line in view.Lines)
		{
			double wrapped = Math.Max(1, Math.Ceiling(line.Length / charsPerLine));
			height += wrapped * fontSize * LineHeightFactor + LineGap;
		}

		return height;
	}

	// The text area grows first and the picture shrinks (or drops); only when the credits alone would overflow does the font shrink, to a minimum.
	private double FitFontSize()
	{
		double size = LineFontSize;
		while (size > MinLineFontSize && CreditsHeight(size) > _card.Height)
		{
			size -= FontStep;
		}

		return size;
	}

	// The picture is as big as the card's width share allows, but never so tall that the credit lines (as they wrap at the chosen font size) do not fit under it.
	private double PictureHeight()
	{
		double byWidth = _card.Width * CoverWidthShare / CoverAspect;
		double byHeight = _card.Height - CreditsHeight(_fontSize) - CoverGap;
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
