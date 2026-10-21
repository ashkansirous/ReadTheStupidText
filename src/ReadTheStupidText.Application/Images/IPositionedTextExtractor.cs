namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Extracts text together with its position on the page (Slice 63), so picture mode can box the
/// spoken sentence over the real page. Sits beside <see cref="IImageTextExtractor"/>, whose text-only
/// call stays for Read The Stupid Text.
/// </summary>
public interface IPositionedTextExtractor
{
    /// <summary>The pages of the file at <paramref name="path"/>, each with its lines in reading order.</summary>
    Task<IReadOnlyList<PositionedPage>> ExtractPagesAsync(string path);
}
