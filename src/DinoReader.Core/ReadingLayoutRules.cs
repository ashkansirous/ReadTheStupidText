namespace DinoReader.Core;

/// <summary>The four shapes of the reading screen (Slice 98, Slice 174): upright, picture only held sideways, picture + text held sideways
/// (words over the picture) and text held sideways.</summary>
public enum ReadingLayout
{
    Upright,
    PictureSide,
    PictureOverlay,
    TextRow,
}

/// <summary>Which shape the reading screen takes, and which mode a book can offer (Slice 164 follow-up).</summary>
public static class ReadingLayoutRules
{
    public static ReadingLayout For(double width, double height, ReadingMode mode, bool isPictureTextBook)
    {
        if (width <= 0 || width <= height)
        {
            return ReadingLayout.Upright;
        }

        if (mode == ReadingMode.Text)
        {
            return ReadingLayout.TextRow;
        }

        if (!isPictureTextBook)
        {
            return ReadingLayout.Upright;
        }

        return mode == ReadingMode.PictureAndText ? ReadingLayout.PictureOverlay : ReadingLayout.PictureSide;
    }

    /// <summary>A mode the switch cannot offer (picture + text on a book whose pages do not change with the words) is folded to picture only.</summary>
    public static ReadingMode Offered(ReadingMode mode, bool offersPictureAndText) =>
        !offersPictureAndText && mode == ReadingMode.PictureAndText ? ReadingMode.PictureOnly : mode;
}
