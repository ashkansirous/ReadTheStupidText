using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Picture mode with generated book pages (Slice 62): big type on a pastel page, every sentence on
/// its own lines, and the spoken sentence boxed by a translucent rounded box per line. The line
/// boxes come from <see cref="PageComposer"/>, the same model Slices 63 and 64 fill from real pages.
/// </summary>
public sealed class PicturePages(string text, IReadOnlyList<SentenceSpan> sentences, ITextMeasurer measurer,
	string font, ReadingTheme theme) : IPageRenderer
{
	private ReadingTheme _theme = theme;
	private IReadOnlyList<ComposedPage> _pages = [];
	private Size _card;

	public int PageCount => _pages.Count;

	public double CornerRadius => ReadingMetrics.PictureCorner;

	/// <summary>Pure layout (no UI objects), so it can run off the UI thread.</summary>
	public void Compose(Size card)
	{
		_card = card;
		var geometry = new PageGeometry(card.Width, card.Height, ReadingMetrics.PicturePaddingX,
			ReadingMetrics.PicturePaddingY, ReadingMetrics.PictureLineHeight, ReadingMetrics.PictureLineGap,
			ReadingMetrics.PictureOutset);
		var wrapper = new ScaledMeasurer(measurer, 1 / ReadingMetrics.WrapSlack);
		_pages = new PageComposer(geometry, wrapper).Compose(text, sentences);
	}

	public void SetTheme(ReadingTheme newTheme) => _theme = newTheme;

	public int PageOf(int sentence, int position = -1) => PageComposer.PageOf(_pages, sentence, position);

	public int FirstPositionOf(int page) => page >= 0 && page < _pages.Count ? _pages[page].FirstStart : -1;

	public Color CardColour(int page) => _theme.PageCard(page);

	public int FirstSentenceOf(int page) => page >= 0 && page < _pages.Count ? _pages[page].FirstSentence : -1;

	public void DrawPage(PageLayers layers, int page)
	{
		layers.Boxes.Children.Clear();
		layers.Text.Children.Clear();
		if (page < 0 || page >= _pages.Count)
		{
			return;
		}

		foreach (ComposedLine line in _pages[page].Lines)
		{
			layers.Text.Children.Add(PageArt.Label(line, _card, font, _theme));
		}
	}

	public void Mark(PageLayers layers, int page, int sentence)
	{
		layers.Boxes.Children.Clear();
		if (page < 0 || page >= _pages.Count)
		{
			return;
		}

		foreach (NormalizedRect rect in _pages[page].BoxesOf(sentence))
		{
			layers.Boxes.Children.Add(PageArt.Box(PageArt.ToRect(rect, _card), _theme));
		}
	}

	public int SentenceAt(int page, Point point) =>
		page < 0 || page >= _pages.Count || _card.Width <= 0
			? -1
			: _pages[page].SentenceAt(point.X / _card.Width, point.Y / _card.Height);
}
