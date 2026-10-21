using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The big two-icon switch of the reading screen (mock screen 3): open book for text mode, picture
/// frame for picture mode, always visible. The active cell is filled with a hard under-edge.
/// </summary>
public sealed class ModeSwitch : Border
{
	private const double CellWidth = 52;
	private const double CellHeight = 44;
	private const double CellCorner = 12;
	private const double EdgeDrop = 2;
	private const double IconSize = 24;
	private const double IconStroke = 2.2;
	private const double TrackCorner = 16;
	private const double TrackPadding = 4;

	private ReadingTheme _theme = ReadingTheme.For(false);

	public ModeSwitch()
	{
		StrokeThickness = 0;
		Padding = TrackPadding;
		StrokeShape = new RoundRectangle { CornerRadius = TrackCorner };
		Render();
	}

	public ReadingMode Mode { get; private set; } = ReadingMode.Text;

	public event EventHandler<ReadingMode>? ModeChanged;

	public void Set(ReadingMode mode, ReadingTheme theme)
	{
		Mode = mode;
		_theme = theme;
		Render();
	}

	private void Render()
	{
		BackgroundColor = _theme.ControlFill;
		Content = new HorizontalStackLayout
		{
			Spacing = TrackPadding,
			Children = { Cell(ReadingMode.Text, IconKind.Book, ReadingText.TextMode), Cell(ReadingMode.Picture, IconKind.Picture, ReadingText.PictureMode) },
		};
	}

	private View Cell(ReadingMode mode, IconKind icon, string label)
	{
		bool active = Mode == mode;
		var cell = new Grid { WidthRequest = CellWidth, HeightRequest = CellHeight + EdgeDrop };
		if (active)
		{
			cell.Children.Add(Face(_theme.SwitchEdge, EdgeDrop));
		}

		cell.Children.Add(Face(active ? _theme.SwitchActive : Colors.Transparent, 0));
		cell.Children.Add(new IconView
		{
			Kind = icon, Size = IconSize, StrokeWidth = IconStroke, VerticalOptions = LayoutOptions.Start,
			Margin = new Thickness(0, (CellHeight - IconSize) / 2, 0, 0),
			Color = active ? _theme.SwitchActiveIcon : _theme.ControlIcon,
		});
		SemanticProperties.SetDescription(cell, label);
		cell.GestureRecognizers.Add(Tap(mode));
		return cell;
	}

	private static Border Face(Color colour, double top) => new()
	{
		BackgroundColor = colour,
		StrokeThickness = 0,
		HeightRequest = CellHeight,
		VerticalOptions = LayoutOptions.Start,
		Margin = new Thickness(0, top, 0, 0),
		StrokeShape = new RoundRectangle { CornerRadius = CellCorner },
	};

	private TapGestureRecognizer Tap(ReadingMode mode)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => Choose(mode);
		return tap;
	}

	private void Choose(ReadingMode mode)
	{
		if (mode == Mode)
		{
			return;
		}

		Mode = mode;
		Render();
		ModeChanged?.Invoke(this, mode);
	}
}
