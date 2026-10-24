using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Text mode (Slice 62, mock screen 4): the story as pages of whole sentences on a white card. What
/// was read is dim, the spoken sentence has a highlight behind its words and the rest is ink. A
/// page is a stack of lines, each line one label of word spans, so the layout is the one that was
/// measured. The highlight is square-cornered (a MAUI span cannot round).
/// </summary>
public sealed class TextPages(string text, IReadOnlyList<SentenceSpan> sentences, ITextMeasurer measurer,
	string font, ReadingTheme theme, bool rightToLeft) : IPageRenderer
{
	// What a line looks like for the sentence being spoken: all ink, all dim, or split around the
	// current sentence (its index). A line is rebuilt only when this changes.
	private const int PlainKey = -1;
	private const int ReadKey = -2;
	private const int NotApplied = int.MinValue;

	// A wrapped line with the sentence each of its words belongs to, worked out once at wrap time.
	private sealed record TextLine(IReadOnlyList<WordAt> Words, int[] SentenceOfWord)
	{
		public int FirstSentence => SentenceOfWord.Length == 0 ? -1 : SentenceOfWord[0];

		public int LastSentence => SentenceOfWord.Length == 0 ? -1 : SentenceOfWord[^1];
	}

	private ReadingTheme _theme = theme;
	private IReadOnlyList<FlowPage> _pages = [];
	private Size _card;
	private double _textWidth;
	private readonly Dictionary<int, IReadOnlyList<TextLine>> _lines = [];
	private readonly List<Label> _labels = [];
	private int[] _applied = [];

	public int PageCount => _pages.Count;

	public double CornerRadius => ReadingMetrics.TextCorner;

	/// <summary>Pure layout (no UI objects), so it can run off the UI thread.</summary>
	public void Compose(Size card)
	{
		_lines.Clear();
		_card = card;
		_textWidth = card.Width - 2 * ReadingMetrics.TextPadding;
		int linesPerPage = Math.Max(1, (int)Math.Floor((card.Height - 2 * ReadingMetrics.TextPadding) / ReadingMetrics.TextLineHeight));
		_pages = FlowPager.Paginate(text, sentences, WrapWidth, linesPerPage, measurer);
	}

	private double WrapWidth => _textWidth * ReadingMetrics.WrapSlack;

	public void SetTheme(ReadingTheme newTheme) => _theme = newTheme;

	public int PageOf(int sentence, int position = -1) => FlowPager.PageOf(_pages, sentence, position);

	public int FirstPositionOf(int page) => page >= 0 && page < _pages.Count ? _pages[page].Start : -1;

	public Color CardColour(int page) => _theme.TextCard;

	public int FirstSentenceOf(int page) => page >= 0 && page < _pages.Count ? _pages[page].FirstSentence : -1;

	public void DrawPage(PageLayers layers, int page)
	{
		layers.Boxes.Children.Clear();
		layers.Text.Children.Clear();
		_labels.Clear();
		if (page < 0 || page >= _pages.Count)
		{
			return;
		}

		IReadOnlyList<TextLine> lines = LinesOf(page);
		for (int i = 0; i < lines.Count; i++)
		{
			Label label = ReadingMetrics.LineLabel(font, ReadingMetrics.TextLineHeight, _textWidth);
			AbsoluteLayout.SetLayoutBounds(label, new Rect(
				ReadingMetrics.TextPadding, ReadingMetrics.TextPadding + i * ReadingMetrics.TextLineHeight,
				_textWidth, ReadingMetrics.TextLineHeight));
			label.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
			_labels.Add(label);
			layers.Text.Children.Add(label);
		}

		_applied = Enumerable.Repeat(NotApplied, lines.Count).ToArray();
		Mark(layers, page, -1);
	}

	public void Mark(PageLayers layers, int page, int sentence)
	{
		if (page < 0 || page >= _pages.Count)
		{
			return;
		}

		IReadOnlyList<TextLine> lines = LinesOf(page);
		for (int i = 0; i < lines.Count && i < _labels.Count; i++)
		{
			int key = KeyFor(lines[i], sentence);
			if (_applied[i] != key)
			{
				_applied[i] = key;
				_labels[i].FormattedText = BuildLine(lines[i], sentence);
			}
		}
	}

	private static int KeyFor(TextLine line, int current)
	{
		if (current < 0 || line.Words.Count == 0 || line.FirstSentence > current)
		{
			return PlainKey;
		}

		return line.LastSentence < current ? ReadKey : current;
	}

	public int SentenceAt(int page, Point point)
	{
		if (page < 0 || page >= _pages.Count)
		{
			return -1;
		}

		int row = (int)Math.Floor((point.Y - ReadingMetrics.TextPadding) / ReadingMetrics.TextLineHeight);
		IReadOnlyList<TextLine> lines = LinesOf(page);
		if (row < 0 || row >= lines.Count || lines[row].Words.Count == 0)
		{
			return -1;
		}

		int word = WordIndexAtX(lines[row].Words, DistanceFromLineStart(point.X));
		return lines[row].SentenceOfWord[word];
	}

	// A right-to-left line starts at the card's right text edge and runs left: mirror the tap's x.
	private double DistanceFromLineStart(double x) =>
		rightToLeft ? _card.Width - ReadingMetrics.TextPadding - x : x - ReadingMetrics.TextPadding;

	// The word whose slot holds x (measured from where the line starts); past the end is the last word.
	private int WordIndexAtX(IReadOnlyList<WordAt> line, double x)
	{
		double space = measurer.Width(" ");
		double edge = 0;
		for (int i = 0; i < line.Count; i++)
		{
			edge += (i > 0 ? space : 0) + measurer.Width(line[i].Text);
			if (x <= edge)
			{
				return i;
			}
		}

		return line.Count - 1;
	}

	private IReadOnlyList<TextLine> LinesOf(int page)
	{
		if (!_lines.TryGetValue(page, out IReadOnlyList<TextLine>? lines))
		{
			FlowPage p = _pages[page];
			lines = LineWrapper.WrapParagraphs(text[p.Start..p.End], p.Start, WrapWidth, measurer)
				.Select(ToTextLine).ToList();
			_lines[page] = lines;
		}

		return lines;
	}

	private TextLine ToTextLine(IReadOnlyList<WordAt> words) =>
		new(words, words.Select(w => StorySentences.IndexAt(sentences, w.Start)).ToArray());

	// Consecutive words in the same state share one span, so a line is a handful of spans, not one per word.
	private FormattedString BuildLine(TextLine line, int current)
	{
		var formatted = new FormattedString();
		var run = new System.Text.StringBuilder();
		int runState = 0;
		for (int i = 0; i < line.Words.Count; i++)
		{
			int state = StateOf(line.SentenceOfWord[i], current);
			if (run.Length > 0 && state != runState)
			{
				formatted.Spans.Add(BuildSpan(run.ToString(), runState));
				run.Clear();
			}

			runState = state;
			run.Append(line.Words[i].Text).Append(i < line.Words.Count - 1 ? " " : string.Empty);
		}

		if (run.Length > 0)
		{
			formatted.Spans.Add(BuildSpan(run.ToString(), runState));
		}

		return formatted;
	}

	// -1 read, 0 unread, 1 the sentence being spoken.
	private static int StateOf(int sentence, int current)
	{
		if (current < 0)
		{
			return 0;
		}

		return sentence < current ? -1 : sentence == current ? 1 : 0;
	}

	private Span BuildSpan(string words, int state)
	{
		var span = new Span { Text = words, TextColor = _theme.Ink, FontFamily = font, FontSize = ReadingMetrics.FontSize };
		if (state < 0)
		{
			span.TextColor = _theme.ReadText;
		}
		else if (state > 0)
		{
			span.BackgroundColor = _theme.TextHighlight;
		}

		return span;
	}
}
