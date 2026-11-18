using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>
/// The three end icons of the Settings "Books and buying" rows (Slice 399, mock SB9): an external-link square for Subscription, a counter-clockwise arrow for Restore purchases and a ticket for Redeem a code.
/// Each is drawn on the mock's 24-unit grid, 16 dp, stroke 2.4 in the Settings chevron's colour, the way <see cref="IconView"/> draws its kinds.
/// </summary>
internal static class BuyingRowIcons
{
	private const double GridSize = 24;
	private const double IconSize = 16;
	private const double StrokeWidth = 2.4;
	private const string ExternalLinkSquare = "M18 14v5a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 4 19V7.5A1.5 1.5 0 0 1 5.5 6H10";
	private const string ExternalLinkArrow = "M14 4h6v6 M20 4l-9 9";
	private const string RestoreArc = "M4 12a8 8 0 1 0 2.3-5.7";
	private const string RestoreHead = "M4 4.5V10h5.5";
	private const string TicketBody = "M3.5 8.5V6.5h17v2a2.5 2.5 0 0 0 0 5v2h-17v-2a2.5 2.5 0 0 0 0-5z";
	private const string TicketPerforation = "M10 6.5v11";
	private static readonly float[] PerforationDashes = [2, 2];
	private static readonly PathGeometryConverter Converter = new();

	public static View ExternalLink() => Icon(Layer(ExternalLinkSquare), Layer(ExternalLinkArrow));

	public static View Restore() => Icon(Layer(RestoreArc), Layer(RestoreHead));

	public static View Ticket() => Icon(Layer(TicketBody), Layer(TicketPerforation, PerforationDashes));

	private static AbsoluteLayout Icon(params Path[] layers)
	{
		var icon = new AbsoluteLayout
		{
			WidthRequest = IconSize,
			HeightRequest = IconSize,
			FlowDirection = FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
			InputTransparent = true,
		};
		foreach (Path layer in layers)
		{
			icon.Add(layer);
		}

		AutomationProperties.SetIsInAccessibleTree(icon, false); // the row's button carries the name
		return icon;
	}

	// One path on the 24-unit grid, scaled uniformly to the icon's size (the same method as IconView.GridLayer).
	private static Path Layer(string data, float[]? dashes = null)
	{
		var path = new Path
		{
			Data = (Geometry)Converter.ConvertFromInvariantString(data)!,
			Stroke = new SolidColorBrush(DinoTokens.Get(DinoTokens.Muted)),
			Fill = Brush.Transparent,
			StrokeThickness = StrokeWidth,
			StrokeLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Aspect = Stretch.None,
			AnchorX = 0,
			AnchorY = 0,
			Scale = IconSize / GridSize,
		};
		if (dashes is not null)
		{
			path.StrokeDashArray = new DoubleCollection { dashes[0], dashes[1] };
		}

		AbsoluteLayout.SetLayoutBounds(path, new Rect(0, 0, GridSize, GridSize));
		return path;
	}
}
