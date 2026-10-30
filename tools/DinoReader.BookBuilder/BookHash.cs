using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DinoReader.BookBuilder;

/// <summary>
/// The hash of a book's content (Slice 157, Decision 162): its text (<c>story.md</c> or <c>lines.json</c>), page pictures, covers, translations, and the parts of <c>book.json</c>
/// that reach the package (id, title, ages, author, credits, colour, added date, cast, animals). The version is not in it: <c>check</c> compares the hash with the one stored
/// beside the version, so changing a book without <c>stamp</c> (which raises the version and stores the new hash) fails the check. Text files are hashed with LF line
/// endings, so a Windows checkout and a Linux one agree.
/// </summary>
internal static class BookHash
{
    private static readonly string[] TextExtensions = [".md", ".json"];

    public static string Of(BookFolder book)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(hash, "book", Encoding.UTF8.GetBytes(CanonicalInfo(book.Info)));
        foreach (string file in ContentFiles(book))
        {
            Add(hash, Path.GetRelativePath(book.Dir, file).Replace('\\', '/'), Read(file));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IEnumerable<string> ContentFiles(BookFolder book)
    {
        var files = new List<string>();
        files.AddRange(new[] { Path.Combine(book.Dir, BookFolder.StoryFile), book.LinesPath, book.CoverPath, book.CoverThumbPath }.Where(File.Exists));
        files.AddRange(StarterBuilder.PictureFilesIn(book.PicturesPath).OrderBy(Path.GetFileName, StringComparer.Ordinal));
        files.AddRange(FilesIn(book.TranslationsPath, ".json", BookFolder.PiecesFile));
        return files;
    }

    // Only the files the build reads: the page pictures (*.jpg) and the translation tables (<lang>.json, <lang>.speak.json), never pieces.json or a stray Thumbs.db.
    private static IEnumerable<string> FilesIn(string folder, string extension, string? excludedName) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder).Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(f) != excludedName).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            : [];

    private static string CanonicalInfo(BookInfo info)
    {
        var options = new JsonSerializerOptions(Books.JsonOptions) { WriteIndented = false };
        return JsonSerializer.Serialize(new
        {
            info.Id, info.Title, info.AgeMin, info.AgeMax, info.Author, info.Credits, info.Colour, info.Added, info.Cast, info.TextInPictures,
            Animals = info.Animals?.OrderBy(a => a.Key, StringComparer.Ordinal).ToDictionary(a => a.Key, a => a.Value),
            NotAnimals = info.NotAnimals?.Order(StringComparer.Ordinal).ToArray(),
        }, options);
    }

    private static void Add(IncrementalHash hash, string name, byte[] bytes)
    {
        hash.AppendData(Encoding.UTF8.GetBytes($"{name}\0{bytes.Length}\0"));
        hash.AppendData(bytes);
    }

    private static byte[] Read(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);
        return TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ? WithoutCarriageReturns(bytes) : bytes;
    }

    private static byte[] WithoutCarriageReturns(byte[] bytes)
    {
        var kept = new List<byte>(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            bool beforeLineFeed = bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n';
            if (!beforeLineFeed)
            {
                kept.Add(bytes[i]);
            }
        }

        return kept.ToArray();
    }
}
