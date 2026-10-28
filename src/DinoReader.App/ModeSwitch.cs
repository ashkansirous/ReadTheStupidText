using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The three-way view switch of the reading screen (Slice 151, mock Batch 31 screens 66-68): picture only, picture and
/// text, text only, always in that order, each a 44 x 40 segment named for TalkBack and VoiceOver. The chosen one is lifted
/// on a white (by night, brand) fill. A book without pictures has only text, so the page hides the switch for it.
/// </summary>
public sealed class ModeSwitch : Border
{
	private const double CellWidth = 44;
	private const double CellHeight = 40;
	private const double CellCorner = 13;
	private const double IconSize = 22;
	private const double IconStroke = 2.2;
	private const double TrackCorner = 16;
	private const double TrackPadding = 3;
	private const double TrackGap = 2;
	private const double ChipCellWidth = 40;
	private const double ChipCellHeight = 38;
	private const float ChipAlpha = 0.92f;
	private const float ChipShadowOpacity = 0.18f;
	private const float ShadowOpacity = 0.16f;
	private const float ShadowRadius = 4;
	private const float ShadowOffsetY = 1;

	private static readonly PathGeometryConverter Converter = new();

	// Path data copied from the mock's inline SVGs (rects rewritten as arcs).
	private const string PictureOnlyPath = "M6.5 4.5h11a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3h-11a3 3 0 0 1-3-3v-9a3 3 0 0 1 3-3z M7.2 10a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0-3.6 0 M4 17l5-4.5 4 3.5 3-2.5 4 3.5";
	private const string PictureAndTextPath = "M6 3.5h12a2.5 2.5 0 0 1 2.5 2.5v5a2.5 2.5 0 0 1-2.5 2.5H6a2.5 2.5 0 0 1-2.5-2.5V6A2.5 2.5 0 0 1 6 3.5z M6 17h12 M6 20.5h8";
	private const string TextOnlyPath = "M5 6h14 M5 10.5h14 M5 15h14 M5 19.5h9";

	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _onPicture;

	public ModeSwitch()
	{
		StrokeThickness = 0;
		Padding = TrackPadding;
		StrokeShape = new RoundRectangle { CornerRadius = TrackCorner };
		Render();
	}

	public ReadingMode Mode { get; private set; } = ReadingMode.PictureAndText;

	/// <summary>False for a book whose pages the reader draws the same with or without words (scanned pages with line boxes): the picture + text segment is left out, and that mode shows as picture only.</summary>
	public bool OffersPictureAndText { get; set; } = true;

	/// <summary>Floating over a picture (mock Batch 37 frame 92): the switch sits on the same white chip as the back button, with smaller segments, so it reads on any picture.</summary>
	public bool OnPicture
	{
		get => _onPicture;
		set
		{
			if (value != _onPicture)
			{
				_onPicture = value;
				Render();
			}
		}
	}

	public event EventHandler<ReadingMode>? ModeChanged;

	public void Set(ReadingMode mode, ReadingTheme theme)
	{
		Mode = mode;
		_theme = theme;
		Render();
	}

	private ReadingMode ShownMode => !OffersPictureAndText && Mode == ReadingMode.PictureAndText ? ReadingMode.PictureOnly : Mode;

	private void Render()
	{
		BackgroundColor = _onPicture ? _theme.TextCard.WithAlpha(ChipAlpha) : _theme.ControlFill;
		Shadow = _onPicture ? new Shadow { Brush = Brush.Black, Opacity = ChipShadowOpacity, Radius = ShadowRadius * 1.5f, Offset = new Point(0, ShadowOffsetY * 2) } : null;
		Content = new HorizontalStackLayout
		{
			Spacing = TrackGap,
		};
		AddCells((HorizontalStackLayout)Content);
	}

	private void AddCells(HorizontalStackLayout row)
	{
		row.Children.Add(Cell(ReadingMode.PictureOnly, PictureOnlyPath, ReadingText.PictureOnlyMode));
		if (OffersPictureAndText)
		{
			row.Children.Add(Cell(ReadingMode.PictureAndText, PictureAndTextPath, ReadingText.PictureAndTextMode));
		}

		row.Children.Add(Cell(ReadingMode.Text, TextOnlyPath, ReadingText.TextOnlyMode));
	}

	private View Cell(ReadingMode mode, string icon, string label)
	{
		bool active = ShownMode == mode;
		var cell = new Border
		{
			WidthRequest = _onPicture ? ChipCellWidth : CellWidth,
			HeightRequest = _onPicture ? ChipCellHeight : CellHeight,
			StrokeThickness = 0,
			BackgroundColor = active ? ActiveFill() : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = CellCorner },
			Shadow = active ? new Shadow { Brush = Brush.Black, Opacity = ShadowOpacity, Radius = ShadowRadius, Offset = new Point(0, ShadowOffsetY) } : null,
			Content = Glyph(icon, active ? ActiveIcon() : _theme.ControlIcon),
		};
		SemanticProperties.SetDescription(cell, label);
		SemanticProperties.SetHint(cell, active ? ReadingText.ModeSelected : null);
		cell.GestureRecognizers.Add(Tap(mode));
		return cell;
	}

	// By day the chosen segment is white with a brand icon; by night the brand fill with its own icon colour.
	// On the chip the track is already white, so the chosen segment is the tinted fill instead.
	private Color ActiveFill() => _onPicture ? _theme.ControlFill : _theme.IsBedtime ? _theme.SwitchActive : Colors.White;

	private Color ActiveIcon() => _theme.IsBedtime && !_onPicture ? _theme.SwitchActiveIcon : _theme.SwitchActive;

	private static Microsoft.Maui.Controls.Shapes.Path Glyph(string data, Color colour) => new()
	{
		Data = (Geometry)Converter.ConvertFromInvariantString(data)!,
		Stroke = new SolidColorBrush(colour),
		Fill = Brush.Transparent,
		StrokeThickness = IconStroke,
		StrokeLineCap = PenLineCap.Round,
		StrokeLineJoin = PenLineJoin.Round,
		Aspect = Stretch.Uniform,
		WidthRequest = IconSize,
		HeightRequest = IconSize,
		HorizontalOptions = LayoutOptions.Center,
		VerticalOptions = LayoutOptions.Center,
		InputTransparent = true,
	};

	private TapGestureRecognizer Tap(ReadingMode mode)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => Choose(mode);
		return tap;
	}

	private void Choose(ReadingMode mode)
	{
		if (mode == ShownMode)
		{
			return;
		}

		Mode = mode;
		Render();
		ModeChanged?.Invoke(this, mode);
	}
}
