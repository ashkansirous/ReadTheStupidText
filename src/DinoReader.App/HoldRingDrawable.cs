namespace DinoReader;

/// <summary>The gate's hold ring (mock screen 6): a pale track circle with a brand-coloured arc that fills clockwise from the top.</summary>
public sealed class HoldRingDrawable : IDrawable
{
	private const float Radius = 54;
	private const float StrokeWidth = 8;
	private const float TopAngle = 90;
	private const float FullTurn = 360;

	private static readonly Color Track = Color.FromArgb("#ECE7FF");
	private static readonly Color Arc = Color.FromArgb("#5B57E8");

	/// <summary>0 to 1: how much of the hold has been done.</summary>
	public double Progress { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		float cx = dirtyRect.Center.X;
		float cy = dirtyRect.Center.Y;
		canvas.StrokeSize = StrokeWidth;
		canvas.StrokeColor = Track;
		canvas.DrawCircle(cx, cy, Radius);
		if (Progress > 0)
		{
			DrawProgress(canvas, cx, cy);
		}
	}

	private void DrawProgress(ICanvas canvas, float cx, float cy)
	{
		canvas.StrokeColor = Arc;
		canvas.StrokeLineCap = LineCap.Round;
		float end = TopAngle - (float)(FullTurn * Math.Min(Progress, 0.9999));
		canvas.DrawArc(cx - Radius, cy - Radius, Radius * 2, Radius * 2, TopAngle, end, true, false);
	}
}
