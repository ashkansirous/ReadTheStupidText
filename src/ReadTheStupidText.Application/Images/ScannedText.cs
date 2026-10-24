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

    /// <summary>The 1-based numbers of the pages that contributed text to <see cref="JoinPages"/> (not failed, not blank).</summary>
    public static IReadOnlyList<int> ContributingPageNumbers(IReadOnlyList<string?> pageTexts) =>
        Enumerable.Range(0, pageTexts.Count)
            .Where(i => !string.IsNullOrWhiteSpace(pageTexts[i]))
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

/// <summary>Where a captured page is in the background OCR (Slice 100).</summary>
public enum CapturedPageState
{
    Reading,
    Read,
    Failed,
}

/// <summary>One captured page: where its image is, and what its OCR gave once settled.</summary>
public sealed class CapturedPage
{
    public CapturedPage(Guid id, string location)
    {
        Id = id;
        Location = location;
    }

    public Guid Id { get; }

    public string Location { get; }

    public CapturedPageState State { get; internal set; } = CapturedPageState.Reading;

    public string? Text { get; internal set; }
}

/// <summary>
/// The pages of the capture wizard, in the order they will be read (Slice 100). Pure bookkeeping:
/// pages are added as they arrive, settle one by one as their OCR finishes (in any order), can be
/// removed, and read out as the per-page texts <see cref="ScannedText"/> joins. A removed page's
/// late result is ignored.
/// </summary>
public sealed class CapturedPageList
{
    private readonly List<CapturedPage> _pages = [];

    public IReadOnlyList<CapturedPage> Pages => _pages;

    public int Count => _pages.Count;

    public CapturedPage Add(string location)
    {
        var page = new CapturedPage(Guid.NewGuid(), location);
        _pages.Add(page);
        return page;
    }

    /// <summary>Removes the page and returns it, or null when it is not there.</summary>
    public CapturedPage? Remove(Guid id)
    {
        CapturedPage? page = Find(id);
        if (page is not null)
        {
            _pages.Remove(page);
        }

        return page;
    }

    /// <summary>Records the page's OCR text; a null text marks a page whose OCR failed. Ignored for a removed page.</summary>
    public void Settle(Guid id, string? text)
    {
        CapturedPage? page = Find(id);
        if (page is null)
        {
            return;
        }

        page.Text = text;
        page.State = text is null ? CapturedPageState.Failed : CapturedPageState.Read;
    }

    /// <summary>The pages' texts in order, null for a failed page; the shape <see cref="ScannedText.JoinPages"/> takes.</summary>
    public IReadOnlyList<string?> Texts() =>
        _pages.Select(page => page.State == CapturedPageState.Read ? page.Text : null).ToList();

    private CapturedPage? Find(Guid id) => _pages.FirstOrDefault(page => page.Id == id);
}
