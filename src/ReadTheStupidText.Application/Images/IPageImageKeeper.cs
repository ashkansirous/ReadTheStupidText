namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Keeps a scanned or photographed page as a lasting file (Slice 63): upright, JPEG, long edge
/// capped near <see cref="MaxLongEdge"/>. Dino Reader keeps its pages this way; Read The Stupid
/// Text still deletes them after OCR.
/// </summary>
public interface IPageImageKeeper
{
    const int MaxLongEdge = 1600;

    /// <summary>Writes the image at <paramref name="location"/> (plain path, file:// or content:// uri) to <paramref name="destinationPath"/>.</summary>
    Task KeepAsync(string location, string destinationPath);
}
