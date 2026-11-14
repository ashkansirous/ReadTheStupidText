namespace ReadTheStupidText.Mobile;

/// <summary>The app's theme colours read from Colors.xaml, so code-built views share one lookup instead of repeating resource keys.</summary>
internal static class ThemeColors
{
    public const string TextPrimaryDarkKey = "TextPrimaryDark";
    public const string TextSecondaryLightKey = "TextSecondaryLight";
    public const string WaitFillLightKey = "WaitFillLight";
    public const string WaitFillDarkKey = "WaitFillDark";
    public const string WaitDetailDarkKey = "WaitDetailDark";
    public const string WaitCancelFillDarkKey = "WaitCancelFillDark";
    public const string WaitCancelTextDarkKey = "WaitCancelTextDark";

    private static readonly Color MissingColor = Colors.Gray;

    public static bool IsDark()
    {
        return Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    /// <summary>The colour stored under <paramref name="key"/>; grey when the resource or the application is missing, so a theme callback never throws.</summary>
    public static Color Get(string key)
    {
        ResourceDictionary? resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        return resources is not null && resources.TryGetValue(key, out object? value) && value is Color color ? color : MissingColor;
    }
}
