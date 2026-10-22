namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Which languages some voice on this phone can read (Slice 82, Decision 89): the phone's own
/// text-to-speech voices, plus the natural voice packs that are installed or offered by the
/// catalog. A language neither covers is never offered, so text is not read by a voice that
/// would skip its script.
/// </summary>
public sealed class VoiceAvailability
{
    private readonly HashSet<string>? _phone;
    private readonly HashSet<string> _natural;

    /// <param name="phoneLanguages">Language codes (primary subtag, any case) the phone TTS can speak;
    /// null when the phone can't be asked, which is not the same as an empty list (reads nothing).</param>
    /// <param name="naturalLanguages">Language codes served by an installed or catalog-offered natural pack.</param>
    public VoiceAvailability(IEnumerable<string>? phoneLanguages, IEnumerable<string> naturalLanguages)
    {
        _phone = phoneLanguages is null ? null : Normalise(phoneLanguages);
        _natural = Normalise(naturalLanguages);
    }

    /// <summary>Whether the phone's voices were successfully listed. When not, nothing is hidden or blocked.</summary>
    public bool PhoneKnown => _phone is not null;

    /// <summary>Whether the phone voice can read this language; true when the phone can't be asked.</summary>
    public bool CanPhoneRead(string languageCode) => _phone is null || _phone.Contains(Primary(languageCode));

    /// <summary>Whether a natural voice is installed or on offer for this language.</summary>
    public bool CanNaturalRead(string languageCode) => _natural.Contains(Primary(languageCode));

    /// <summary>Whether any voice can read this language.</summary>
    public bool CanRead(string languageCode) => CanPhoneRead(languageCode) || CanNaturalRead(languageCode);

    /// <summary>The primary language subtag, lower case: "en-GB" and "EN" both give "en".</summary>
    public static string Primary(string languageCode)
    {
        int separator = languageCode.IndexOfAny(['-', '_']);
        string primary = separator < 0 ? languageCode : languageCode[..separator];
        return primary.Trim().ToLowerInvariant();
    }

    private static HashSet<string> Normalise(IEnumerable<string> codes) =>
        codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Primary).ToHashSet();
}
