namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>The no-Wi-Fi icon of the waiting-for-internet look (Slice 187): three arcs, a dot and a slash, drawn in one colour.</summary>
public sealed class NoWifiIcon : GraphicsView
{
    private const double IconSize = 44;

    public NoWifiIcon(Color color)
    {
        Drawable = new NoWifiDrawable(color);
        WidthRequest = IconSize;
        HeightRequest = IconSize;
        SemanticProperties.SetDescription(this, string.Empty);
    }

    public void Recolor(Color color)
    {
        Drawable = new NoWifiDrawable(color);
        Invalidate();
    }

    private sealed class NoWifiDrawable(Color color) : IDrawable
    {
        private const float Stroke = 3.2f;
        private static readonly float[] Radii = [0.22f, 0.4f, 0.58f];

        public void Draw(ICanvas canvas, RectF dirty)
        {
            float centreX = dirty.Width / 2;
            float baseY = dirty.Height * 0.78f;
            canvas.StrokeColor = color;
            canvas.FillColor = color;
            canvas.StrokeSize = Stroke;
            canvas.StrokeLineCap = LineCap.Round;
            foreach (float radius in Radii)
            {
                float r = dirty.Width * radius;
                canvas.DrawArc(centreX - r, baseY - r, r * 2, r * 2, 45, 135, false, false);
            }

            canvas.FillCircle(centreX, baseY, 2.6f);
            canvas.DrawLine(dirty.Width * 0.14f, dirty.Height * 0.1f, dirty.Width * 0.86f, dirty.Height * 0.9f);
        }
    }
}
