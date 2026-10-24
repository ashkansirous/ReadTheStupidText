using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The colours of the reading screen: the child's daytime set (mock screens 3, 4, 5) and the
/// bedtime set (screen 12), which the phone's dark mode picks. Built from <see cref="DinoTokens"/>.
/// </summary>
public sealed record ReadingTheme(
    Color Page,
    Color Ink,
    Color BackIcon,
    Color ControlFill,
    Color ControlIcon,
    Color SwitchActive,
    Color SwitchEdge,
    Color SwitchActiveIcon,
    Color TextCard,
    Color ReadText,
    Color TextHighlight,
    Color PageHighlight,
    Color BoxFill,
    Color BoxStroke,
    Color ScanCard,
    Color DotIdle,
    Color DotCurrent,
    Color SkipFill,
    Color SkipIcon,
    Color Play,
    Color PlayEdge,
    Color PlayGlyph,
    Color PlayHalo,
    Color PillFill,
    Color PillText,
    bool IsBedtime)
{
    public static ReadingTheme For(bool bedtime) => bedtime ? Bedtime() : Daytime();

    /// <summary>The background of generated page <paramref name="pageIndex"/>: pastel by day, one dim card at night.</summary>
    public Color PageCard(int pageIndex) =>
        IsBedtime ? DinoTokens.Get(DinoTokens.NightCard) : Color.FromArgb(PagePalette.ColourAt(pageIndex));

    private static ReadingTheme Daytime()
    {
        Color Get(string key) => DinoTokens.Get(key);
        return new ReadingTheme(
            Page: Get(DinoTokens.Cream), Ink: Get(DinoTokens.Ink), BackIcon: Get(DinoTokens.Ink),
            ControlFill: Get(DinoTokens.ControlFill), ControlIcon: Get(DinoTokens.Muted),
            SwitchActive: Get(DinoTokens.Brand), SwitchEdge: Get(DinoTokens.BrandEdge), SwitchActiveIcon: Colors.White,
            TextCard: Colors.White, ReadText: Get(DinoTokens.ReadDim), TextHighlight: Get(DinoTokens.SentenceFill), PageHighlight: Get(DinoTokens.PageHighlight),
            BoxFill: Get(DinoTokens.BoxFill), BoxStroke: Get(DinoTokens.BoxStroke), ScanCard: Get(DinoTokens.ScanCard),
            DotIdle: Get(DinoTokens.DotIdle), DotCurrent: Get(DinoTokens.Brand),
            SkipFill: Get(DinoTokens.BrandTint), SkipIcon: Get(DinoTokens.Brand),
            Play: Get(DinoTokens.Coral), PlayEdge: Get(DinoTokens.CoralEdge), PlayGlyph: Colors.White,
            PlayHalo: Get(DinoTokens.CoralHalo), PillFill: Get(DinoTokens.Ink), PillText: Colors.White, IsBedtime: false);
    }

    private static ReadingTheme Bedtime()
    {
        Color Get(string key) => DinoTokens.Get(key);
        return new ReadingTheme(
            Page: Get(DinoTokens.NightPage), Ink: Get(DinoTokens.NightInk), BackIcon: Get(DinoTokens.NightSoft),
            ControlFill: Get(DinoTokens.NightFill), ControlIcon: Get(DinoTokens.NightMuted),
            SwitchActive: Get(DinoTokens.NightBrand), SwitchEdge: Get(DinoTokens.NightBrandEdge),
            SwitchActiveIcon: Get(DinoTokens.NightPage),
            TextCard: Get(DinoTokens.NightCard), ReadText: Get(DinoTokens.NightMuted), TextHighlight: Get(DinoTokens.NightBoxFill), PageHighlight: Get(DinoTokens.NightPageHighlight),
            BoxFill: Get(DinoTokens.NightBoxFill), BoxStroke: Get(DinoTokens.NightBoxStroke), ScanCard: Get(DinoTokens.NightCard),
            DotIdle: Get(DinoTokens.NightDot), DotCurrent: Get(DinoTokens.NightBrand),
            SkipFill: Get(DinoTokens.NightFill), SkipIcon: Get(DinoTokens.NightBrand),
            Play: Get(DinoTokens.NightCoral), PlayEdge: Get(DinoTokens.NightCoralEdge), PlayGlyph: Get(DinoTokens.NightCoralGlyph),
            PlayHalo: Get(DinoTokens.NightCoralHalo), PillFill: Get(DinoTokens.NightCard), PillText: Get(DinoTokens.NightInk), IsBedtime: true);
    }
}
