using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>The print-size pictures of one book that make its high-res pack (Slice 375 B): one per page, in page order, plus the cover and the back cover when the book has them.</summary>
internal sealed record HighResFiles(IReadOnlyList<string> Pages, string? Cover, string? BackCover)
{
    /// <summary>Every file, for the content hash (sorted by path so the order never depends on the file system).</summary>
    public IEnumerable<string> All => Pages.Concat([Cover, BackCover]).OfType<string>().OrderBy(p => p, StringComparer.Ordinal);

    /// <summary>True when any file is still a Git LFS pointer (the objects were not pulled): the set is complete in git but cannot be packed.</summary>
    public bool HasPointers => All.Any(HighResPack.IsLfsPointer);
}

/// <summary>
/// Slice 375 B: the second package of a store book. <c>story-v&lt;n&gt;.hd.dino</c> has the same version, text and story.json as <c>story-v&lt;n&gt;.dino</c> but carries the
/// 1792x2400 pictures from <c>content/dino/&lt;stem&gt;/high-res/</c>, and exists only when that folder holds every page, the cover and the back cover the book has
/// (a book with only a <c>high-res/back-cover.jpg</c> ships the standard pack alone). A Git LFS pointer in place of a picture (the folder was never pulled) counts as missing, so a
/// checkout without the objects builds the standard pack and never a broken high-res one.
/// </summary>
internal static class HighResPack
{
    public const string Directory = "high-res";
    public const string HdMarker = ".hd";
    public const string MetaKey = "hdBytes";

    private const string LfsPointerStart = "version https://git-lfs";
    private const string OidPrefix = "oid sha256:";
    private const double PictureAspect = 0.75;
    private const double AspectTolerance = 0.01;
    private const long MaxPictureBytes = 1_500_000; // review #1: a picture over this was never re-saved at JPEG 85 (prepare-story-pictures.ps1 -HighRes), so the pack would be several times too big

    public static string HdPackageFileName(int version) => $"story-v{version}{HdMarker}.dino";

    public static string FolderOf(BookFolder book) => Path.Combine(book.Dir, Directory);

    /// <summary>The high-res pictures when the folder holds every one the book needs, else null.</summary>
    public static HighResFiles? Find(BookFolder book)
    {
        IReadOnlyList<string> pages = StarterBuilder.PictureFilesIn(book.PicturesPath);
        if (pages.Count == 0)
        {
            return null;
        }

        string folder = FolderOf(book);
        List<string> pagePaths = pages.Select(p => Path.Combine(folder, Path.GetFileName(p))).ToList();
        string? cover = File.Exists(book.CoverPath) ? Path.Combine(folder, BookFolder.CoverFile) : null;
        string? back = File.Exists(book.BackCoverPath) ? Path.Combine(folder, BackCover.PictureName) : null;
        bool complete = pagePaths.Concat([cover, back]).OfType<string>().All(File.Exists);
        return complete ? new HighResFiles(pagePaths, cover, back) : null;
    }

