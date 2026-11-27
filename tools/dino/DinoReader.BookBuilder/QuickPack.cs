using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>
/// Slice 463: the third package of a book, <c>story-v&lt;n&gt;.quick.dino</c>, for a slow first download. Same version, text and story.json as the standard package, with the small pictures of
/// <c>content/dino/&lt;stem&gt;/quick/</c> (448x600, made by <c>prepare-story-pictures.ps1 -Quick</c>). It exists only when that folder holds every page, the cover and the back cover the book has,
/// as real files: a Git LFS pointer in place of a picture counts as missing (the book then ships without a quick copy, never with a broken one). A quick set is part of the book's hash,
/// so a change to it raises the version like any picture change.
/// </summary>
internal static class QuickPack
{
    public const string Directory = "quick";
    public const string MetaKey = "quickBytes";

    public static string QuickPackageFileName(int version) => $"story-v{version}.quick.dino";

    public static string FolderOf(BookFolder book) => Path.Combine(book.Dir, Directory);

    /// <summary>The quick pictures when the folder holds every one the book needs as real files, else null.</summary>
    public static HighResFiles? Find(BookFolder book) => FindSet(book) is { HasPointers: false } files ? files : null;

    /// <summary>The complete quick set whether its files are real or Git LFS pointers (the hash names it by LFS oid either way, so a run with or without <c>git lfs pull</c> gets one hash); null when incomplete.</summary>
    public static HighResFiles? FindSet(BookFolder book)
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
        HighResFiles files = new(pagePaths, cover, back);
        return files.All.All(File.Exists) ? files : null;
    }

    /// <summary>
    /// Writes the quick package beside the standard one: the story's picture files in <paramref name="library"/> are overwritten with the quick ones (same names, so the same story.json) and packed at
    /// <paramref name="version"/>. The library holds the quick pictures afterwards, so call this last. Returns the package's size in bytes.
    /// </summary>
    public static async Task<long> WriteAsync(Story story, FileStoryLibrary library, HighResFiles files, string folder, int version)
    {
        if (files.Pages.Count != story.Pages.Count)
        {
            throw new InvalidOperationException($"{story.Title}: quick page count differs ({files.Pages.Count} quick pictures, {story.Pages.Count} story pages)");
        }

        for (int i = 0; i < files.Pages.Count; i++)
        {
            HighResPack.Overwrite(library, story, HighResPack.PageFileName(story, i), files.Pages[i]);
        }

        if (files.Cover is not null && story.CoverFile is { } coverName)
        {
            HighResPack.Overwrite(library, story, coverName, files.Cover);
        }

        if (files.BackCover is not null && story.BackCover?.PictureFile is { } backName)
        {
            HighResPack.Overwrite(library, story, backName, files.BackCover);
        }

        string path = Path.Combine(folder, QuickPackageFileName(version));
        await using (FileStream output = File.Create(path))
        {
            await StoryPackageWriter.WriteAsync(story, library, output, includeRecordings: false, version: version);
        }

        string standard = Path.Combine(folder, StoreBuilder.PackageFileName(version));
        if (HighResPack.SameTextAndNames(standard, path) is { } problem)
        {
            File.Delete(path);
            throw new InvalidOperationException($"{story.Title}: the quick pack does not match the standard pack ({problem})");
        }

        if (PictureBytesProblem(story, files, path) is { } picture)
        {
            File.Delete(path);
            throw new InvalidOperationException($"{story.Title}: {picture}");
        }

        return new FileInfo(path).Length;
    }

    /// <summary>Null when every page, cover and back-cover entry of the quick pack is byte-identical to its <c>quick/</c> file; else the first picture that is not. (Unlike the high-res check it does not ask that the bytes differ from the standard pack's: a source already as small as the quick size stays as it is.)</summary>
    public static string? PictureBytesProblem(Story story, HighResFiles files, string quickPath)
    {
        var expected = new List<(string Entry, string Source)>();
        for (int i = 0; i < files.Pages.Count; i++)
        {
            expected.Add((HighResPack.PageFileName(story, i), files.Pages[i]));
        }

        if (files.Cover is not null && story.CoverFile is { } coverName)
        {
            expected.Add((coverName, files.Cover));
        }

        if (files.BackCover is not null && story.BackCover?.PictureFile is { } backName)
        {
            expected.Add((backName, files.BackCover));
        }

        using ZipArchive quick = ZipFile.OpenRead(quickPath);
        foreach ((string entry, string source) in expected)
        {
            if (quick.GetEntry(entry) is null || !HighResPack.BytesOf(quick, entry).SequenceEqual(File.ReadAllBytes(source)))
            {
                return $"the quick pack's {entry} is not the bytes of {Directory}/{Path.GetFileName(source)}";
            }
        }

        return null;
    }
}
