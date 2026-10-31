using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>What the launch scan does with one pack's <c>.partial</c> file (Slice 208).</summary>
public enum StalePartialAction
{
    /// <summary>The settings still want the pack and the partial is the catalog's version: the app's normal start continues it from the partial file (HTTP Range append).</summary>
    Resume,

    /// <summary>Nothing is decided: the file stays and nothing resumes it (no catalog to compare with, a partial newer than the cached catalog, or a pack whose use the scan cannot tell).</summary>
    Keep,

    /// <summary>Nothing wants the pack any more, or the partial cannot be continued: the file is deleted to free the space.</summary>
    Delete,
}

/// <summary>A <c>.partial</c> file found in the packs folder: its name and the bytes on disk.</summary>
public sealed record FoundPartial(string FileName, long Length);

/// <summary>What the launch scan did: how many partials it deleted, and the wanted packs whose partial the app's normal start continues.</summary>
public sealed record StalePartialScan(int Deleted, IReadOnlyList<string> Resumable)
{
    public static StalePartialScan Empty { get; } = new(0, []);
}

/// <summary>The scan's verdict for one partial file.</summary>
public sealed record StalePartialDecision(string FileName, string PackId, StalePartialAction Action);

/// <summary>
/// The pure decision behind the launch scan for half-finished pack downloads (Slice 208), kept free of file and platform types
/// so it is unit-tested. A crash forgets the in-memory resume entries (Slice 182), and the launch only starts the current
/// language's packs, so a partial for another wanted pack would sit there forever. A partial of a wanted pack resumes, a partial of
/// a pack nothing wants is deleted, and so is one that cannot be continued (empty, or older than the catalog's version of the
/// pack: a republished pack never resumes onto stale bytes, so those bytes are dead weight).
/// </summary>
public static class StalePartialPolicy
{
    private const char VersionSeparator = '-';

    /// <param name="found">The partial files in the packs folder.</param>
    /// <param name="wantedPackIds">The ids of the packs the current settings still want.</param>
    /// <param name="catalogVersions">The catalog's current version of each pack; null when there is no catalog, and then every partial is kept.</param>
    /// <param name="undeterminedPackIds">Packs whose use the settings cannot show (translation, storyteller): their partials are kept, never resumed.</param>
    public static IReadOnlyList<StalePartialDecision> Decide(
        IEnumerable<FoundPartial> found,
        IReadOnlySet<string> wantedPackIds,
        IReadOnlyDictionary<string, string>? catalogVersions,
        IReadOnlySet<string>? undeterminedPackIds = null)
    {
        var decisions = new List<StalePartialDecision>();
        var resuming = new HashSet<string>(StringComparer.Ordinal);
        foreach (FoundPartial partial in found)
        {
            if (!TryParse(partial.FileName, out string packId, out string version))
            {
                continue; // not a pack download file: left alone
            }

            StalePartialAction action = ActionFor(partial, packId, version, wantedPackIds, catalogVersions, undeterminedPackIds);
            if (action == StalePartialAction.Resume && !resuming.Add(packId))
            {
                action = StalePartialAction.Delete; // a second partial of the same pack is a leftover of an older run
            }

            decisions.Add(new StalePartialDecision(partial.FileName, packId, action));
        }

        return decisions;
    }

    /// <summary>Splits <c>&lt;id&gt;-&lt;version&gt;.zip.partial</c>; false for any other name.</summary>
    public static bool TryParse(string fileName, out string packId, out string version)
    {
        packId = "";
        version = "";
        string suffix = PackNaming.ZipSuffix + PackNaming.PartialSuffix;
        if (!fileName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string stem = fileName[..^suffix.Length];
        int cut = stem.LastIndexOf(VersionSeparator); // a version never contains '-'
        if (cut <= 0)
        {
            return false;
        }

        packId = stem[..cut];
        version = stem[(cut + 1)..];
        return PackNaming.IsValidId(packId) && PackNaming.IsValidVersion(version);
    }

    private static StalePartialAction ActionFor(
        FoundPartial partial, string packId, string version, IReadOnlySet<string> wanted,
        IReadOnlyDictionary<string, string>? catalogVersions, IReadOnlySet<string>? undetermined)
    {
        if (catalogVersions is null || (undetermined?.Contains(packId) ?? false))
        {
            return StalePartialAction.Keep;
        }

        if (partial.Length <= 0 || !wanted.Contains(packId))
        {
            return StalePartialAction.Delete;
        }

        if (!catalogVersions.TryGetValue(packId, out string? current))
        {
            return StalePartialAction.Keep;
        }

        int order = PackVersions.Compare(version, current);
        return order < 0 ? StalePartialAction.Delete : order > 0 ? StalePartialAction.Keep : StalePartialAction.Resume;
    }
}
