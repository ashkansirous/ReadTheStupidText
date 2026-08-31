namespace ReadTheStupidText.Application.Documents;

/// <summary>
/// Extracts the plain-text contents of an uploaded document so it can feed the
/// existing read pipeline like any other intercepted text. One implementation per
/// file type; <see cref="CompositeDocumentTextExtractor"/> routes by extension.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>Whether this extractor handles files with the given extension
    /// (e.g. <c>".txt"</c>, case-insensitive).</summary>
    bool CanHandle(string extension);

    Task<string> ExtractTextAsync(string filePath);
}
