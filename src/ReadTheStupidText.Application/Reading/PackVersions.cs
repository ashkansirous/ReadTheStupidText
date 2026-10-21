namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Orders pack versions (<see cref="PackNaming.IsValidVersion"/>: letters, digits and dots). Dotted
/// parts compare as numbers when both are numeric (so 10 is newer than 9), otherwise as text, and a
/// missing part counts as zero (so 1 and 1.0 are the same version).
/// </summary>
public static class PackVersions
{
    /// <summary>
    /// Whether a catalog pack should be downloaded: nothing installed serves the language yet, or the
    /// installed copy is the same pack and the catalog lists a newer version of it.
    /// </summary>
    public static bool ShouldInstall(PackInfo pack, InstalledVoicePack? installed) =>
        installed is null || (installed.Manifest.Id == pack.Id && IsNewer(pack.Version, installed.Manifest.Version));

    public static bool IsNewer(string candidate, string installed) => Compare(candidate, installed) > 0;

    public static int Compare(string left, string right)
    {
        string[] a = left.Split('.');
        string[] b = right.Split('.');
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int order = ComparePart(i < a.Length ? a[i] : "0", i < b.Length ? b[i] : "0");
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    private static int ComparePart(string left, string right) =>
        long.TryParse(left, out long x) && long.TryParse(right, out long y)
            ? x.CompareTo(y)
            : string.CompareOrdinal(left, right);
}
