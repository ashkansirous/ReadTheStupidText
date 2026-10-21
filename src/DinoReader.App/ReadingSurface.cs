#if ANDROID
using PlatformTextMeasurer = DinoReader.AndroidTextMeasurer;
#elif IOS
using PlatformTextMeasurer = DinoReader.IosTextMeasurer;
#elif WINDOWS
using PlatformTextMeasurer = DinoReader.WindowsTextMeasurer;
#endif
using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The story's pages with their pictures and recognised lines, and how a picture's file name becomes a path
/// (Slice 64). Picture mode draws the real pages from it; null for a story without pictures.</summary>
public sealed record ScanSource(IReadOnlyList<StoryPage> Pages, Func<string?, string?> ResolveImage);

/// <summary>What the surface shows: the story text, its sentences and the font its language is set in.</summary>
public sealed record ReadingContent(string Text, IReadOnlyList<SentenceSpan> Sentences, string Font, bool RightToLeft,
	ScanSource? Scans = null);

/// <summary>
/// The page card of the reading screen: one page at a time in picture or text mode, the spoken
/// sentence marked, a swipe to turn pages and a tap on a sentence to read from there. It lays the
/// story out once the card has a size and again when the size or mode changes (a size change always
/// waits until the card has stopped resizing); the layout is pure geometry, so it is computed on a
/// worker thread, one layout at a time and the newest request wins, while the page already shown stays
/// on screen. A layout that fails keeps what was shown (or says so on an empty card) and is tried
/// again on the next size or mode change. A theme change only re-colours.
/// </summary>
public sealed class ReadingSurface : Border
{
	private static readonly TimeSpan ResizeSettle = TimeSpan.FromMilliseconds(150);

	private readonly AbsoluteLayout _boxes = new() { InputTransparent = true };
	private readonly AbsoluteLayout _text = new() { InputTransparent = true };
	private readonly PageLayers _layers;
	private ReadingContent? _content;
	private ReadingMode _mode = ReadingMode.Text;
	private ReadingTheme? _theme;
	private IPageRenderer? _renderer;
	private int _page;
	private int _sentence = -1;
	private Size _laidOutFor;
	private int _anchor = -1;
	private int _anchorPosition = -1;
	private int _position = -1;
	private bool _composing;
	private bool _dirty;
	private IDispatcherTimer? _resizeTimer;
	private PlatformTextMeasurer? _measurer;
	private string? _measurerFont;
	private readonly Label _error = new()
	{
		Text = ReadingText.LayoutFailed,
		IsVisible = false,
		HorizontalTextAlignment = TextAlignment.Center,
		VerticalOptions = LayoutOptions.Center,
		Margin = new Thickness(24),
		InputTransparent = true,
	};

	public ReadingSurface()
	{
		_layers = new PageLayers(_boxes, _text);
		Content = new Grid { Children = { _boxes, _text, _error } };
		StrokeThickness = 0;
		SizeChanged += (_, _) => OnSizeChanged();
		GestureRecognizers.Add(BuildTap());
		GestureRecognizers.Add(BuildSwipe(SwipeDirection.Left));
		GestureRecognizers.Add(BuildSwipe(SwipeDirection.Right));
	}

	public int PageCount => _renderer?.PageCount ?? 0;

	public int CurrentPage => _page;

	/// <summary>The child turned the page by hand (swipe); the argument is the page now shown.</summary>
	public event EventHandler<int>? PageSwiped;

	/// <summary>The child tapped a sentence; the argument is its index.</summary>
	public event EventHandler<int>? SentenceTapped;

	/// <summary>Raised after the pages were laid out again (a new size or mode): the page count may have changed.
	/// The page shown is the one holding the sentence the old page began with.</summary>
	public event EventHandler? LaidOut;

	public void Show(ReadingContent content, ReadingMode mode, ReadingTheme theme)
	{
		_content = content;
		_mode = mode;
		_theme = theme;
		_error.TextColor = theme.Ink;
		FlowDirection = content.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
		_renderer = null;
		_anchor = -1;
		_anchorPosition = -1;
		_position = -1;
		_laidOutFor = default;
		_ = RelayoutAsync();
	}

	// The page of the old mode stays up until the new layout is adopted.
	public void SetMode(ReadingMode mode)
	{
		if (mode == _mode)
		{
			return;
		}

		_mode = mode;
		_laidOutFor = default;
		_ = RelayoutAsync();
	}

	/// <summary>Re-colours the page shown; the layout is kept.</summary>
	public void SetTheme(ReadingTheme theme)
	{
		_theme = theme;
		_error.TextColor = theme.Ink;
		if (_renderer is null)
		{
			return;
		}

		_renderer.SetTheme(theme);
		ShowPage(_page);
	}

	/// <summary>Marks the sentence being spoken; <paramref name="position"/> is the story index being spoken inside
	/// it. It does not turn the page: see <see cref="ShowPage"/>.</summary>
	public void Mark(int sentence, int position = -1)
	{
		_sentence = sentence;
		_position = position;
		_renderer?.Mark(_layers, _page, sentence);
	}

	public int FirstSentenceOnPage(int page) => _renderer?.FirstSentenceOf(page) ?? -1;

	/// <summary>The page holding the sentence (the one holding <paramref name="position"/> when the sentence runs
	/// over several), or -1 while no layout has been adopted yet.</summary>
	public int PageOfSentence(int sentence, int position = -1) => _renderer?.PageOf(sentence, position) ?? -1;

	public void ShowPage(int page)
	{
		if (_renderer is null || _renderer.PageCount == 0)
		{
			return;
		}

		_page = Math.Clamp(page, 0, _renderer.PageCount - 1);
		_anchor = _renderer.FirstSentenceOf(_page);
		_anchorPosition = _renderer.FirstPositionOf(_page);
		BackgroundColor = _renderer.CardColour(_page);
		_renderer.DrawPage(_layers, _page);
		_renderer.Mark(_layers, _page, _sentence);
	}