    /// <summary>True for a file that is a Git LFS pointer (its objects were not pulled).</summary>
    public static bool IsLfsPointer(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] head = new byte[LfsPointerStart.Length];
        int read = stream.Read(head, 0, head.Length);
        return read == head.Length && System.Text.Encoding.ASCII.GetString(head) == LfsPointerStart;
    }

    /// <summary>The Git LFS object id of a file: the pointer's own <c>oid sha256:</c> when it is a pointer, else the sha256 of its bytes (which is what the oid is), so both forms of one file agree.</summary>
    public static string Oid(string path) =>
        TryOid(path) ?? throw new InvalidOperationException($"{path} is a Git LFS pointer with no oid line.");

    /// <summary>Like <see cref="Oid"/>, but null for a pointer with no <c>oid sha256:</c> line (truncated or hand-edited), so a caller can name the book instead of failing the whole run.</summary>
    public static string? TryOid(string path)
    {
        if (IsLfsPointer(path))
        {
            string? line = File.ReadLines(path).FirstOrDefault(l => l.StartsWith(OidPrefix, StringComparison.Ordinal));
            return line?[OidPrefix.Length..].Trim().ToLowerInvariant();
        }

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>The pointers of a book's high-res set that carry no oid line, as problems naming the book and the file.</summary>
    public static IEnumerable<string> BrokenPointerProblems(BookFolder book) =>
        book.Info.Kind == BookKind.Store && Find(book) is { } files
            ? files.All.Where(f => IsLfsPointer(f) && TryOid(f) is null).Select(f => $"{book.Stem}/{Directory}/{Path.GetFileName(f)} is a Git LFS pointer with no oid line (damaged; restore it from git)")
            : [];

    /// <summary>The problem when a book's high-res set is complete in git but not pulled (a pointer where a picture must be packed); null otherwise. Publishing must fail on it, never ship standard only.</summary>
    public static string? UnpulledProblem(BookFolder book) =>
        book.Info.Kind == BookKind.Store && Find(book) is { HasPointers: true }
            ? $"{book.Stem} has a complete high-res/ folder but its pictures are Git LFS pointers (run: git lfs pull --include \"content/dino/{book.Stem}/high-res/**\" --exclude \"\"); not built, so no book is published without its high-res pack"
            : null;

    /// <summary>The publish gate's picture check (<see cref="StoreValidation.ProblemsByBook"/>): problems of a complete, pulled set; none for no set or an unpulled one (<see cref="UnpulledProblem"/> names that). Never throws: an unreadable file is a problem of that book alone.</summary>
    public static IReadOnlyList<string> GateProblems(BookFolder book)
    {
        try
        {
            List<string> broken = BrokenPointerProblems(book).ToList();
            return broken.Count > 0 ? broken : Find(book) is { HasPointers: false } files ? PictureProblems(book.Stem, files) : [];
        }
        catch (IOException e)
        {
            return [$"{book.Stem}: the high-res pictures could not be read ({e.Message})"];
        }
    }

    /// <summary>Problems with a complete set: a picture that is not a JPEG, not 3:4 (the reader frames pages 3:4 and would crop it) or over 1.5 MB (not made by prepare-story-pictures.ps1 -HighRes). Each problem names the book and the file.</summary>
    public static IReadOnlyList<string> PictureProblems(string stem, HighResFiles files)
    {
        var problems = new List<string>();
        foreach (string path in files.Pages.Concat([files.Cover, files.BackCover]).OfType<string>())
        {
            if (JpegSize.Of(path) is not { } size)
            {
                problems.Add($"{stem}/high-res/{Path.GetFileName(path)} is not a readable JPEG");
            }
            else if (Math.Abs((double)size.Width / size.Height - PictureAspect) > AspectTolerance)
            {
                problems.Add($"{stem}/high-res/{Path.GetFileName(path)} is {size.Width}x{size.Height}, not 3:4");
            }
            else if (new FileInfo(path).Length > MaxPictureBytes)
            {
                problems.Add($"{stem}/high-res/{Path.GetFileName(path)} is {new FileInfo(path).Length} bytes, over the 1.5 MB limit: re-save it with prepare-story-pictures.ps1 -HighRes");
            }
        }

        return problems;
    }

    /// <summary>
    /// Writes the high-res package beside the standard one: the story's picture files in <paramref name="library"/> are overwritten with the print-size ones (same names, so the same
    /// story.json) and packed at <paramref name="version"/>. The library folder holds the high-res pictures afterwards, so call this last. Returns the package's size in bytes.
    /// </summary>
    public static async Task<long> WriteAsync(Story story, FileStoryLibrary library, HighResFiles files, string folder, int version)
    {
        for (int i = 0; i < files.Pages.Count; i++)
        {
            Overwrite(library, story, StarterBuilder.PictureName(i), files.Pages[i]);
        }

        if (files.Cover is not null && story.CoverFile is { } coverName)
        {
            Overwrite(library, story, coverName, files.Cover);
        }

        if (files.BackCover is not null && story.BackCover?.PictureFile is { } backName)
        {
            Overwrite(library, story, backName, files.BackCover);
        }

        string path = Path.Combine(folder, HdPackageFileName(version));
        await using (FileStream output = File.Create(path))
        {
            await StoryPackageWriter.WriteAsync(story, library, output, includeRecordings: false, version: version);
        }

        string standard = Path.Combine(folder, StoreBuilder.PackageFileName(version));
        if (SameTextAndNames(standard, path) is { } problem)
        {
            File.Delete(path);
            throw new InvalidOperationException($"{story.Title}: the high-res pack does not match the standard pack ({problem})");
        }

        return new FileInfo(path).Length;
    }

    private static void Overwrite(FileStoryLibrary library, Story story, string name, string source) =>
        File.Copy(source, library.TryResolveFile(story.Id, name)!, overwrite: true);

    /// <summary>Null when the two packages carry the same entries and the same manifest (version included) and story.json (text, cast, page order); else the first difference.</summary>
    public static string? SameTextAndNames(string standardPath, string hdPath)
    {
        using ZipArchive standard = ZipFile.OpenRead(standardPath);
        using ZipArchive hd = ZipFile.OpenRead(hdPath);
        string[] names = standard.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(hd.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal)))
        {
            return "the packages list different files";
        }

        foreach (string name in new[] { StoryPackageManifest.ManifestFileName, StoryPackageManifest.StoryFileName })
        {
            if (!BytesOf(standard, name).SequenceEqual(BytesOf(hd, name)))
            {
                return $"{name} differs";
            }
        }

        return null;
    }

    private static byte[] BytesOf(ZipArchive zip, string name)
    {
        using Stream stream = (zip.GetEntry(name) ?? throw new InvalidOperationException($"{name} is missing")).Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
