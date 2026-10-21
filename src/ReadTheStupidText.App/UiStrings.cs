using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using Microsoft.UI.Xaml;

namespace ReadTheStupidText_App;

/// <summary>
/// Looks up UI copy from <c>Strings\&lt;lang&gt;\Resources.resw</c> for code-behind (XAML uses
/// <c>x:Uid</c>) and applies the chosen language (Slice 57). The lower layers return data; the words
/// live here in the UI layer.
/// </summary>
internal static class UiStrings
{
    private const string EnglishTag = "en-US";
    private const string ResourcesMapPrefix = "Resources/";

    private static ResourceLoader? _loader;
    private static ResourceContext? _englishContext;
    private static string? _languageCode;

    public static string Get(string key)
    {
        _loader ??= new ResourceLoader();
        string value = _loader.GetString(key);
        return string.IsNullOrEmpty(value) ? GetEnglish(key) : value;
    }

    // A key missing in the current language shows its en-US text, never the raw key.
    private static string GetEnglish(string key)
    {
        var manager = new ResourceManager();
        _englishContext ??= CreateEnglishContext(manager);
        ResourceCandidate? candidate = manager.MainResourceMap.TryGetValue(ResourcesMapPrefix + key.Replace('.', '/'), _englishContext);
        return candidate?.ValueAsString ?? key;
    }

    private static ResourceContext CreateEnglishContext(ResourceManager manager)
    {
        ResourceContext context = manager.CreateResourceContext();
        context.QualifierValues["Language"] = EnglishTag;
        return context;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// Makes <paramref name="code"/> the language for resources loaded from now on. Windows already
    /// built keep their text (x:Uid is applied once), so a change shown in a built window restarts the app.
    /// </summary>
    public static void ApplyLanguage(string? code)
    {
        string? tag = UiLanguageTag.For(code);
        if (tag is null)
        {
            return;
        }

        ApplicationLanguages.PrimaryLanguageOverride = tag;
        _languageCode = code;
        _loader = null;
    }

    /// <summary>The flow direction for the language applied at startup, or the device's when none is stored.</summary>
    public static FlowDirection CurrentFlowDirection =>
        UiLanguageTag.IsRightToLeft(_languageCode, CultureInfo.CurrentUICulture.Name)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
}
