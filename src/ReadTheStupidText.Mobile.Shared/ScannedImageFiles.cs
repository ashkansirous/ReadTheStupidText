using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Removes a scanned or photographed page image once its text has been extracted, so page images
/// do not pile up in app storage. Read The Stupid Text deletes them this way; Dino Reader (Slice 63)
/// instead copies them with <see cref="IPageImageKeeper"/> into its own story folder.
/// <para>
/// A kept image can never be deleted here: <c>KeepAsync</c> writes a new JPEG (decoded, rotated, scaled
/// and re-encoded) to the keeper's destination in the app's story folder, never the scanner's cache
/// folder the sources live in, and the sources are deleted only after the keeper has finished. The
/// two paths are different files in different folders, so deleting a source cannot touch the copy.
/// </para>
/// </summary>
public static class ScannedImageFiles
{
    /// <summary>
    /// Deletes the file behind a plain path or a <c>file://</c> uri string. Other schemes
    /// (for example <c>content://</c>) are not ours to delete. Never throws.
    /// </summary>
    public static void TryDelete(string location)
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
