using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>What a scan does next about a page the built-in recognizer could not read (Slice 87).</summary>
public enum PageLanguageStep
{
    /// <summary>Nothing to download: use the reads as they are.</summary>
    Proceed,

    /// <summary>Ask which language the page is in.</summary>
    AskLanguage,

    /// <summary>The catalog could not be fetched, so the page cannot be read yet: keep the editor text and offer Retry.</summary>
    NeedsConnection,
}

/// <summary>The catalog's answer for a pending page: its fetch status and the languages to offer.</summary>
public sealed record PageLanguageOffer(PackDownloadResult CatalogStatus, IReadOnlyList<string> Languages);

/// <summary>
/// Decides when a scan asks which language the page is in (Slice 87), and which languages it offers.
/// The built-in recognizer cannot tell Farsi from Arabic before a pack exists, so the person says.
/// A scan asks when any page came back flagged <see cref="OcrSource.MlKitPackPending"/>; the offer is
/// every language of a catalog OCR pack that is not written in Latin and is not installed yet, those
/// written in the script the page was detected in first, then catalog order. Pure.
/// </summary>
public static class PageLanguagePrompt
{
    public static bool NeedsPack(IEnumerable<OcrSource> sources) =>
        sources.Contains(OcrSource.MlKitPackPending);

    /// <summary>True when a re-read page is final; a page still flagged pending was not helped by the download.</summary>
    public static bool IsResolved(OcrSource source) => source != OcrSource.MlKitPackPending;

    /// <summary>The non-Latin script most letters of the pending pages are in, or null when they hold none.</summary>
    public static TextScript? PageScript(IEnumerable<ScriptShare> pendingPages) =>
        pendingPages
            .SelectMany(share => share.Counts)
            .Where(pair => pair.Key is not (TextScript.Latin or TextScript.Unknown))
            .GroupBy(pair => pair.Key, pair => pair.Value)
            .Select(group => (Script: group.Key, Letters: group.Sum()))
            .OrderByDescending(entry => entry.Letters)
            .Select(entry => (TextScript?)entry.Script)
            .FirstOrDefault();

    /// <summary>The languages to offer; empty means there is nothing to download, so no question is asked.</summary>
    public static IReadOnlyList<string> Offer(IEnumerable<OcrSource> sources, TextScript? pageScript, IEnumerable<PackInfo> catalog, IEnumerable<InstalledVoicePack> installed)
    {
        if (!NeedsPack(sources))
        {
            return [];
        }

        List<InstalledVoicePack> packs = installed.ToList();

        return catalog
            .Where(p => p.Kind == PackKind.Ocr)
            .SelectMany(p => p.Languages)
            .Where(IsNonLatin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(language => !PackSelection.Serving(packs, PackKind.Ocr, language).Any())
            .OrderByDescending(language => pageScript is not null && ScriptDetector.ScriptOf(language) == pageScript)
            .ToList();
    }

    /// <summary>What to do once the catalog has answered for a scan that holds a pending page.</summary>
    public static PageLanguageStep Decide(PageLanguageOffer offer)
    {
        if (offer.CatalogStatus != PackDownloadResult.Ok)
        {
            return PageLanguageStep.NeedsConnection;
        }

        return offer.Languages.Count > 0 ? PageLanguageStep.AskLanguage : PageLanguageStep.Proceed;
    }

    // Only a language the script table places in a non-Latin script is offered. One it does not list
    // (Turkish, Vietnamese, Urdu, Thai...) is skipped: the built-in recognizer reads the Latin ones,
    // and the catalog does not say which of the rest need a pack.
    private static bool IsNonLatin(string language) =>
        ScriptDetector.ScriptOf(language) is not (TextScript.Latin or TextScript.Unknown);
}
