using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// Where an on-device translation engine spells a language differently from the app, the one table
/// that maps between them (Slice 103): the app says <c>nb</c> (Norwegian Bokmål), ML Kit says <c>no</c>.
/// Every other code is the same on both sides. Pure.
/// </summary>
public static class EngineLanguageCodes
{
    private static readonly Dictionary<string, string> AppToEngine = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nb"] = "no",
    };

    /// <summary>The code the engine knows a language by.</summary>
    public static string ToEngine(string appCode) =>
        AppToEngine.TryGetValue(AppLanguages.Canonical(appCode)!, out string? engine) ? engine : appCode;

    /// <summary>The app's code for a language the engine names.</summary>
    public static string FromEngine(string engineCode) =>
        AppToEngine.FirstOrDefault(pair => string.Equals(pair.Value, engineCode, StringComparison.OrdinalIgnoreCase)).Key ?? engineCode;
}
