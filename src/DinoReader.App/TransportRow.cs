using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Previous sentence, play or pause, next sentence (Slice 174, mock Batch 37 frames 91-93): three flat circles of one family, the coral play button a little
/// bigger than the tinted skip buttons, with no under-edge and no halo (a child who has wandered is told by the "reading page" pill instead).
/// </summary>
public sealed class TransportRow : HorizontalStackLayout
{
	private const double SkipSize = 64;
	private const double SkipIconSize = 28;
	private const double SkipIconStroke = 2.6;
	private const double PlaySize = 84;
	private const double PlayGlyphSize = 32;
	private const double Gap = 28;
	// Held sideways (frames 92 and 93): 52 / 68 buttons, 20 apart. SidewaysHeight is the room PictureTextPages keeps for the row.
	private const double CompactSkipSize = 52;
	private const double CompactSkipIconSize = 24;
	private const double CompactPlaySize = 68;
	private const double CompactPlayGlyphSize = 28;
	private const double CompactGap = 20;
	public const double SidewaysHeight = CompactPlaySize + 4 + 12;

	private readonly Border _previous = Circle(SkipSize);
	private readonly Border _next = Circle(SkipSize);
	private readonly Border _play = Circle(PlaySize);
	private readonly IconView _glyph = new() { Size = PlayGlyphSize, Filled = true, InputTransparent = true };
	private readonly IconView _previousIcon = SkipIcon(IconKind.SkipBack);
	private readonly IconView _nextIcon = SkipIcon(IconKind.SkipForward);
	private bool _compact;

	public TransportRow()
	{
		Spacing = Gap;
		HorizontalOptions = LayoutOptions.Center;
		Padding = new Thickness(0, 12, 0, 26);
		_previous.Content = _previousIcon;
		_next.Content = _nextIcon;
		_play.Content = _glyph;
		Children.Add(_previous);
		Children.Add(_play);
		Children.Add(_next);
		Wire(_previous, () => PreviousTapped?.Invoke(this, EventArgs.Empty), ReadingText.PreviousSentence);
		Wire(_next, () => NextTapped?.Invoke(this, EventArgs.Empty), ReadingText.NextSentence);
		Wire(_play, () => PlayTapped?.Invoke(this, EventArgs.Empty), ReadingText.Play);
	}

	/// <summary>The smaller row used when the page is held sideways.</summary>
	public bool Compact
	{
		get => _compact;
		set
		{
			if (value == _compact)
			{
				return;
			}

			_compact = value;
			Resize();
		}
	}

	private void Resize()
	{
		Spacing = _compact ? CompactGap : Gap;
		Padding = _compact ? new Thickness(0, 4, 0, 12) : new Thickness(0, 12, 0, 26);
		Size(_previous, _compact ? CompactSkipSize : SkipSize);
		Size(_next, _compact ? CompactSkipSize : SkipSize);
		Size(_play, _compact ? CompactPlaySize : PlaySize);
		_previousIcon.Size = _compact ? CompactSkipIconSize : SkipIconSize;
		_nextIcon.Size = _compact ? CompactSkipIconSize : SkipIconSize;
		_glyph.Size = _compact ? CompactPlayGlyphSize : PlayGlyphSize;
	}

	private static void Size(Border circle, double size)
	{
		circle.WidthRequest = size;
		circle.HeightRequest = size;
		circle.StrokeShape = new RoundRectangle { CornerRadius = size / 2 };
	}

	public event EventHandler? PreviousTapped;

	public event EventHandler? PlayTapped;

	public event EventHandler? NextTapped;

	public void Apply(SessionState state, ReadingTheme theme)
	{
		_previous.BackgroundColor = theme.SkipFill;
		_next.BackgroundColor = theme.SkipFill;
		_previousIcon.Color = theme.SkipIcon;
		_nextIcon.Color = theme.SkipIcon;
		_play.BackgroundColor = theme.Play;
		_glyph.Color = theme.PlayGlyph;
		_glyph.Kind = state == SessionState.Playing ? IconKind.Pause : IconKind.Play;
		_play.Opacity = state == SessionState.Loading ? 0.7 : 1;
		SemanticProperties.SetDescription(_play, Describe(state));
	}

	private static string Describe(SessionState state) => state switch
	{
		SessionState.Playing => ReadingText.Pause,
		SessionState.Loading => ReadingText.Loading,
		_ => ReadingText.Play,
	};

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
