using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>The pieces both picture renderers draw: a generated page's line label and the translucent box that
/// marks a spoken line (mock screens 2 and 3 share the box).</summary>
public static class PageArt
{
	private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);

	public static Rect ToRect(NormalizedRect r, Size card) =>
		new(r.X * card.Width, r.Y * card.Height, r.Width * card.Width, r.Height * card.Height);

	public static Label Label(ComposedLine line, Size card, string font, ReadingTheme theme)
	{
		Rect bounds = ToRect(line.TextBounds, card);
		Label label = ReadingMetrics.LineLabel(font, bounds.Height, bounds.Width);
		label.Text = line.Text;
		label.TextColor = theme.Ink;
		AbsoluteLayout.SetLayoutBounds(label, bounds);
		return label;
	}

	public static Border Box(Rect bounds, ReadingTheme theme)
	{
		var box = new Border
		{
			BackgroundColor = theme.BoxFill,
			Stroke = theme.BoxStroke,
			StrokeThickness = ReadingMetrics.BoxStrokeWidth,
			StrokeShape = new RoundRectangle { CornerRadius = ReadingMetrics.BoxCorner },
			InputTransparent = true,
			Opacity = 0,
		};
		AbsoluteLayout.SetLayoutBounds(box, bounds);
		_ = box.FadeToAsync(1, (uint)FadeIn.TotalMilliseconds);
		return box;
	}
}
