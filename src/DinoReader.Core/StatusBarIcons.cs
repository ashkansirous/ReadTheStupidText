namespace DinoReader.Core;

/// <summary>The status bar icon colour rule (Slice 325 A4): light icons over the corner's ink header, else by the page behind the bar.</summary>
public static class StatusBarIcons
{
    /// <summary>True when the icons should be dark. Ink under the bar always wants light icons; otherwise a light page wants dark ones.</summary>
    public static bool UseDarkIcons(bool inkUnderBar, bool lightBackground) => !inkUnderBar && lightBackground;
}
