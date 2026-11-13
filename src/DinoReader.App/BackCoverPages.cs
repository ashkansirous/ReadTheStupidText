using System.Globalization;
using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace DinoReader;

/// <summary>What the back cover shows (Slice 237): its lines already worded in the reading language, the book's colour and the small cover picture (a file path, or null for a colour cover).
/// Slice 359: <see cref="BackPicture"/> is the book's drawn back-cover picture (a file path); with it the credits sit on a cream card in the middle of the picture (item 6), and without it (Good Enough) the live page above is drawn.</summary>
public sealed record BackCoverView(IReadOnlyList<string> Lines, Color Colour, string? CoverPicture, string? BackPicture = null, string Title = "");

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
	private const double StageCorner = 18; // the drawn picture's stage (Slice 359 item 6, mock BC1-BC3)
	private const double CardCorner = 22;
	private const double CardShadowBlur = 28;
	private const double CardShadowOffset = 8;
	private const float CardShadowOpacity = 0.22f; // rgba(43,37,64,.22)
	private const double TitleLineHeight = 1.25;
	private const double CreditLineHeight = 1.55;
	private const double CreditGap = 2;
	private const double RuleWidth = 36;
	private const double RuleHeight = 3;
	private const double RuleMargin = 10;
	private static readonly Thickness CardPadding = new(22, 18, 22, 20);

	private ReadingTheme? _theme;
	private Size _card;
	private double _fontSize = LineFontSize;
	private double _appliedCardWidth; // the width BuildCard really set (0 for the live page), logged by DrawPage

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
		_appliedCardWidth = 0;
		layers.Text.Children.Add(BuildPage());
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"BACKCOVER shown lines={view.Lines.Count} picture={(view.BackPicture is null ? "live" : "drawn")}{CardLogSuffix()}");
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

	// Slice 359 item 6 (mock BC1-BC3): the drawn picture fills the stage (cover, radius 18, sand fill) and the credits sit on one cream card in the middle of it, at every size, view and
	// direction. The card has fixed text sizes and grows in height; only when it would not fit the stage do its sizes step down (BackCoverCard.Fit). The picture keeps its crop a little above centre.
	private View BuildDrawnPage(string file)
	{
		Size area = _card; // the one stage size the picture frame and the card fit both use
		double aspect = PageImageSize.Read(file) is { Height: > 0 } size ? size.Width / size.Height : 0; // the picture's real pixel aspect; 0 falls back to 3:4
		BackCoverPictureFrame frame = BackCoverPicture.Frame(area.Width, area.Height, aspect);
		var picture = new Image
		{
			Source = ImageSource.FromFile(file),
			Aspect = Aspect.AspectFill,
			WidthRequest = frame.Width,
			HeightRequest = frame.Height,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Start,
			TranslationY = frame.OffsetY,
			InputTransparent = true,
		};
		var stage = new Grid { InputTransparent = true, Children = { picture, BuildCard(area) } };
		return Host(new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.SoundPillSand),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = StageCorner },
			InputTransparent = true,
			Content = stage,
		});
	}

	private BackCoverCardSpec CardSpec(Size stage)
	{
		(double width, double height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, stage.Width, stage.Height); // the size class the reading page uses, so a narrow window gets the phone card
		bool wide = LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet;
		return BackCoverCard.Fit(BackCoverCard.KindFor(wide, width > height), stage.Width, stage.Height, view.Title, view.Lines); // wide and landscape both from the effective size
	}

	// The card's width in whole points for the BACKCOVER log, culture-independent so a Farsi or Arabic device logs "card=300"; empty for the live page, which has no card.
	private string CardLogSuffix() =>
		_appliedCardWidth > 0 ? $" card={((int)Math.Round(_appliedCardWidth)).ToString(CultureInfo.InvariantCulture)}" : string.Empty;

	private Border BuildCard(Size stage)
	{
		BackCoverCardSpec spec = CardSpec(stage);
		_appliedCardWidth = spec.Width;
		return CardFrame(spec.Width, CardContent(spec));
	}

	private VerticalStackLayout CardContent(BackCoverCardSpec spec)
	{
		var content = new VerticalStackLayout { Spacing = CreditGap, InputTransparent = true };
		content.Add(CardLabel(view.Title, spec.TitleSize, TitleLineHeight, FontAttributes.Bold));
		content.Add(CardRule());
		foreach (string line in view.Lines)
		{
			content.Add(CardLabel(line, spec.LineSize, CreditLineHeight, FontAttributes.None));
		}

		return content;
	}

	private static BoxView CardRule() => new()
	{
		WidthRequest = RuleWidth,
		HeightRequest = RuleHeight,
		CornerRadius = RuleHeight / 2,
		Color = DinoTokens.Get(DinoTokens.Amber),
		HorizontalOptions = LayoutOptions.Center,
		Margin = new Thickness(0, RuleMargin - CreditGap),
		InputTransparent = true,
	};

	private static Border CardFrame(double width, View content)
	{
		return new Border
		{
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			WidthRequest = width,
			Padding = CardPadding,
			BackgroundColor = DinoTokens.Get(DinoTokens.BackCoverCream),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardCorner },
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, CardShadowOffset), Radius = (float)CardShadowBlur, Opacity = CardShadowOpacity },
			InputTransparent = true,
			Content = content,
		};
	}

	private Label CardLabel(string text, double size, double lineHeight, FontAttributes weight) => new()
	{
		Text = text,
		FontFamily = font,
		FontSize = size,
		FontAttributes = weight,
		LineHeight = lineHeight,
		FontAutoScalingEnabled = false,
		TextColor = DinoTokens.Get(DinoTokens.Ink), // the card is cream in every theme, so its ink is fixed
		HorizontalTextAlignment = TextAlignment.Center,
		FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		InputTransparent = true,
	};

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