	// The first layout (and a retry after a failed one) runs at once; any other size change, a
	// compose in flight included, waits for the resizing to settle.
	private void OnSizeChanged()
	{
		if (_content is null || _theme is null || Width <= 0 || Height <= 0)
		{
			return;
		}

		if (_renderer is null && !_composing)
		{
			_ = RelayoutAsync();
			return;
		}

		if (new Size(Width, Height) != _laidOutFor)
		{
			RestartResizeTimer();
		}
	}

	private void RestartResizeTimer()
	{
		if (_resizeTimer is null)
		{
			_resizeTimer = Dispatcher.CreateTimer();
			_resizeTimer.Interval = ResizeSettle;
			_resizeTimer.IsRepeating = false;
			_resizeTimer.Tick += (_, _) => _ = RelayoutAsync();
		}

		_resizeTimer.Stop();
		_resizeTimer.Start();
	}

	// One layout runs at a time. A request that arrives while one is composing marks it stale; the
	// stale result is dropped and the loop lays out again for the newest size, mode and theme.
	private async Task RelayoutAsync()
	{
		_dirty = true;
		if (_composing)
		{
			return;
		}

		_composing = true;
		try
		{
			while (_dirty)
			{
				_dirty = false;
				await LayoutOnceAsync();
			}
		}
		finally
		{
			_composing = false;
		}
	}

	// The page held afterwards is the one holding the text the old page began with (so a wanderer
	// stays where they were), else the marked one. A failure keeps the previous renderer.
	private async Task LayoutOnceAsync()
	{
		if (_content is null || _theme is null || Width <= 0 || Height <= 0)
		{
			return;
		}

		var card = new Size(Width, Height);
		if (_renderer is not null && card == _laidOutFor)
		{
			return;
		}

		IPageRenderer renderer = CreateRenderer(_content, _theme);
		bool composed = await TryComposeAsync(renderer, card);
		if (_dirty)
		{
			return;
		}

		if (composed)
		{
			Adopt(renderer, card);
		}
		else
		{
			_error.IsVisible = _renderer is null;
		}
	}

	private static async Task<bool> TryComposeAsync(IPageRenderer renderer, Size card)
	{
		try
		{
			await Task.Run(() => renderer.Compose(card));
			return true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not lay the story out: {ex}");
			return false;
		}
	}

	private void Adopt(IPageRenderer renderer, Size card)
	{
		renderer.SetTheme(_theme!);
		_renderer = renderer;
		_laidOutFor = card;
		_error.IsVisible = false;
		StrokeShape = new RoundRectangle { CornerRadius = renderer.CornerRadius };
		int held = _anchor >= 0 ? renderer.PageOf(_anchor, _anchorPosition) : renderer.PageOf(_sentence, _position);
		ShowPage(Math.Max(0, held));
		LaidOut?.Invoke(this, EventArgs.Empty);
	}

	private IPageRenderer CreateRenderer(ReadingContent content, ReadingTheme theme)
	{
		PlatformTextMeasurer measurer = MeasurerFor(content.Font);
		if (_mode != ReadingMode.Picture)
		{
			return new TextPages(content.Text, content.Sentences, measurer, content.Font, theme, content.RightToLeft);
		}

		return content.Scans is { } scans
			? new ScanPages(content.Text, scans.Pages, content.Sentences, scans.ResolveImage, measurer, content.Font, theme,
				e => OnTapped(this, e), e => OnSwiped(this, e))
			: new PicturePages(content.Text, content.Sentences, measurer, content.Font, theme);
	}

	// One measurer per font and size, kept while the surface lives and let go with it.
	private PlatformTextMeasurer MeasurerFor(string font)
	{
		if (_measurer is null || _measurerFont != font)
		{
			_measurer?.Dispose();
			_measurer = new PlatformTextMeasurer(font, ReadingMetrics.FontSize);
			_measurerFont = font;
		}

		return _measurer;
	}

	protected override void OnHandlerChanging(HandlerChangingEventArgs args)
	{
		base.OnHandlerChanging(args);
		if (args.NewHandler is null)
		{
			_measurer?.Dispose();
			_measurer = null;
			_measurerFont = null;
		}
	}

	private TapGestureRecognizer BuildTap()
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += OnTapped;
		return tap;
	}

	private SwipeGestureRecognizer BuildSwipe(SwipeDirection direction)
	{
		var swipe = new SwipeGestureRecognizer { Direction = direction };
		swipe.Swiped += OnSwiped;
		return swipe;
	}

	private void OnTapped(object? sender, TappedEventArgs e)
	{
		Point? point = e.GetPosition(this);
		if (_renderer is null || point is not { } p)
		{
			return;
		}

		int sentence = _renderer.SentenceAt(_page, p);
		if (sentence >= 0)
		{
			SentenceTapped?.Invoke(this, sentence);
		}
	}

	// Swipe left turns to the next page in a left-to-right layout; the direction mirrors for right-to-left.
	private void OnSwiped(object? sender, SwipedEventArgs e)
	{
		if (_renderer?.IsZoomed == true)
		{
			return;
		}

		bool toNext = (e.Direction == SwipeDirection.Left) != (FlowDirection == FlowDirection.RightToLeft);
		int target = Math.Clamp(_page + (toNext ? 1 : -1), 0, Math.Max(0, PageCount - 1));
		if (_renderer is null || target == _page)
		{
			return;
		}

		ShowPage(target);
		PageSwiped?.Invoke(this, _page);
	}
}
