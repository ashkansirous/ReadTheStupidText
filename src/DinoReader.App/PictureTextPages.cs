using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Picture mode for a picture + text book (Slice 98, Decision 102; Slice 174, mock Batch 37 frames 91 and 92): each story page is one page of the reader, its picture covering its area and cropped from the top
/// (the art keeps its bottom free for words). Held sideways the picture is the left 40% and the text card the right 60% at full height; held upright the picture fills the page and the text sits on a 92% white
/// card over its lower part, five whole lines tall, that scrolls when the text is longer. The spoken sentence has the highlight behind its words and the card scrolls to keep it in view; a tap on a sentence
/// reads from it. With the words switched off (<c>wordsOver</c>, Slice 122) only the picture is drawn: upright it fills the page; sideways a tall picture keeps the left half and a wide one spans both halves above the transport row. A page with only a picture shows the picture, a page with only text shows the text card.
/// </summary>
public sealed class PictureTextPages(string text, IReadOnlyList<StoryPage> storyPages, IReadOnlyList<SentenceSpan> sentences,
	Func<string?, string?> resolveImage, string font, ReadingTheme theme, bool rightToLeft, Action<int> onSentence,
	Action<SwipedEventArgs> onSwipe, bool wordsOver = true) : IPageRenderer
{
	private const float PanelAlpha = 0.92f;
	private const double PanelFontSize = 21;
	private const double SidewaysFontSize = 23;
	private const int UprightLines = 5;
	private const double SidewaysPictureShare = 2;
	private const double SidewaysTextShare = 3;
	private const double PanelLineHeight = 1.5;
	private const double PictureCorner = 18;
	private const double UprightPanelCorner = 16;
	private const double SidewaysPanelCorner = 22;
	private const double PanelMargin = 8;
	private const long UserScrollQuietMs = 500;
	private const int MeasureRetries = 5;
	private const int MeasureRetryMs = 60;
	private static readonly Thickness UprightPanelPadding = new(14, 10);
	// The fade is as tall as the label's top margin, so it never covers the first visible line (that line sits one margin below the top of the viewport).
	private const double FadeHeight = 10;
	private const float FadeAlpha = 0.95f;
	private const float FadeSolidUntil = 0.6f;
	private const double SidewaysGap = 6;
	private const double UprightPictureTop = 6;
	private const double UprightPictureSide = 14;

	private sealed record Slot(int StoryPage, string? Image, double Aspect);

	private ReadingTheme _theme = theme;
	private ScanLayout _layout = null!;
	private List<Slot> _slots = [];
	private Size _card;
	private Label? _label;
	private ScrollView? _scroll;
	private double _scrollPad;
	private int _drawnPage = -1;
	private string _pageText = "";

	// Slice 171: the card's line table, read from the label's own native layout once per page and width (never on Mark), and the pitch last seen per font, which sizes a new card before its first read so it does not jump.
	private static readonly Dictionary<string, double> KnownPitch = [];
	private TextLines? _lines;
	private double _linesWidth;
	private bool _measureQueued;
	private Action? _replace;
	private int _current = -1;
	private int _appScrollHolds;
	private double _appTargetY = -1;
	private long _userScrollTick = long.MinValue / 2;

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
		_scroll = null;
		_lines = null;
		_replace = null;
		_measureQueued = false;
		_current = -1;
		_appScrollHolds = 0;
		_appTargetY = -1;
		_userScrollTick = long.MinValue / 2;
		_pageText = "";
		_drawnPage = page;
		// Only the scrolling text panel takes touches; the picture and the card leave taps and swipes to the surface.
		layers.Boxes.InputTransparent = false;
		layers.Boxes.CascadeInputTransparent = false;
		if (!InRange(page))
		{
			return;
		}

		_pageText = text[_layout.PageStart(_slots[page].StoryPage).._layout.PageEnd(_slots[page].StoryPage)];
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
		_current = sentence;
		ScrollToCurrent(animated: true);
	}

	/// <summary>The whole page's text on one soft highlight (a page read from a parent's recording, Slice 111).</summary>
	public void MarkPage(PageLayers layers, int page, int first, int last)
	{
		if (_label is null || page != _drawnPage || !InRange(page))
		{
			return;
		}

		_label.FormattedText = TextOf(_slots[page].StoryPage, -1, whole: true);
		_current = first;
		ScrollToCurrent(animated: true);
	}

	// Slice 171: the card keeps the spoken sentence in view. Where its first word sits comes from the label's own native layout (Android TextView.Layout, iOS TextKit),
	// so wrapping, scripts and fallback fonts are the screen's own; the choice of line is Core's. A sentence longer than the card keeps its start in view. A card the child is
	// dragging or flinging is left alone until the next sentence, and a redrawn page is brought back to the current sentence once its lines are known.
	private void ScrollToCurrent(bool animated)
	{
		if (_current < 0 || !InRange(_drawnPage) || _label is not { Width: > 0 } || _scroll is not { Height: > 0 } scroll)
		{
			return;
		}

		if (_lines is null)
		{
			// Mark never reads the native layout; QueueMeasure does, and then scrolls to the current sentence.
			return;
		}

		int storyPage = _slots[_drawnPage].StoryPage;
		(int first, int last) = _layout.SentencesOnPage(storyPage);
		if (_current < first || _current > last || UserIsScrolling())
		{
			return;
		}

		int start = _layout.PageStart(storyPage);
		int from = Math.Clamp(sentences[_current].Start, start, _layout.PageEnd(storyPage)) - start;
		double target = PictureTextBook.ScrollTargetFor(_lines!, _pageText, from, scroll.Height, _scrollPad);
		if (Math.Abs(target - scroll.ScrollY) >= 1)
		{
			_ = ScrollAsync(scroll, target, animated);
		}
	}

	private async Task ScrollAsync(ScrollView scroll, double y, bool animated)
	{
		HoldAppScroll();
		_appTargetY = y;
		try
		{
			await scroll.ScrollToAsync(0, y, animated);
		}
		catch (Exception ex)
		{
			// The page was turned or redrawn while the card was scrolling: nothing left to scroll.
			Debug.WriteLine($"Dino Reader: text card scroll ended early: {ex.Message}");
		}
		finally
		{
			ReleaseAppScroll();
		}
	}

	// A scroll the app did not ask for is the child's finger (or its fling); events keep coming while it lasts. The app's own scrolls and resizes are tagged (held) so they never start the pause;
	// the hold is released a dispatch later, after the events they queued. After a resize the scroll target is worked out again with the new viewport.
	private void WatchScroll(ScrollView scroll)
	{
		scroll.Scrolled += (_, _) =>
		{
			if (_appScrollHolds == 0 && Math.Abs(scroll.ScrollY - _appTargetY) >= 1)
			{
				_userScrollTick = Environment.TickCount64;
			}
		};
		scroll.SizeChanged += (_, _) =>
		{
			if (_lines is not null)
			{
				ScrollToCurrent(animated: false);
			}
		};
	}

	private void HoldAppScroll() => _appScrollHolds++;

	private void ReleaseAppScroll()
	{
		if (_label?.Dispatcher is { } dispatcher)
		{
			dispatcher.Dispatch(() => _appScrollHolds = Math.Max(0, _appScrollHolds - 1));
			return;
		}

		_appScrollHolds = Math.Max(0, _appScrollHolds - 1);
	}

	private bool UserIsScrolling() => Environment.TickCount64 - _userScrollTick < UserScrollQuietMs;

	// The label's lines are read once its native view has laid the text out, and again only if its width changes. Done after the layout pass, not inside it; a native layout not built yet
	// for the label's width (null) is tried again a few times a moment later.
	private void QueueMeasure(int attempt = 0)
	{
		if ((_measureQueued && attempt == 0) || _label is not { Width: > 0 } label || (_lines is not null && Math.Abs(_linesWidth - label.Width) < 0.5))
		{
			return;
		}

		_measureQueued = true;
		label.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(attempt == 0 ? 0 : MeasureRetryMs), () =>
		{
			if (!ReferenceEquals(label, _label))
			{
				return;
			}

			_measureQueued = false;
			if (ReadLines())
			{
				// The card may have been resized to whole lines; scroll once that has applied, with the new viewport.
				label.Dispatcher.Dispatch(() => ScrollToCurrent(animated: false));
			}
			else if (attempt < MeasureRetries)
			{
				QueueMeasure(attempt + 1);
			}
		});
	}

	private bool ReadLines()
	{
		if (_label is not { Width: > 0 } label || TextLineSources.Platform.Read(label) is not { Count: > 0 } read)
		{
			return false;
		}

		_lines = read;
		_linesWidth = label.Width;
		KnownPitch[PitchKey] = read.Pitch;
		_replace?.Invoke();
		return true;
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
			var margin = new Thickness(UprightPictureSide, UprightPictureTop, UprightPictureSide, 0);
			grid.Add(wordsOver ? CoverPicture(slot.Image, slot.Aspect, margin) : PictureCard(slot.Image, margin));
		}

		if (HasText(slot))
		{
			Border panel = TextPanel(UprightPanelCorner, UprightPanelPadding, PanelAlpha, fade: true);
			panel.VerticalOptions = LayoutOptions.End;
			panel.Margin = new Thickness(PanelMargin);
			PlacePanel(panel);
			// Placed again once the real lines are known (the card then holds five whole lines).
			Action replace = () =>
			{
				HoldAppScroll();
				PlacePanel(panel);
				ReleaseAppScroll();
			};
			_replace = replace;
			grid.SizeChanged += (_, _) => replace();
			panel.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromRgb(43, 37, 64)), Opacity = 0.16f, Radius = 12, Offset = new Point(0, 4) };
			grid.Add(panel);
		}

		return grid;
	}

	// Mock Batch 37 frame 91: the picture fills the area and is cropped from the top, so the panel sits on its lower part, a margin above the controls, and holds five whole lines.
	private void PlacePanel(Border panel)
	{
		double pitch = KnownPitch.TryGetValue(PitchKey, out double known) ? known : PanelFontSize * PanelLineHeight;
		panel.Margin = new Thickness(PanelMargin);
		panel.MaximumHeightRequest = PictureTextBook.CardHeightFor(UprightLines, _lines, pitch, UprightPanelPadding.Top);
	}

	private View SidewaysPage(Slot slot)
	{
		if (!wordsOver)
		{
			return SidewaysPictureOnly(slot);
		}

		// Mock Batch 37 frame 92: the picture is the left 40%, cropped from the top, with the controls floating over its bottom; the text takes the other 60% at full height.
		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(new GridLength(SidewaysPictureShare, GridUnitType.Star)), new ColumnDefinition(new GridLength(SidewaysTextShare, GridUnitType.Star))] };
		if (slot.Image is not null)
		{
			grid.Add(CoverPicture(slot.Image, slot.Aspect, new Thickness(0, 0, SidewaysGap, 0)), 0);
		}

		if (HasText(slot))
		{
			Border card = TextPanel(SidewaysPanelCorner, new Thickness(18, 14), 1f, fade: false);
			card.Margin = new Thickness(slot.Image is null ? 0 : SidewaysGap, 0, 0, 0);
			grid.Add(card, slot.Image is null ? 0 : 1);
			if (slot.Image is null)
			{
				Grid.SetColumnSpan(card, 2);
			}
		}

		return grid;
	}

	// Picture only, held sideways: the old halves layout with the controls under the right half (a wide picture spans both above them).
	private View SidewaysPictureOnly(Slot slot)
	{
		if (slot.Image is not null && IsWide(slot))
		{
			return WidePicture(slot.Image);
		}

		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
		if (slot.Image is not null)
		{
			grid.Add(PictureCard(slot.Image, new Thickness(SidewaysGap)), 0);
		}

		return grid;
	}

	// With the words off (Slice 122) a tall picture keeps its half at full height, as big as it can be there, and the other half keeps
	// the transport row; a wide picture would shrink in one half, so it gets the whole page above the transport row.
	// Wide means wider than one half of the page, where it would be width-bound and small; before layout, wider than square.
	private bool IsWide(Slot slot)
	{
		double oneHalf = _card.Width > 0 && _card.Height > 0 ? _card.Width / 2 / _card.Height : 1;
		return slot.Aspect > oneHalf;
	}

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

	// The picture covers its area and is cropped from the bottom (Slice 174): the image is sized to the cover frame and clipped by the rounded border. Unknown shape: filled as it is.
	private static Border CoverPicture(string file, double aspect, Thickness margin)
	{
		// Until the area has a size the picture fills it, cropped (never stretched or at native size); then the exact cover frame replaces that.
		var image = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFill, InputTransparent = true };
		AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(image, new Rect(0, 0, 1, 1));
		var host = new AbsoluteLayout { InputTransparent = true, Children = { image } };
		host.SizeChanged += (_, _) =>
		{
			AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
			var frame = PictureTextBook.CoverFromTop(host.Width, host.Height, aspect);
			AbsoluteLayout.SetLayoutBounds(image, new Rect(frame.X, frame.Y, frame.Width, frame.Height));
			image.Aspect = aspect > 0 ? Aspect.Fill : Aspect.AspectFill;
		};
		return new Border
		{
			Margin = margin,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = PictureCorner },
			InputTransparent = true,
			Content = host,
		};
	}

	private double FontSize => Sideways ? SidewaysFontSize : PanelFontSize;

	private string PitchKey => $"{font}:{FontSize}";

	private Border TextPanel(double corner, Thickness padding, float alpha, bool fade)
	{
		_label = new Label
		{
			AutomationId = ReadingMetrics.StoryTextId,
			FontFamily = font,
			FontSize = FontSize,
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
			_scrollPad = 0;
			_scroll = new ScrollView { Content = _label };
			WatchScroll(_scroll);
			panel.Content = _scroll;
		}

		_label.SizeChanged += (_, _) => QueueMeasure();
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
		_scrollPad = padding.Top;
		var scroll = new ScrollView { Content = _label, VerticalOptions = LayoutOptions.End };
		_scroll = scroll;
		WatchScroll(scroll);
		BoxView top = TopFade();
		scroll.Scrolled += (_, _) => ShowFadeWhenScrolled(top, scroll);
		scroll.SizeChanged += (_, _) => ShowFadeWhenScrolled(top, scroll);
		_label.SizeChanged += (_, _) => ShowFadeWhenScrolled(top, scroll);
		var grid = new Grid();
		grid.Add(scroll);
		grid.Add(top);
		return grid;
	}

	private static void ShowFadeWhenScrolled(BoxView fade, ScrollView scroll)
	{
		fade.IsVisible = scroll.ScrollY > 1;
	}

	private BoxView TopFade() => new()
	{
		HeightRequest = FadeHeight,
		VerticalOptions = LayoutOptions.Start,
		IsVisible = false,
		InputTransparent = true,
		Background = new LinearGradientBrush(
			[new GradientStop(_theme.TextCard.WithAlpha(FadeAlpha), 0f), new GradientStop(_theme.TextCard.WithAlpha(FadeAlpha), FadeSolidUntil), new GradientStop(_theme.TextCard.WithAlpha(0f), 1f)],
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
		var span = new Span { Text = piece, TextColor = _theme.Ink, FontFamily = font, FontSize = FontSize };
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
