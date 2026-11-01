using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Which packs the launch scan treats as wanted, and which as undecidable, before <see cref="StalePartialPolicy"/> decides each partial file (Slice 208,
/// pulled out of <see cref="MobileVoicePackService"/> in Slice 225 so the rule is unit-tested). Free of file and platform types.
/// </summary>
internal static class StalePartialDecider
{
    /// <summary>
    /// The wanted packs are exactly the ones the app's own start picks for each language (VoicePackSelector, OcrPackSelector): one source of truth, so a
    /// pack the start would never resume is not "wanted" and a selected pack is never deleted. A translation pair, Dino's storyteller voice and a scan
    /// pack of a language not asked for here (a scanned page can be in any language) are not decided by the settings seen here: their partial is kept.
    /// </summary>
    internal static IReadOnlyList<StalePartialDecision> DecideAll(IEnumerable<FoundPartial> found, IReadOnlyList<PackInfo> packs, IReadOnlyCollection<string> languages)
    {
        IReadOnlySet<string> wanted = languages
            .SelectMany(l => new[] { VoicePackSelector.Select(packs, l), OcrPackSelector.Select(packs, l) })
            .OfType<PackInfo>().Select(p => p.Id).ToHashSet();
        IReadOnlySet<string> undetermined = packs
            .Where(p => p.Kind is PackKind.Translation or PackKind.Storyteller || (p.Kind == PackKind.Ocr && !wanted.Contains(p.Id)))
            .Select(p => p.Id).ToHashSet();
        IReadOnlyDictionary<string, string> versions = packs.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().Version);
        return StalePartialPolicy.Decide(found, wanted, versions, undetermined);
    }
}
