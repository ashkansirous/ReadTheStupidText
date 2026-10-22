using System.Globalization;

namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Pure helpers for turning per-page OCR results into editor text (Slice 45). A page whose OCR
/// failed or found nothing is represented by a null or blank entry, so one bad page never costs
/// the rest of the scan.
/// </summary>
public static class ScannedText
{
    private const string Separator = "\n\n";

    /// <summary>
    /// Joins the pages' trimmed, non-empty texts (in page order, blank-line separated). Returns an
    /// empty string when no page has text.
    /// </summary>
    public static string JoinPages(IReadOnlyList<string?> pageTexts) =>
        string.Join(Separator, pageTexts
            .Select(text => text?.Trim())
            .Where(text => !string.IsNullOrEmpty(text)));

    /// <summary>The 1-based numbers of the pages whose entry is null (the OCR call failed).</summary>
    public static IReadOnlyList<int> FailedPageNumbers(IReadOnlyList<string?> pageTexts) =>
        Enumerable.Range(0, pageTexts.Count)
            .Where(i => pageTexts[i] is null)
            .Select(i => i + 1)
            .ToList();

    /// <summary>
    /// Converts a plain path or a <c>file://</c> uri string (percent-encoded) to a local path.
    /// Returns null for any other scheme, which is not ours to touch.
    /// </summary>
    public static string? ToLocalPath(string location)
    {
        if (!location.Contains("://", StringComparison.Ordinal))
        {
            return location;
        }

        return Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) && uri.IsFile
            ? uri.LocalPath
            : null;
    }

    /// <summary>Formats page numbers as "3, 9" for a message.</summary>
    public static string FormatPageNumbers(IReadOnlyList<int> pages) =>
        string.Join(", ", pages.Select(p => p.ToString(CultureInfo.InvariantCulture)));
}
