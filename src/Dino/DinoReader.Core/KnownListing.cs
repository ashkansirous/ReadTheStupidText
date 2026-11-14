using System.Text.Json;

namespace DinoReader.Core;

/// <summary>
/// Slice 360, "Expectation rule after review": which store listing is the known one for a book, and how the copy saved on the phone is read back. Pure.
/// </summary>
public static class KnownListing
{
    /// <summary>The most a saved listing may weigh (the same cap as the live list response).</summary>
    public const long MaxSavedBytes = 1024 * 1024;

    /// <summary>
    /// The book's entry in the known listing. When this launch read a live listing, only it counts (a book it leaves out is not in the store now, whatever the saved copy says); the saved copy
    /// is used only when no live listing was read this launch.
    /// </summary>
    public static StoreItem? Pick(bool liveListingRead, StoreItem? live, StoreItem? saved) => liveListingRead ? live : live ?? saved;

    /// <summary>Whether a live listing's text may replace the saved one: it must read back with at least one book (an empty or invalid listing never overwrites the last good one).</summary>
    public static bool IsSavable(string json) => ParseSaved(json) is { Count: > 0 };

    /// <summary>
    /// The readable entries of a saved listing by id, or null (no saved listing) when the text is blank, too big, not a list or does not parse. A partial failure never gives half a listing.
    /// </summary>
    public static IReadOnlyDictionary<string, StoreItem>? ParseSaved(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxSavedBytes)
        {
            return null;
        }

        try
        {
            var items = new Dictionary<string, StoreItem>(StringComparer.OrdinalIgnoreCase);
            foreach (StoreItem item in StoreCatalog.Readable(StoreCatalog.Parse(json)))
            {
                items[item.Id] = item;
            }

            return items;
        }
        catch (JsonException)
        {
            // no-fallback: a pure parser; the caller treats null as "no saved listing", deletes the file and reports it
            return null;
        }
    }
}
