using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>The commands of the book builder (Slice 157): <c>build</c>, <c>check</c>, <c>stamp</c> and <c>dump-pieces</c>.</summary>
internal static class BookCommands
{
    /// <summary>Writes <c>artifacts/store/&lt;id&gt;/{meta.json, cover.jpg, story-v&lt;n&gt;.dino}</c> for every store book (or the named ones), the same for the starters, and rewrites each
    /// committed starter whose content changed. Returns the exit code.</summary>
    public static async Task<int> BuildAsync(IReadOnlyList<string> stems)
    {
        IReadOnlyList<BookFolder> chosen = Choose(stems);
        await StarterBuilder.InScratchAsync(async library =>
        {
            foreach (BookFolder book in chosen.Where(b => b.Info.Kind == BookKind.Store))
            {
                await StoreBuilder.WriteStoryAsync(book.ToSpec(), library);
                Console.WriteLine($"built {book.Stem} v{book.Info.Version}");
            }

            foreach (var (fileName, story) in await StarterBuilder.BuildAllAsync(library, name => chosen.Any(b => b.FileName == name)))
            {
                BookFolder book = chosen.Single(b => b.FileName == fileName);
                await StoreBuilder.WritePackageAsync(story, StoreBuilder.StarterEntries[fileName].ToSpec(story, book.Info.Version), library, book.CoverThumbPath);
                bool rewritten = await WriteCommittedStarterAsync(story, fileName, library, book.Info.Version);
                Console.WriteLine($"built {book.Stem} v{book.Info.Version}{(rewritten ? " (rewrote the committed starter)" : string.Empty)}");
            }
        });
        return 0;
    }

    /// <summary>Fails (exit code 1) when a book's content differs from its stored hash (raise its version with <c>stamp</c>), or a committed starter differs from what its folder builds.</summary>
    public static async Task<int> CheckAsync()
    {
        var problems = new List<string>();
        problems.AddRange(CheckHashes());
        problems.AddRange(CheckIds());
        problems.AddRange(CheckStoreBooksBuild());
        problems.AddRange(await CheckStartersAsync());
        problems.ForEach(p => Console.Error.WriteLine(p));
        Console.WriteLine(problems.Count == 0 ? $"All {Books.All.Count} books are current." : $"{problems.Count} problem(s).");
        return problems.Count == 0 ? 0 : 1;
    }

    /// <summary>Raises the version of each named book whose content changed and stores the new hash. A book with no stored hash yet (new) keeps its version. <paramref name="keepVersion"/> only refreshes
    /// the hash (one-off seeding); <c>check</c> still fails a content change that did not raise the version above <c>origin/main</c>'s.</summary>
    public static int Stamp(IReadOnlyList<string> stems, bool keepVersion)
    {
        foreach (BookFolder book in Choose(stems))
        {
            string hash = BookHash.Of(book);
            if (hash == book.Info.ContentHash && !keepVersion)
            {
                Console.WriteLine($"{book.Stem}: unchanged, still v{book.Info.Version}");
                continue;
            }

            bool firstStamp = string.IsNullOrEmpty(book.Info.ContentHash);
            int version = keepVersion || firstStamp ? book.Info.Version : book.Info.Version + 1;
            Books.WriteInfo(book.Dir, book.Info with { Version = version, ContentHash = hash });
            Console.WriteLine($"{book.Stem}: v{version}");
        }

        return 0;
    }

