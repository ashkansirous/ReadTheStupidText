namespace DinoReader.Core;

/// <summary>The status bar icon colour rule (Slice 325 A4): light icons over the corner's ink header, else by the page behind the bar.</summary>
public static class StatusBarIcons
{
    /// <summary>True when the corner's ink header runs under the status bar: grown-up mode on a bar chrome (a rail has no header on top). Slice 349: the page behind the bar is then ink itself, so the icons follow what is drawn.</summary>
    public static bool InkUnderBar(bool grownUp, bool rail) => grownUp && !rail;

    /// <summary>True when the icons should be dark. Ink under the bar always wants light icons; otherwise a light page wants dark ones.</summary>
    public static bool UseDarkIcons(bool inkUnderBar, bool lightBackground) => !inkUnderBar && lightBackground;
}
