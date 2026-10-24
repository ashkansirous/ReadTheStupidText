using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Picture mode for a picture + text book (Slice 98, Decision 102, mock screens 34 and 35): each story page is one page of the
/// reader, its picture fitted without cropping and its text beside it. Held sideways the picture is the left half and the text card
/// the right half with the transport row under it; held upright the picture fills the page and the text sits on a 92% white panel over its lower part (up to 58% of
/// the height) that scrolls when the text is long. The spoken sentence has the highlight behind its words; a tap on a sentence
/// reads from it. A page with only a picture shows the picture, a page with only text shows the text card.
/// </summary>
public sealed class PictureTextPages(string text, IReadOnlyList<StoryPage> storyPages, IReadOnlyList<SentenceSpan> sentences,
	Func<string?, string?> resolveImage, string font, ReadingTheme theme, bool rightToLeft, Action<int> onSentence,
	Action<SwipedEventArgs> onSwipe) : IPageRenderer
{
	private const double PanelHeightShare = 0.58;
	private const float PanelAlpha = 0.92f;
	private const double PanelFontSize = 21;
	private const double PanelLineHeight = 1.55;
	private const double PictureCorner = 18;
	private const double UprightPanelCorner = 18;
	private const double SidewaysPanelCorner = 22;
	private const double PanelMargin = 10;
	private const double SidewaysGap = 6;
	private const double UprightPictureTop = 6;
	private const double UprightPictureSide = 14;

	private sealed record Slot(int StoryPage, string? Image);

	private ReadingTheme _theme = theme;
	private ScanLayout _layout = null!;
	private List<Slot> _slots = [];
	private Size _card;
	private Label? _label;
	private int _drawnPage = -1;

	public int PageCount => _slots.Count;

	public double CornerRadius => ReadingMetrics.ScanCorner;

	private bool Sideways => _card.Width > _card.Height;

	/// <summary>Pure layout (picture paths only), so it can run off the UI thread.</summary>
	public void Compose(Size card)
	{
		_card = card;
		_layout = new ScanLayout(storyPages, sentences);
		var slots = new List<Slot>();
		for (int page = 0; page < storyPages.Count; page++)
		{
			// Every story page keeps its place (even a blank one), so page numbers and dots match the story's pages.
			slots.Add(new Slot(page, resolveImage(storyPages[page].ImageFile)));
		}

		_slots = slots;
	}

	public void SetTheme(ReadingTheme newTheme) => _theme = newTheme;

	public int PageOf(int sentence, int position = -1)
	{
		int storyPage = _layout.PageOfSentence(sentence, position);
		return _slots.FindIndex(s => s.StoryPage == storyPage);
	}

	public int FirstPositionOf(int page) => InRange(page) ? _layout.PageStart(_slots[page].StoryPage) : -1;

	public Color CardColour(int page) => _theme.Page;

	public int FirstSentenceOf(int page) => InRange(page) ? _layout.SentencesOnPage(_slots[page].StoryPage).First : -1;

	public void DrawPage(PageLayers layers, int page)
	{
		layers.Boxes.Children.Clear();
		layers.Text.Children.Clear();
		_label = null;
		_drawnPage = page;
		// Only the scrolling text panel takes touches; the picture and the card leave taps and swipes to the surface.
		layers.Boxes.InputTransparent = false;
		layers.Boxes.CascadeInputTransparent = false;
		if (!InRange(page))
		{
			return;
		}

		View content = Sideways ? SidewaysPage(_slots[page]) : UprightPage(_slots[page]);
		AbsoluteLayout.SetLayoutBounds(content, new Rect(0, 0, _card.Width, _card.Height));
		layers.Boxes.Children.Add(content);
		Mark(layers, page, -1);
	}

	public void Mark(PageLayers layers, int page, int sentence)
	{
		if (_label is null || page != _drawnPage || !InRange(page))
		{
			return;
		}

		_label.FormattedText = TextOf(_slots[page].StoryPage, sentence);
	}

	// Taps on the text are handled by each sentence's own span (the label takes touches, so they reach the spans), so the
	// card-level lookup finds nothing.
	public int SentenceAt(int page, Point point) => -1;

	private bool InRange(int page) => page >= 0 && page < _slots.Count;

	private View UprightPage(Slot slot)
	{
		var grid = new Grid();
		if (slot.Image is not null)
		{
			grid.Add(PictureCard(slot.Image, new Thickness(UprightPictureSide, UprightPictureTop, UprightPictureSide, 0)));
		}

		if (HasText(slot))
		{
			Border panel = TextPanel(UprightPanelCorner, new Thickness(16, 14), PanelAlpha);
			panel.VerticalOptions = LayoutOptions.End;
			panel.Margin = new Thickness(PanelMargin);
			panel.MaximumHeightRequest = _card.Height * PanelHeightShare;
			panel.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromRgb(43, 37, 64)), Opacity = 0.18f, Radius = 18, Offset = new Point(0, 6) };
			grid.Add(panel);
		}

		return grid;
	}

	private View SidewaysPage(Slot slot)
	{
		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		if (slot.Image is not null)
		{
			grid.Add(PictureCard(slot.Image, new Thickness(SidewaysGap)), 0);
		}

		if (HasText(slot))
		{
			Border card = TextPanel(SidewaysPanelCorner, new Thickness(18, 14), 1f);
			// The transport row sits under the text card, in the bottom of this half (mock screen 35).
			card.Margin = new Thickness(SidewaysGap, SidewaysGap, SidewaysGap, SidewaysGap + TransportRow.SidewaysHeight);
			grid.Add(card, 1);
		}

		return grid;
	}

	private bool HasText(Slot slot) => _layout.SentencesOnPage(slot.StoryPage).First >= 0;

	private static Border PictureCard(string file, Thickness margin) => new()
	{
		Margin = margin,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = PictureCorner },
		InputTransparent = true,
		Content = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFit, InputTransparent = true },
	};

	private Border TextPanel(double corner, Thickness padding, float alpha)
	{
		_label = new Label
		{
			FontFamily = font,
			FontSize = PanelFontSize,
			LineHeight = PanelLineHeight,
			FontAutoScalingEnabled = false,
			TextColor = _theme.Ink,
			FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		};
		var panel = new Border
		{
			BackgroundColor = _theme.TextCard.WithAlpha(alpha),
			StrokeThickness = 0,
			Padding = padding,
			StrokeShape = new RoundRectangle { CornerRadius = corner },
			Content = new ScrollView { Content = _label },
		};
		panel.GestureRecognizers.Add(PanelSwipe(SwipeDirection.Left));
		panel.GestureRecognizers.Add(PanelSwipe(SwipeDirection.Right));
		return panel;
	}

	private SwipeGestureRecognizer PanelSwipe(SwipeDirection direction)
	{
		var swipe = new SwipeGestureRecognizer { Direction = direction };
		swipe.Swiped += (_, e) => onSwipe(e);
		return swipe;
	}

	// The page's text cut at each sentence start: what was read and the spoken sentence are ink (the spoken one on the
	// highlight), what is still to come is dim; with nothing spoken the page is all ink.
	private FormattedString TextOf(int storyPage, int current)
	{
		int start = _layout.PageStart(storyPage);
		int end = _layout.PageEnd(storyPage);
		var formatted = new FormattedString();
		(int first, int last) = _layout.SentencesOnPage(storyPage);
		int at = start;
		for (int i = first; i >= 0 && i <= last; i++)
		{
			int pieceEnd = i == last ? end : Math.Clamp(sentences[i + 1].Start, at, end);
			formatted.Spans.Add(SpanOf(text[at..pieceEnd], i, current));
			at = pieceEnd;
		}

		if (formatted.Spans.Count == 0)
		{
			formatted.Spans.Add(SpanOf(text[start..end], -1, current));
		}

		return formatted;
	}

	private Span SpanOf(string piece, int sentence, int current)
	{
		var span = new Span { Text = piece, TextColor = _theme.Ink, FontFamily = font, FontSize = PanelFontSize };
		if (current >= 0 && sentence > current)
		{
			span.TextColor = _theme.ReadText;
		}
		else if (current >= 0 && sentence == current)
		{
			span.BackgroundColor = _theme.TextHighlight;
		}

		if (sentence >= 0)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => onSentence(sentence);
			span.GestureRecognizers.Add(tap);
		}

		return span;
	}
}
