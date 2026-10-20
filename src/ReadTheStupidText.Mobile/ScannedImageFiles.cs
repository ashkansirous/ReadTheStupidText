using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Removes a scanned or photographed page image once its text has been extracted, so page images
/// do not pile up in app storage. Slice 63 will add a setting to keep them instead.
/// </summary>
internal static class ScannedImageFiles
{
    /// <summary>
    /// Deletes the file behind a plain path or a <c>file://</c> uri string. Other schemes
    /// (for example <c>content://</c>) are not ours to delete. Never throws.
    /// </summary>
    internal static void TryDelete(string location)
    {
        try
        {
            string? path = ScannedText.ToLocalPath(location);
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Cleanup is best effort; a leftover cache file is harmless.
        }
    }
}
