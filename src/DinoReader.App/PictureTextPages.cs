using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Picture mode for a picture + text book (Slice 98, Decision 102, mock screens 34 and 35): each story page is one page of the
/// reader, its picture fitted without cropping and its text beside it. Held sideways the picture is the left half and the text card
/// the right half with the transport row under it; held upright the picture fills the page and the text sits on a 92% white panel over its lower part (up to 30% of
/// the visible picture, Slice 122) that scrolls when the text is long. The spoken sentence has the highlight behind its words; a tap on a sentence
/// reads from it. With the words switched off (<c>wordsOver</c>, Slice 122) only the picture is drawn: upright it fills the page; sideways a tall picture keeps the left half and a wide one spans both halves above the transport row. A page with only a picture shows the picture, a page with only text shows the text card.
/// </summary>
public sealed class PictureTextPages(string text, IReadOnlyList<StoryPage> storyPages, IReadOnlyList<SentenceSpan> sentences,
	Func<string?, string?> resolveImage, string font, ReadingTheme theme, bool rightToLeft, Action<int> onSentence,
	Action<SwipedEventArgs> onSwipe, bool wordsOver = true) : IPageRenderer
{
	private const float PanelAlpha = 0.92f;
	private const double PanelFontSize = 21;
	private const double PanelLineHeight = 1.5;
	private const double PictureCorner = 18;
	private const double UprightPanelCorner = 16;
	private const double SidewaysPanelCorner = 22;
	private const double PanelMargin = 8;
	private static readonly Thickness UprightPanelPadding = new(14, 10);
	private const double FadeHeight = 22;
	private const float FadeAlpha = 0.95f;
	private const double SidewaysGap = 6;
	private const double UprightPictureTop = 6;
	private const double UprightPictureSide = 14;

	private sealed record Slot(int StoryPage, string? Image, double Aspect);

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
			slots.Add(SlotOf(page));
		}

		_slots = slots;
	}

	private Slot SlotOf(int page)
	{
		string? image = resolveImage(storyPages[page].ImageFile);
		// The shape sizes the upright panel and, with the words off, places a sideways picture. Compose runs on a worker thread.
		Size? size = image is null ? null : PageImageSize.Read(image);
		return new Slot(page, image, size is { Height: > 0 } s ? s.Width / s.Height : 0);
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

		_label.FormattedText = TextOf(_slots[page].StoryPage, sentence, whole: false);
	}

	/// <summary>The whole page's text on one soft highlight (a page read from a parent's recording, Slice 111).</summary>
	public void MarkPage(PageLayers layers, int page, int first, int last)
	{
		if (_label is null || page != _drawnPage || !InRange(page))
		{
			return;
		}

		_label.FormattedText = TextOf(_slots[page].StoryPage, -1, whole: true);
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
			Border panel = TextPanel(UprightPanelCorner, UprightPanelPadding, PanelAlpha, fade: true);
			panel.VerticalOptions = LayoutOptions.End;
			panel.Margin = new Thickness(PanelMargin);
			CapPanel(panel, _card, slot.Aspect);
			grid.SizeChanged += (_, _) => CapPanel(panel, new Size(grid.Width, grid.Height), slot.Aspect);
			panel.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromRgb(43, 37, 64)), Opacity = 0.16f, Radius = 12, Offset = new Point(0, 4) };
			grid.Add(panel);
		}

		return grid;
	}

	// A card with no size yet leaves the panel uncapped; the cap is applied again once the page has been laid out.
	private static void CapPanel(Border panel, Size card, double aspect)
	{
		if (card.Width > 0 && card.Height > 0)
		{
			panel.MaximumHeightRequest = PictureTextBook.PanelMaxHeight(card.Width, card.Height, aspect, PanelMargin,
				UprightPictureTop, UprightPictureSide);
		}
	}

	private View SidewaysPage(Slot slot)
	{
		if (!wordsOver && slot.Image is not null && IsWide(slot))
		{
			return WidePicture(slot.Image);
		}

		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		if (slot.Image is not null)
		{
			grid.Add(PictureCard(slot.Image, new Thickness(SidewaysGap)), 0);
		}

		if (HasText(slot))
		{
			Border card = TextPanel(SidewaysPanelCorner, new Thickness(18, 14), 1f, fade: false);
			// The transport row sits under the text card, in the bottom of this half (mock screen 35).
			card.Margin = new Thickness(SidewaysGap, SidewaysGap, SidewaysGap, SidewaysGap + TransportRow.SidewaysHeight);
			grid.Add(card, 1);
		}

		return grid;
	}

	// With the words off (Slice 122) a tall picture keeps its half at full height, as big as it can be there, and the other half keeps
	// the transport row; a wide picture would shrink in one half, so it gets the whole page above the transport row.
	// Wide means wider than one half of the page, where it would be width-bound and small; before layout, wider than square.
	private bool IsWide(Slot slot) =>
		slot.Aspect > (_card.Width > 0 && _card.Height > 0 ? _card.Width / 2 / _card.Height : 1);

	private static Grid WidePicture(string file)
	{
		var holder = new Grid { Padding = new Thickness(0, 0, 0, TransportRow.SidewaysHeight) };
		holder.Add(PictureCard(file, new Thickness(SidewaysGap)));
		return holder;
	}

	// A page with no picture keeps its text card whatever the switch says: there is nothing else to show.
	private bool HasText(Slot slot) => (wordsOver || slot.Image is null) && _layout.SentencesOnPage(slot.StoryPage).First >= 0;

	private static Border PictureCard(string file, Thickness margin) => new()
	{
		Margin = margin,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = PictureCorner },
		InputTransparent = true,
		Content = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFit, InputTransparent = true },
	};

	private Border TextPanel(double corner, Thickness padding, float alpha, bool fade)
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
			StrokeShape = new RoundRectangle { CornerRadius = corner },
		};
		if (fade)
		{
			panel.Content = PanelContent(padding);
		}
		else
		{
			panel.Padding = padding;
			panel.Content = new ScrollView { Content = _label };
		}

		panel.GestureRecognizers.Add(PanelSwipe(SwipeDirection.Left));
		panel.GestureRecognizers.Add(PanelSwipe(SwipeDirection.Right));
		return panel;
	}

	// The upright panel: short text sits at the bottom, and a soft fade in the panel's own colour covers its top edge while
	// some text is scrolled out above (mock frames 56-58, Slice 124). The padding is the label's margin, so the scroll area
	// and the fade both reach the panel's edges and the Border's rounded clip trims them.
	private View PanelContent(Thickness padding)
	{
		_label!.Margin = padding;
		var scroll = new ScrollView { Content = _label, VerticalOptions = LayoutOptions.End };
		BoxView top = TopFade();
		scroll.Scrolled += (_, _) => ShowFadeWhenScrolled(top, scroll);
		scroll.SizeChanged += (_, _) => ShowFadeWhenScrolled(top, scroll);
		_label.SizeChanged += (_, _) => ShowFadeWhenScrolled(top, scroll);
		var grid = new Grid();
		grid.Add(scroll);
		grid.Add(top);
		return grid;
	}

	private static void ShowFadeWhenScrolled(BoxView fade, ScrollView scroll) => fade.IsVisible = scroll.ScrollY > 1;

	private BoxView TopFade() => new()
	{
		HeightRequest = FadeHeight,
		VerticalOptions = LayoutOptions.Start,
		IsVisible = false,
		InputTransparent = true,
		Background = new LinearGradientBrush(
			[new GradientStop(_theme.TextCard.WithAlpha(FadeAlpha), 0f), new GradientStop(_theme.TextCard.WithAlpha(0f), 1f)],
			new Point(0, 0), new Point(0, 1)),
	};

	private SwipeGestureRecognizer PanelSwipe(SwipeDirection direction)
	{
		var swipe = new SwipeGestureRecognizer { Direction = direction };
		swipe.Swiped += (_, e) => onSwipe(e);
		return swipe;
	}

	// The page's text cut at each sentence start: what was read and the spoken sentence are ink (the spoken one on the
	// highlight), what is still to come is dim; with nothing spoken the page is all ink.
	private FormattedString TextOf(int storyPage, int current, bool whole)
	{
		int start = _layout.PageStart(storyPage);
		int end = _layout.PageEnd(storyPage);
		var formatted = new FormattedString();
		(int first, int last) = _layout.SentencesOnPage(storyPage);
		int at = start;
		for (int i = first; i >= 0 && i <= last; i++)
		{
			int pieceEnd = i == last ? end : Math.Clamp(sentences[i + 1].Start, at, end);
			formatted.Spans.Add(SpanOf(text[at..pieceEnd], i, current, whole));
			at = pieceEnd;
		}

		if (formatted.Spans.Count == 0)
		{
			formatted.Spans.Add(SpanOf(text[start..end], -1, current, whole));
		}

		return formatted;
	}

	private Span SpanOf(string piece, int sentence, int current, bool whole)
	{
		var span = new Span { Text = piece, TextColor = _theme.Ink, FontFamily = font, FontSize = PanelFontSize };
		if (whole)
		{
			span.BackgroundColor = _theme.PageHighlight;
		}
		else if (current >= 0 && sentence > current)
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
