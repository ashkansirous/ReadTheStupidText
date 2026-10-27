using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// German is an app and reading language since Slice 168, but the shipped books (starters and the free store) get their German
/// pieces and titles in Slice 169. Until then the "every language is translated" checks skip these codes; Slice 169 empties the list.
/// </summary>
internal static class GermanBooksPending
{
    public static readonly string[] Languages = ["de"];

    /// <summary>The supported languages the shipped books must already be translated into.</summary>
    public static IEnumerable<string> BookLanguages => UiLanguage.Supported.Except(Languages);
}
