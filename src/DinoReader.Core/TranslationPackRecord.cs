namespace DinoReader.Core;

/// <summary>
/// Dino Reader's own record of the translation packs a grown-up downloaded (kept in preferences by the app). The child's screens ask this
/// record instead of the translation engine, because the engine (Google ML Kit) must not load until a grown-up asks for a translation.
/// The record can lag the phone (a pack from before the record existed): a grown-up path adds what the engine reports. Pure and thread-safe.
/// </summary>
public sealed class TranslationPackRecord
{
    private readonly object _gate = new();
    private readonly HashSet<string> _languages;

    public TranslationPackRecord(IEnumerable<string>? languages = null) =>
        _languages = new HashSet<string>((languages ?? []).Select(Normalise), StringComparer.Ordinal);

    /// <summary>The languages whose pack is recorded as on the phone, sorted.</summary>
    public IReadOnlyList<string> Languages
    {
        get { lock (_gate) { return [.. _languages.OrderBy(l => l, StringComparer.Ordinal)]; } }
    }

    /// <summary>Whether every one of <paramref name="languages"/> is recorded (true for none).</summary>
    public bool AllReady(IEnumerable<string> languages)
    {
        lock (_gate)
        {
            return languages.All(language => _languages.Contains(Normalise(language)));
        }
    }

    /// <summary>Records the pack as on the phone or as gone; true when the record changed.</summary>
    public bool Set(string language, bool ready)
    {
        lock (_gate)
        {
            return ready ? _languages.Add(Normalise(language)) : _languages.Remove(Normalise(language));
        }
    }

    /// <summary>Adds the packs the engine reports, never dropping one already recorded (a download may finish while the engine list is read); true when the record changed.</summary>
    public bool AddRange(IEnumerable<string> languages)
    {
        HashSet<string> reported = [.. languages.Select(Normalise)];
        lock (_gate)
        {
            int before = _languages.Count;
            _languages.UnionWith(reported);
            return _languages.Count != before;
        }
    }

    /// <summary>The preference text of the record.</summary>
    public string Serialize() => StoryLanguageSet.Join(Languages);

    public static TranslationPackRecord Parse(string? text) => new(StoryLanguageSet.Split(text));

    private static string Normalise(string language) => language.Trim().ToLowerInvariant();
}
