using System.Text.RegularExpressions;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Strict naming rules for catalog pack ids and versions, which become file and
/// folder names on the device, so a hostile or typo'd catalog can neither escape
/// the packs folder nor make one pack's cleanup touch another's files.
/// A version never contains '-', which keeps <c>&lt;id&gt;-&lt;version&gt;.zip</c> unambiguous.
/// </summary>
public static partial class PackNaming
{
    public const string ZipSuffix = ".zip";
    public const string PartialSuffix = ".partial";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[0-9A-Za-z.]{1,32}$")]
    private static partial Regex VersionPattern();

    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    public static bool IsValidVersion(string? version) => version is not null && VersionPattern().IsMatch(version);

    public static string ZipFileName(string id, string version) => $"{id}-{version}{ZipSuffix}";

    /// <summary>
    /// True when <paramref name="fileName"/> is exactly <c>&lt;id&gt;-&lt;version&gt;.zip</c> or
    /// <c>.zip.partial</c> for this id. A longer id sharing the prefix (supertonic-3-xx-1.zip
    /// for supertonic-3) leaves a '-' in the version slot and so never matches.
    /// </summary>
    public static bool IsOrphanFileName(string fileName, string id)
    {
        string stem = StripSuffix(fileName);
        string prefix = id + "-";
        return stem.Length > prefix.Length
            && stem.StartsWith(prefix, StringComparison.Ordinal)
            && IsValidVersion(stem[prefix.Length..]);
    }

    private static string StripSuffix(string fileName)
    {
        if (fileName.EndsWith(ZipSuffix + PartialSuffix, StringComparison.Ordinal))
        {
            return fileName[..^(ZipSuffix.Length + PartialSuffix.Length)];
        }

        return fileName.EndsWith(ZipSuffix, StringComparison.Ordinal) ? fileName[..^ZipSuffix.Length] : "";
    }
}
