using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Picture mode on real pages (Slice 64, mock screens 2 and 13): a story made from scans or a PDF shows the
/// page pictures, fitted to the card with their aspect kept, with a box over each line of the spoken sentence.
/// The boxes come from <see cref="ScanLayout"/> in the picture's own 0..1 coordinates and are drawn in a
/// layer that holds the picture too, so letterboxing, rotation and zoom move picture and boxes together.
/// A story page without a usable picture (a typed page, or a scan whose lines could not be placed) is drawn
/// as a generated page, so mixed stories read as one book.
/// </summary>
public sealed class ScanPages(string text, IReadOnlyList<StoryPage> storyPages, IReadOnlyList<SentenceSpan> sentences,
	Func<string?, string?> resolveImage, ITextMeasurer measurer, string font, ReadingTheme theme, Action<TappedEventArgs> onTap, Action<SwipedEventArgs> onSwipe) : IPageRenderer
{
	private sealed record Slot(int StoryPage, string? Image, Size ImageSize, ComposedPage? Generated)
	{
		public bool IsScan => Image is not null;
	}

	private ReadingTheme _theme = theme;
	private ScanLayout _layout = null!;
	private List<Slot> _slots = [];
	private Size _card;
	private ZoomablePage? _view;

	public int PageCount => _slots.Count;

	public bool IsZoomed => _view?.IsZoomed == true;

	public double CornerRadius => _slots.Any(s => s.IsScan) ? ReadingMetrics.ScanCorner : ReadingMetrics.PictureCorner;

	/// <summary>Pure layout (picture sizes are read from file headers), so it can run off the UI thread.</summary>
	public void Compose(Size card)
	{
		_card = card;
		_layout = new ScanLayout(storyPages, sentences);
		var slots = new List<Slot>();
		PageComposer composer = ComposerFor(card);
		for (int page = 0; page < storyPages.Count; page++)
		{
			AddSlots(slots, composer, page);
		}

		_slots = slots;
	}

	public void SetTheme(ReadingTheme newTheme) => _theme = newTheme;

	public int PageOf(int sentence, int position = -1)
	{
		int storyPage = _layout.PageOfSentence(sentence, position);
		int chosen = -1;
		for (int i = 0; i < _slots.Count; i++)
		{
			Slot slot = _slots[i];
			bool holds = slot.IsScan ? slot.StoryPage == storyPage : slot.Generated!.Holds(sentence);
			if (holds && (chosen < 0 || (position >= 0 && !slot.IsScan && position >= slot.Generated!.StartOf(sentence))))
			{
				chosen = i;
			}
		}

		return chosen;
	}

	public int FirstPositionOf(int page) =>
		!InRange(page) ? -1 : _slots[page].IsScan ? _layout.PageStart(_slots[page].StoryPage) : _slots[page].Generated!.FirstStart;

	public Color CardColour(int page) =>
		InRange(page) && _slots[page].IsScan ? _theme.ScanCard : _theme.PageCard(page);

	public int FirstSentenceOf(int page) =>
		!InRange(page) ? -1 : _slots[page].IsScan ? _layout.SentencesOnPage(_slots[page].StoryPage).First : _slots[page].Generated!.FirstSentence;

	public void DrawPage(PageLayers layers, int page)
	{
		layers.Boxes.Children.Clear();
		layers.Text.Children.Clear();
		_view = null;
		// A picture takes pinch and pan, so its layer must be hit-testable; a generated page leaves taps to the card.
		bool scan = InRange(page) && _slots[page].IsScan;
		layers.Boxes.InputTransparent = !scan;
		layers.Boxes.CascadeInputTransparent = !scan;
		if (!InRange(page))
		{
			return;
		}

		Slot slot = _slots[page];
		if (slot.IsScan)
		{
			_view = ZoomablePage.Create(slot.Image!, slot.ImageSize, _card, onTap, onSwipe);
			layers.Boxes.Children.Add(_view.Container);
			return;
		}

		foreach (ComposedLine line in slot.Generated!.Lines)
		{
			layers.Text.Children.Add(PageArt.Label(line, _card, font, _theme));
		}
	}

	public void Mark(PageLayers layers, int page, int sentence)
	{
		if (!InRange(page))
		{
			layers.Boxes.Children.Clear();
			return;
		}

		Slot slot = _slots[page];
		if (!slot.IsScan)
		{
			MarkGenerated(layers, slot, sentence);
			return;
		}

		IEnumerable<NormalizedRect> boxes = _layout.BoxesOf(sentence).Where(b => b.Page == slot.StoryPage).Select(b => b.Rect);
		_view?.ShowBoxes(boxes, _theme);
	}

	public int SentenceAt(int page, Point point)
	{
		if (!InRange(page) || _card.Width <= 0)
		{
			return -1;
		}

		Slot slot = _slots[page];
		if (!slot.IsScan)
		{
			return slot.Generated!.SentenceAt(point.X / _card.Width, point.Y / _card.Height);
		}

		return _view?.ToPageUnits(point) is { } at ? _layout.SentenceAt(slot.StoryPage, at.X, at.Y) : -1;
	}

	private bool InRange(int page) => page >= 0 && page < _slots.Count;

	private PageComposer ComposerFor(Size card)
	{
		var geometry = new PageGeometry(card.Width, card.Height, ReadingMetrics.PicturePaddingX,
			ReadingMetrics.PicturePaddingY, ReadingMetrics.PictureLineHeight, ReadingMetrics.PictureLineGap,
			ReadingMetrics.PictureOutset);
		return new PageComposer(geometry, new ScaledMeasurer(measurer, 1 / ReadingMetrics.WrapSlack));
	}

	private void AddSlots(List<Slot> slots, PageComposer composer, int page)
	{
		if (_layout.IsScanned(page) && TryPicture(page) is { } picture)
		{
			slots.Add(new Slot(page, picture.Path, picture.Size, null));
			return;
		}

		bool afterPicture = slots.Count > 0 && slots[^1].IsScan && slots[^1].StoryPage == page - 1;
		IReadOnlyList<GeneratedSpan> generated = _layout.SpansForGeneratedPage(page, afterPicture);
		IReadOnlyList<SentenceSpan> spans = generated.Select(g => g.Span).ToList();
		IReadOnlyList<int> real = generated.Select(g => g.Sentence).ToList();
		if (spans.Count == 0)
		{
			return;
		}

		foreach (ComposedPage composed in composer.Compose(text, spans))
		{
			slots.Add(new Slot(page, null, default, composed.RemapSentences(real)));
		}
	}

	private (string Path, Size Size)? TryPicture(int page)
	{
		string? path = resolveImage(storyPages[page].ImageFile);
		return path is not null && PageImageSize.Read(path) is { } size ? (path, size) : null;
	}

	private void MarkGenerated(PageLayers layers, Slot slot, int sentence)
	{
		layers.Boxes.Children.Clear();
		foreach (NormalizedRect rect in slot.Generated!.BoxesOf(sentence))
		{
			layers.Boxes.Children.Add(PageArt.Box(PageArt.ToRect(rect, _card), _theme));
		}
	}
}
