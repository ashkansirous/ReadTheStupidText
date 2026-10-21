using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Previous sentence, play or pause, next sentence (mock screens 3 and 5). Play is the big coral
/// circle with a hard under-edge; while the child has wandered to another page it also carries a
/// soft halo that says "press me to jump back".
/// </summary>
public sealed class TransportRow : HorizontalStackLayout
{
	private const double SkipSize = 72;
	private const double SkipIconSize = 30;
	private const double SkipIconStroke = 2.6;
	private const double PlaySize = 104;
	private const double PlayEdgeDrop = 8;
	private const double PlayHaloGrow = 10;
	private const double PlayGlyphSize = 28;
	private const double Gap = 26;

	private readonly Border _previous = Circle(SkipSize);
	private readonly Border _next = Circle(SkipSize);
	private readonly Border _halo = Circle(PlaySize + 2 * PlayHaloGrow);
	private readonly Border _edge = Circle(PlaySize);
	private readonly Border _front = Circle(PlaySize);
	private readonly IconView _glyph = new() { Size = PlayGlyphSize, Filled = true, InputTransparent = true };
	private readonly IconView _previousIcon = SkipIcon(IconKind.SkipBack);
	private readonly IconView _nextIcon = SkipIcon(IconKind.SkipForward);

	public TransportRow()
	{
		Spacing = Gap;
		HorizontalOptions = LayoutOptions.Center;
		Padding = new Thickness(0, 12, 0, 26);
		_previous.Content = _previousIcon;
		_next.Content = _nextIcon;
		_front.Content = _glyph;
		Children.Add(_previous);
		Children.Add(BuildPlay());
		Children.Add(_next);
		Wire(_previous, () => PreviousTapped?.Invoke(this, EventArgs.Empty), ReadingText.PreviousSentence);
		Wire(_next, () => NextTapped?.Invoke(this, EventArgs.Empty), ReadingText.NextSentence);
		Wire(_front, () => PlayTapped?.Invoke(this, EventArgs.Empty), ReadingText.Play);
	}

	public event EventHandler? PreviousTapped;

	public event EventHandler? PlayTapped;

	public event EventHandler? NextTapped;

	public void Apply(SessionState state, bool wandering, ReadingTheme theme)
	{
		_previous.BackgroundColor = theme.SkipFill;
		_next.BackgroundColor = theme.SkipFill;
		_previousIcon.Color = theme.SkipIcon;
		_nextIcon.Color = theme.SkipIcon;
		_front.BackgroundColor = theme.Play;
		_edge.BackgroundColor = theme.PlayEdge;
		_halo.BackgroundColor = theme.PlayHalo;
		_halo.IsVisible = wandering;
		_glyph.Color = theme.PlayGlyph;
		_glyph.Kind = state == SessionState.Playing ? IconKind.Pause : IconKind.Play;
		_front.Opacity = state == SessionState.Loading ? 0.7 : 1;
		SemanticProperties.SetDescription(_front, Describe(state));
	}

	private static string Describe(SessionState state) => state switch
	{
		SessionState.Playing => ReadingText.Pause,
		SessionState.Loading => ReadingText.Loading,
		_ => ReadingText.Play,
	};

	private AbsoluteLayout BuildPlay()
	{
		var play = new AbsoluteLayout { WidthRequest = PlaySize, HeightRequest = PlaySize + PlayEdgeDrop };
		Place(play, _halo, -PlayHaloGrow, -PlayHaloGrow, PlaySize + 2 * PlayHaloGrow);
		Place(play, _edge, 0, PlayEdgeDrop, PlaySize);
		Place(play, _front, 0, 0, PlaySize);
		_halo.InputTransparent = true;
		_edge.InputTransparent = true;
		return play;
	}

	private static void Place(AbsoluteLayout layout, View view, double x, double y, double size)
	{
		AbsoluteLayout.SetLayoutBounds(view, new Rect(x, y, size, size));
		layout.Children.Add(view);
	}

	private static Border Circle(double size) => new()
	{
		WidthRequest = size,
		HeightRequest = size,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
	};

	private static IconView SkipIcon(IconKind kind) =>
		new() { Kind = kind, Size = SkipIconSize, StrokeWidth = SkipIconStroke, InputTransparent = true };

	private static void Wire(Border button, Action onTap, string description)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => onTap();
		button.GestureRecognizers.Add(tap);
		SemanticProperties.SetDescription(button, description);
	}
}
