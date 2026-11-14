using Sirous.Reading.Application.Usage;

namespace DinoReader.Core;

/// <summary>
/// The <c>fallback</c> count of Dino Reader (Slice 273): its detail is <c>&lt;area&gt;:&lt;reason&gt;</c>, both from the closed <see cref="FallbackArea"/> and <see cref="FallbackReason"/> lists
/// the Worker also holds. No free text, file name or book text can be carried.
/// </summary>
public static class FallbackUsage
{
    private const char Separator = ':';

    /// <summary>The detail of a <c>fallback</c> count; null when either half is not on its closed list, so nothing outside the lists is ever queued.</summary>
    public static string? Detail(string area, string reason) =>
        FallbackArea.All.Contains(area) && FallbackReason.All.Contains(reason) ? string.Concat(area, Separator, reason) : null;

    /// <summary>The voices a book suggested that the roster does not hold at all (plain membership: a voice that is in the roster but taken or of the other gender is not missing). Empty and null suggestions are none.</summary>
    public static IReadOnlyList<string> MissingFromRoster(IEnumerable<string?> suggested, IEnumerable<string> rosterIds)
    {
        var roster = new HashSet<string>(rosterIds);
        return suggested.OfType<string>().Where(id => id.Length > 0 && !roster.Contains(id)).Distinct().ToList();
    }
}
