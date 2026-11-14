namespace DinoReader.Core;

/// <summary>
/// How the Record screen's words card keeps its words inside it (Slice 279). A book is cut into pieces once, against one fixed reference card (the iPhone 8 card of the mock, frame 183), so a piece has the
/// same key on every phone, tablet, window and rotation. A window whose card is smaller than the reference shows the same words at a smaller font; a larger one shows them at the normal size.
/// </summary>
public static class RecordCardFit
{
    /// <summary>The window the pieces are cut for: the iPhone 8 screen of the mock (375 x 664, so the card's most height is 332).</summary>
    public const double ReferenceWindowWidth = 375;
    public const double ReferenceWindowHeight = 664;

    /// <summary>The font never goes below this size, so words stay readable; a window that small scrolls nothing, it just reads small.</summary>
    public const double MinFontSize = 13;

    /// <summary>The font size to show a piece cut for the reference card in a card whose words area is <paramref name="availableWidth"/> by <paramref name="availableHeight"/>:
    /// <paramref name="referenceFont"/> when the reference fits, else scaled down by the tighter side (the line count stays, so the height scales with the font), never below <see cref="MinFontSize"/>.</summary>
    public static double FontSize(double referenceFont, double referenceWidth, double referenceHeight, double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0 || referenceWidth <= 0 || referenceHeight <= 0)
        {
            return referenceFont;
        }

        double scale = Math.Min(1, Math.Min(availableWidth / referenceWidth, availableHeight / referenceHeight));
        return Math.Max(Math.Min(MinFontSize, referenceFont), referenceFont * scale);
    }
}