    /// <summary>Writes <c>translations/pieces.json</c> for every book (or the named ones): the distinct piece texts, in story order, that need a translation.</summary>
    public static async Task<int> DumpPiecesAsync(IReadOnlyList<string> stems)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        await StarterBuilder.InScratchAsync(async library =>
        {
            foreach (BookFolder book in Choose(stems))
            {
                Story story = await BareStoryAsync(book, library);
                Directory.CreateDirectory(book.TranslationsPath);
                await File.WriteAllTextAsync(Path.Combine(book.TranslationsPath, BookFolder.PiecesFile), JsonSerializer.Serialize(StoryPieces.TranslatablePieceTexts(story), options));
            }
        });
        return 0;
    }

    private static async Task<Story> BareStoryAsync(BookFolder book, FileStoryLibrary library) =>
        book.Stem == StarterBuilder.GoodEnoughStem
            ? await StarterBuilder.BuildGoodEnoughAsync(library)
            : StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour);

    private static IReadOnlyList<BookFolder> Choose(IReadOnlyList<string> stems) =>
        stems.Count == 0 ? Books.All : stems.Select(Books.Folder).ToList();

    private static IEnumerable<string> CheckHashes()
    {
        foreach (BookFolder book in Books.All)
        {
            if (BookHash.Of(book) != book.Info.ContentHash)
            {
                yield return $"raise the version of {book.Stem}: its content changed since v{book.Info.Version} (run: dotnet run --project tools/DinoReader.BookBuilder -- stamp {book.Stem})";
            }
            else if (GitBaseline.VersionProblem(book.Stem, book.Info, GitBaseline.Of(book.Stem)) is { } problem)
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> CheckIds() =>
        Books.All.GroupBy(b => b.Info.Id).Where(g => g.Count() > 1).Select(g => $"the books {string.Join(", ", g.Select(b => b.Stem))} share the id {g.Key}");

    private static IEnumerable<string> CheckStoreBooksBuild()
    {
        foreach (BookFolder book in Books.StoreOnly.Where(b => b.Text is not null))
        {
            string? problem = TryBuild(() => StarterBuilder.WithTranslations(
                StarterBuilder.WithPictures(StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour), StarterBuilder.PictureFilesOf(book.Stem)), book.Stem));
            if (problem is not null)
            {
                yield return $"{book.Stem} does not build: {problem}";
            }
        }
    }

    private static string? TryBuild(Action build)
    {
        try
        {
            build();
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or JsonException or IOException)
        {
            return e.Message;
        }
    }

    private static async Task<IReadOnlyList<string>> CheckStartersAsync()
    {
        var problems = new List<string>();
        await StarterBuilder.InScratchAsync(async library =>
        {
            IReadOnlyList<(string FileName, Story Story)> stories;
            try
            {
                stories = await StarterBuilder.BuildAllAsync(library);
            }
            catch (Exception e) when (e is InvalidOperationException or JsonException or IOException)
            {
                problems.Add($"the starters do not build: {e.Message}");
                return;
            }

            foreach (var (fileName, story) in stories)
            {
                BookFolder book = Books.Starters.Single(b => b.FileName == fileName);
                string built = await WriteTempPackageAsync(story, library, book.Info.Version);
                string committed = Path.Combine(StarterBuilder.StartersFolder(), fileName);
                if (!File.Exists(committed) || !PackageContent.Same(built, committed))
                {
                    problems.Add($"the committed starter {fileName} differs from content/dino/{book.Stem} (run: dotnet run --project tools/DinoReader.BookBuilder -- build {book.Stem})");
                }

                File.Delete(built);
            }
        });
        return problems;
    }

    private static async Task<string> WriteTempPackageAsync(Story story, FileStoryLibrary library, int version)
    {
        string path = Path.Combine(Path.GetTempPath(), "dino-check-" + Guid.NewGuid().ToString("N") + ".dino");
        await StarterBuilder.WriteOneAsync(story, path, library, version);
        return path;
    }

    private static async Task<bool> WriteCommittedStarterAsync(Story story, string fileName, FileStoryLibrary library, int version)
    {
        Directory.CreateDirectory(StarterBuilder.StartersFolder());
        string target = Path.Combine(StarterBuilder.StartersFolder(), fileName);
        string built = await WriteTempPackageAsync(story, library, version);
        try
        {
            if (File.Exists(target) && PackageContent.Same(built, target))
            {
                return false;
            }

            File.Copy(built, target, overwrite: true);
            return true;
        }
        finally
        {
            File.Delete(built);
        }
    }
}

/// <summary>Compares two <c>.dino</c> packages by their entries (name and bytes), not by the zip's timestamps, so a rebuild of unchanged content counts as the same.</summary>
internal static class PackageContent
{
    public static bool Same(string a, string b)
    {
        Dictionary<string, string> first = Entries(a);
        Dictionary<string, string> second = Entries(b);
        return first.Count == second.Count && first.All(e => second.TryGetValue(e.Key, out string? other) && other == e.Value);
    }

    private static Dictionary<string, string> Entries(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);
        return zip.Entries.ToDictionary(e => e.FullName, e => Hash(e));
    }

    private static string Hash(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
