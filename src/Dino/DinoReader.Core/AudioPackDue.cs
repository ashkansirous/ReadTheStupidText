namespace DinoReader.Core;

/// <summary>
/// Which pre-made audio packs a store book is due, by the one rule (Slice 267): audio follows only the family's languages (the main language and the story languages, all counted the
/// same). The language the book was written in is never added: a French and Farsi family asks for exactly <c>fr</c> and <c>fa</c>, no English.
/// </summary>
public static class AudioPackDue
{
    /// <summary>The listed packs of <paramref name="item"/> in <paramref name="familyLanguages"/>, one per language, in the family's order. A language the book has no pack for is left out.</summary>
    public static IReadOnlyList<(string Language, AudioPackInfo Pack)> For(StoreItem item, IEnumerable<string> familyLanguages) =>
        familyLanguages.Select(BundledTranslations.Key).Distinct(StringComparer.Ordinal)
            .Select(language => (Language: language, Pack: item.AudioIn(language)))
            .Where(wanted => wanted.Pack is not null)
            .Select(wanted => (wanted.Language, wanted.Pack!))
            .ToList();
}
