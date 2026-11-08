namespace DinoReader.Core;

/// <summary>Whether a check-only trigger (a pick, a mode change, a pack change) must rebuild the shelf tiles (Slice 323): only when the languages they were built in differ from the languages now.</summary>
public static class ShelfRefreshRule
{
    /// <summary>Nothing built yet: false until the page has appeared (the first build is its own, after the starters), true after it (a failed first build is tried again). Otherwise true only when a language differs.</summary>
    public static bool ShouldRebuild(string? builtAppLanguage, string? builtTitleLanguage, string appLanguage, string titleLanguage, bool pageAppeared) =>
        builtAppLanguage is null || builtTitleLanguage is null
            ? pageAppeared
            : builtAppLanguage != appLanguage || builtTitleLanguage != titleLanguage;
}
