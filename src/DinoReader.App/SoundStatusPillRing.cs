namespace DinoReader;

/// <summary>The getting-sound pill's 24 px ring (Batch 50 frame 197): a pale track, a brand arc filled from 12 o'clock by <see cref="Progress"/>, and a small down arrow inside.</summary>
public sealed class SoundStatusPillRing : IDrawable
{
	public const double BoxSize = 24;

	private const float Centre = 12;
	private const float Radius = 9.5f;
	private const float RingStroke = 3;
	private const float ArrowStroke = 1.8f;
	private const float TopAngle = 90;
	private const float FullTurn = 360;

	// The mock's arrow: a stem from (12, 8) to (12, 14.5) and a head from (9.3, 12) via (12, 14.7) to (14.7, 12).
	private const float StemX = 12;
	private const float StemTop = 8;
	private const float StemBottom = 14.5f;
	private const float HeadLeftX = 9.3f;
	private const float HeadRightX = 14.7f;
	private const float HeadY = 12;
	private const float HeadTipY = 14.7f;

	// Tokens live in App.xaml, read when drawing so the colours follow the one definition.
	private static Color Track => DinoTokens.Get(DinoTokens.RingTrack);
	private static Color Arc => DinoTokens.Get(DinoTokens.Brand);

	/// <summary>0 to 1: how much of the run is done; 0 draws the track only.</summary>
	public double Progress { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		canvas.StrokeLineCap = LineCap.Round;
		canvas.StrokeSize = RingStroke;
		canvas.StrokeColor = Track;
		canvas.DrawCircle(Centre, Centre, Radius);
		if (Progress > 0)
		{
			canvas.StrokeColor = Arc;
			float end = TopAngle - (float)(FullTurn * Math.Min(Progress, 0.9999));
			canvas.DrawArc(Centre - Radius, Centre - Radius, Radius * 2, Radius * 2, TopAngle, end, true, false);
		}

		DrawArrow(canvas);
	}

	private static void DrawArrow(ICanvas canvas)
	{
		canvas.StrokeSize = ArrowStroke;
		canvas.StrokeColor = Arc;
		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.DrawLine(StemX, StemTop, StemX, StemBottom);
		var head = new PathF(HeadLeftX, HeadY);
		head.LineTo(StemX, HeadTipY);
		head.LineTo(HeadRightX, HeadY);
		canvas.DrawPath(head);
	}
}
