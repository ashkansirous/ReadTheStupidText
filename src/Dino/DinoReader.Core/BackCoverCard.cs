namespace DinoReader.Core;

/// <summary>Where the back cover is shown, which picks the credits card's size (Slice 359 item 6, the 2026-11-13 mock BC1-BC3).</summary>
public enum BackCoverCardKind
{
    Phone,
    PhoneLandscape,
    Wide,
}

/// <summary>The credits card's width and its text sizes (title, credit lines).</summary>
public readonly record struct BackCoverCardSpec(double Width, double TitleSize, double LineSize);

/// <summary>The credits card laid over the drawn back-cover picture: one size per kind, never wider than the picture less 16 each side.</summary>
public static class BackCoverCard
{
    public const double PhoneWidth = 300;
    public const double PhoneLandscapeWidth = 340;
    public const double WideWidth = 360;
    public const double PictureMargin = 16; // kept free each side of the card

    private const double PhoneTitleSize = 20;
    private const double PhoneLineSize = 16;
    private const double LandscapeTitleSize = 19;
    private const double LandscapeLineSize = 15;
    private const double WideTitleSize = 18;
    private const double WideLineSize = 18;

    public static BackCoverCardKind KindFor(bool wideScreen, bool landscape) =>
        wideScreen ? BackCoverCardKind.Wide : landscape ? BackCoverCardKind.PhoneLandscape : BackCoverCardKind.Phone;

    public static BackCoverCardSpec For(BackCoverCardKind kind, double pictureWidth)
    {
        (double width, double title, double line) = kind switch
        {
            BackCoverCardKind.Wide => (WideWidth, WideTitleSize, WideLineSize),
            BackCoverCardKind.PhoneLandscape => (PhoneLandscapeWidth, LandscapeTitleSize, LandscapeLineSize),
            _ => (PhoneWidth, PhoneTitleSize, PhoneLineSize),
        };
        return new BackCoverCardSpec(Math.Max(0, Math.Min(width, pictureWidth - 2 * PictureMargin)), title, line);
    }

    /// <summary>
    /// The card at its fixed sizes, which grows in height with its text. Only when that height would exceed the stage height less 32 do the title and the credit lines step
    /// down together (one point each per step, to a floor) until the card fits.
    /// </summary>
    public static BackCoverCardSpec Fit(BackCoverCardKind kind, double stageWidth, double stageHeight, string title, IReadOnlyList<string> lines)
    {
        BackCoverCardSpec spec = For(kind, stageWidth);
        double room = stageHeight - 2 * PictureMargin;
        while (EstimatedHeight(spec, title, lines) > room && (spec.TitleSize > MinTitleSize || spec.LineSize > MinLineSize))
        {
            spec = spec with { TitleSize = Math.Max(MinTitleSize, spec.TitleSize - FontStep), LineSize = Math.Max(MinLineSize, spec.LineSize - FontStep) };
        }

        return spec;
    }

    /// <summary>The card's height at a spec: padding, the wrapped title, the rule and the wrapped credit lines (wrapping estimated from text length, erring towards more lines).</summary>
    public static double EstimatedHeight(BackCoverCardSpec spec, string title, IReadOnlyList<string> lines)
    {
        double inner = Math.Max(1, spec.Width - HorizontalPadding);
        double height = VerticalPadding + WrappedLines(title, spec.TitleSize, inner) * spec.TitleSize * TitleLineHeight + RuleBlockHeight + ChildGap;
        foreach (string line in lines)
        {
            height += ChildGap + WrappedLines(line, spec.LineSize, inner) * spec.LineSize * CreditLineHeight;
        }

        return height;
    }

    private static double WrappedLines(string text, double size, double innerWidth) =>
        Math.Max(1, Math.Ceiling(WidthInEm(text) * size / innerWidth));

    /// <summary>The text's width in em: full-width characters (CJK, kana, Hangul, full-width forms) count one em, everything else the average glyph width.</summary>
    internal static double WidthInEm(string text)
    {
        double width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            width += IsFullWidth(text[i]) ? FullWidthGlyph : AverageGlyphWidth;
        }

        return width;
    }

    private static bool IsFullWidth(char c) =>
        c is >= 'ᄀ' and <= 'ᅟ' // Hangul Jamo
        or >= '⺀' and <= '꓏' // CJK radicals, kana, CJK symbols and ideographs, Yi
        or >= '가' and <= '힣' // Hangul syllables
        or >= '豈' and <= '﫿' // CJK compatibility ideographs
        or >= '︰' and <= '﹯' // CJK compatibility forms
        or >= '＀' and <= '｠' // full-width forms
        or >= '￠' and <= '￦';

    private const double MinTitleSize = 12;
    private const double MinLineSize = 10;
    private const double FontStep = 1;
    private const double HorizontalPadding = 44; // 22 each side
    private const double VerticalPadding = 38; // 18 top, 20 bottom
    private const double RuleBlockHeight = 19; // the 3 high rule with 8 above and below
    private const double ChildGap = 2;
    private const double TitleLineHeight = 1.25;
    private const double CreditLineHeight = 1.55;
    private const double AverageGlyphWidth = 0.6; // of the font size
    private const double FullWidthGlyph = 1.0; // of the font size
}

/// <summary>The drawn back-cover picture at cover size, shifted so the crop sits a little above centre.</summary>
public readonly record struct BackCoverPictureFrame(double Width, double Height, double OffsetY);

/// <summary>Where the back-cover picture sits in its stage: filling it, with 40% of the cut from the top and 60% from the bottom (a head near the top stays in).</summary>
public static class BackCoverPicture
{
    public const double AspectWidthOverHeight = 0.75; // the shipped pictures are 3:4
    public const double CutFromTop = 0.4;

    /// <summary>The frame for a picture of the given width/height pixel aspect; zero or less means the shipped 3:4.</summary>
    public static BackCoverPictureFrame Frame(double stageWidth, double stageHeight, double aspectWidthOverHeight = 0)
    {
        double aspect = aspectWidthOverHeight > 0 ? aspectWidthOverHeight : AspectWidthOverHeight;
        double width = Math.Max(stageWidth, stageHeight * aspect);
        double height = width / aspect;
        return new BackCoverPictureFrame(width, height, -(height - stageHeight) * CutFromTop);
    }
}
