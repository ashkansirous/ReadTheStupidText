namespace ReadTheStupidText.Application.Documents;

/// <summary>Reads a <c>.txt</c> file's contents verbatim.</summary>
public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    private const string Extension = ".txt";

    public bool CanHandle(string extension) =>
        string.Equals(extension, Extension, StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(string filePath) => File.ReadAllTextAsync(filePath);
}
