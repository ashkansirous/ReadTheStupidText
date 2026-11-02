namespace DinoReader.Core;

/// <summary>How many columns the Record screen has: one stack (held upright), two (a tablet held sideways) or three (a phone held sideways).</summary>
public enum RecordColumns
{
    Stacked,
    Two,
    Three,
}

/// <summary>
/// The Record screen's shape for one window (Slice 232 part C, mock Batch 44 frames 116 and 129): the number of columns, the picture column's width in dp (0 when the page has no
/// picture or the screen is stacked) and the right-hand rail's width in dp (0 unless there are three columns).
/// </summary>
public readonly record struct RecordShape(RecordColumns Columns, double PicturePane, double RailWidth);

/// <summary>The pure size rules of the Record screen held sideways. A page asks once per layout pass; it never recomputes a width.</summary>
public static class RecordLayout
{
    public const double PhoneRailWidth = 112;
    public const double PhoneButtonSize = 88;
    public const double PhoneWordsSize = 19;
    public const double TabletWordsSize = 28;
    public const double UprightWordsSize = 20;

    /// <summary>The gap between two columns in dp.</summary>
    public const double ColumnGap = 14;

    /// <summary>A phone's room at its edges held sideways: the camera cut-out inset on the start, 20 on the end.</summary>
    public const double PhoneStartPadding = LandscapeLayout.SidewaysInset;
    public const double PhoneEndPadding = 20;

    /// <summary>The narrowest the phone's middle column (title, words, Back, dots) may get: the picture gives way before it does.</summary>
    public const double MinMiddleWidth = 240;

    /// <summary>
    /// Phone held sideways: three columns (picture, words, rail with Done on top and the microphone in the middle). Tablet held sideways: two columns (picture, then everything else stacked).
    /// Held upright: the stack as it always was. <paramref name="pictureAspect"/> is the page picture's width over height; null means the page has no picture, 0 or less an unknown shape.
    /// </summary>
    public static RecordShape For(double width, double height, double? pictureAspect)
    {
        if (!LandscapeLayout.IsLandscape(width, height))
        {
            return new RecordShape(RecordColumns.Stacked, 0, 0);
        }

        double pane = pictureAspect is null ? 0 : LandscapeLayout.PicturePaneWidth(width, height, pictureAspect.Value);
        return LandscapeLayout.SizeOf(width, height) == DeviceSize.Phone
            ? new RecordShape(RecordColumns.Three, Math.Min(pane, PhonePictureRoom(width)), PhoneRailWidth)
            : new RecordShape(RecordColumns.Two, pane, 0);
    }

    // What the picture may take on a phone so the middle column keeps MinMiddleWidth: the window less the paddings, the rail and both gaps (beside the picture and beside the rail).
    // With no room at all the picture column is 0 wide, and then its gap is gone too, so the middle column only gets wider.
    private static double PhonePictureRoom(double width) =>
        Math.Max(0, width - PhoneStartPadding - PhoneEndPadding - PhoneRailWidth - 2 * ColumnGap - MinMiddleWidth);

    /// <summary>The words' font size in dp for a shape: 19 in the phone's middle column, 28 on a tablet, 20 upright.</summary>
    public static double WordsSize(RecordColumns columns) => columns switch
    {
        RecordColumns.Three => PhoneWordsSize,
        RecordColumns.Two => TabletWordsSize,
        _ => UprightWordsSize,
    };
}
