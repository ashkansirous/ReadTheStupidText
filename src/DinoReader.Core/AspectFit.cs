namespace DinoReader.Core;

/// <summary>Where a picture lands inside a view that fits it whole (AspectFit): scaled to touch two edges and centred, so the other two are letterboxed.</summary>
public static class AspectFit
{
    /// <summary>The picture's displayed rectangle in the view's own units; the whole view when either size is empty.</summary>
    public static (double X, double Y, double Width, double Height) ContentRect(double viewWidth, double viewHeight, double imageWidth, double imageHeight)
    {
        if (viewWidth <= 0 || viewHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return (0, 0, Math.Max(viewWidth, 0), Math.Max(viewHeight, 0));
        }

        double scale = Math.Min(viewWidth / imageWidth, viewHeight / imageHeight);
        double width = imageWidth * scale;
        double height = imageHeight * scale;
        return ((viewWidth - width) / 2, (viewHeight - height) / 2, width, height);
    }
}
