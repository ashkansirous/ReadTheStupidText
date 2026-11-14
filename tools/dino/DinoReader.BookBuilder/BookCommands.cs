using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;
using Sirous.Reading.Domain.Reading;

namespace DinoReader.BookBuilder;

/// <summary>The commands of the book builder (Slice 157): <c>build</c>, <c>check</c>, <c>stamp</c> and <c>dump-pieces</c>.</summary>
internal static class BookCommands
{
    /// <summary>The exit code of <c>build --live</c> when it skipped an invalid store book but built the valid ones: the publish-store job still uploads those, then fails naming the bad book.</summary>
    public const int SkippedBooksExitCode = 2;

    /// <summary>Writes <c>artifacts/store/&lt;id&gt;/{meta.json, cover.jpg, story-v&lt;n&gt;.dino}</c> for every store book (or the named ones), the same for the starters, and rewrites each
    /// committed starter whose content changed. Returns the exit code.</summary>
    public static async Task<int> BuildAsync(IReadOnlyList<string> stems, string? livePath = null)
    {
        IReadOnlyList<BookFolder> chosen = Choose(stems);
        IReadOnlyDictionary<string, LiveEntry>? live = livePath is null ? null : StoreVersioning.Read(livePath);
        // publish-store runs no test suite: this is its gate (Slice 239). An invalid book is skipped and named, the valid ones still build.
        IReadOnlyDictionary<string, IReadOnlyList<string>> invalid = livePath is null ? new Dictionary<string, IReadOnlyList<string>>() : StoreValidation.ProblemsByBook(chosen.Where(b => b.Info.Kind == BookKind.Store), includeAudio: true);
        ReportInvalid(invalid);
        var failed = new Dictionary<string, IReadOnlyList<string>>();

        await StarterBuilder.InScratchAsync(async library =>
        {
            foreach (BookFolder book in chosen.Where(b => b.Info.Kind == BookKind.Store && !invalid.ContainsKey(b.Stem)))
            {
                StoreStorySpec spec = StoreVersioning.Versioned(book, live);
                if (await TryWriteStoreBookAsync(book, spec, library) is { } problem)
                {
                    failed[book.Stem] = [problem]; // Slice 375 B (review #3): one book that cannot build (say its high-res pack disagrees with the standard one) is named and skipped, the others still go
                    continue;
                }

                Console.WriteLine($"built {book.Stem} v{spec.Version}");
            }

            foreach (var (fileName, story) in await StarterBuilder.BuildAllAsync(library, name => chosen.Any(b => b.FileName == name)))
            {
                BookFolder book = chosen.Single(b => b.FileName == fileName);
                StoreStorySpec starterSpec = StoreBuilder.StarterEntries[fileName].ToSpec(story, book.Info.Version) with { LiveBytes = LiveBytesOf(book, live) }; // Slice 260: a starter at the live version keeps the live size, like a store book
                await StoreBuilder.WritePackageAsync(story, starterSpec, library, book.CoverThumbPath);
                bool rewritten = await WriteCommittedStarterAsync(story, fileName, library, book.Info.Version);
                Console.WriteLine($"built {book.Stem} v{book.Info.Version}{(rewritten ? " (rewrote the committed starter)" : string.Empty)}");
            }
        });
        ReportInvalid(failed);
        return invalid.Count == 0 && failed.Count == 0 ? 0 : SkippedBooksExitCode;
    }

    // The problem text when the store book could not be written (null when it was); its half-written packages are deleted so the upload never picks them up.
    private static async Task<string?> TryWriteStoreBookAsync(BookFolder book, StoreStorySpec spec, FileStoryLibrary library)
    {
        try
        {
            await StoreBuilder.WriteStoryAsync(spec, library);
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or JsonException or IOException or InvalidDataException)
        {
            string folder = Path.Combine(StoreBuilder.StoreFolder(), spec.Id);
            File.Delete(Path.Combine(folder, StoreBuilder.PackageFileName(spec.Version)));
            File.Delete(Path.Combine(folder, HighResPack.HdPackageFileName(spec.Version)));
            return $"{book.Stem} did not build: {e.Message}";
        }
    }

    private static long? LiveBytesOf(BookFolder book, IReadOnlyDictionary<string, LiveEntry>? live) =>
        live is not null && live.TryGetValue(book.Info.Id, out LiveEntry? entry) && entry.Version == book.Info.Version ? entry.Bytes : null;

    /// <summary>Slice 246 B2: packs the clips folder of one store book or starter language (see <see cref="AudioPacks"/>) beside the book's store folder and prints the version it was built as.</summary>
    public static int BuildAudio(string stem, string language, string? clipsFolder)
    {
        BookFolder book = Books.Folder(stem); // store books and the four bundled starters (Slice 258): both are served by the store route under their id
        AudioPackRecord record = AudioPacks.Build(book, language, clipsFolder ?? AudioPacks.ClipsFolderOf(book, language), StoreBuilder.StoreFolder());
        Console.WriteLine($"built {stem} audio {language} v{record.Version} ({record.Bytes} bytes)");
        return 0;
    }

    /// <summary>Fails (exit code 1) when a starter's content differs from its stored hash (raise its version with <c>stamp</c>), a committed starter differs from what its folder builds, or a store
    /// book does not build or lacks a title in a language. A store book needs no stamp (Slice 239).</summary>
    public static async Task<int> CheckAsync()
    {
        var problems = new List<string>();
        problems.AddRange(CheckHashes());
        problems.AddRange(CheckIds());
        problems.AddRange(CheckBackCovers());
        problems.AddRange(CheckStoreBooksBuild());
        problems.AddRange(CheckStoreTitles());
        problems.AddRange(await CheckHighResAsync());
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
            if (book.Info.Kind == BookKind.Store)
            {
                Console.WriteLine($"{book.Stem}: a store book needs no stamp (publish-store versions it from its content hash)");
                continue;
            }

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
        foreach (BookFolder book in Books.Starters)
        {
            if (BookHash.Of(book) != book.Info.ContentHash)
            {
                yield return $"raise the version of {book.Stem}: its content changed since v{book.Info.Version} (run: dotnet run --project tools/dino/DinoReader.BookBuilder -- stamp {book.Stem})";
            }
            else if (GitBaseline.VersionProblem(book.Stem, book.Info, GitBaseline.Of(book.Stem)) is { } problem)
            {
                yield return problem;
            }
        }
    }

    private static void ReportInvalid(IReadOnlyDictionary<string, IReadOnlyList<string>> invalid)
    {
        foreach ((string stem, IReadOnlyList<string> problems) in invalid)
        {
            Console.Error.WriteLine($"::error::store book {stem} is invalid and was skipped (not built, not uploaded): {string.Join("; ", problems)}");
        }
    }

    private static IEnumerable<string> CheckIds() => IdProblems();

    /// <summary>Slice 359: every book except Good Enough (its pictures are being redrawn, Slice 350 E) has a <c>back-cover.jpg</c>.</summary>
    private static IEnumerable<string> CheckBackCovers() =>
        Books.All.SelectMany(b => BackCoverProblems(b.Stem, b.BackCoverPath, b.Info.BackCover is not null));

    /// <summary>The problem with a book's back-cover picture: missing (Good Enough alone may lack it), not a JPEG, not 3:4 (the reader frames it 3:4), or present while book.json has no credit
    /// fields (<paramref name="hasCredits"/> false: the picture would never be packed or shown); null when it is fine.</summary>
    public static string? BackCoverProblem(string stem, string path, bool hasCredits = true) =>
        BackCoverProblems(stem, path, hasCredits) is { Count: > 0 } problems ? string.Join("; ", problems) : null;

    /// <summary>Every problem with a book's back cover, not only the first: lost credit fields (with or without a picture: the book would show no back cover), a missing, non-JPEG or mis-shaped picture.</summary>
    public static IReadOnlyList<string> BackCoverProblems(string stem, string path, bool hasCredits = true)
    {
        var problems = new List<string>();
        if (!hasCredits)
        {
            problems.Add($"{stem}'s book.json has no credit fields (storyBy, retold, illustration, month), so it shows no back cover and a back-cover.jpg would never be packed or shown");
        }

        if (!File.Exists(path))
        {
            if (stem != StarterBuilder.GoodEnoughStem)
            {
                problems.Add($"{stem} has no back-cover.jpg (every book needs a drawn back cover, Slice 359)");
            }

            return problems;
        }

        problems.AddRange(PictureProblems(stem, path));
        return problems;
    }

    private static IEnumerable<string> PictureProblems(string stem, string path)
    {
        byte[] head = new byte[JpegMagic.Length];
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Read(head, 0, head.Length) != head.Length || !head.AsSpan().SequenceEqual(JpegMagic))
            {
                return [$"{stem}/back-cover.jpg is not a JPEG"];
            }
        }

        return AspectProblem(stem, path) is { } problem ? [problem] : [];
    }

    private static string? AspectProblem(string stem, string path)
    {
        if (JpegSize.Of(path) is not { } size)
        {
            return $"{stem}/back-cover.jpg has no readable size";
        }

        double aspect = (double)size.Width / size.Height;
        return Math.Abs(aspect - BackCoverAspect) <= BackCoverAspectTolerance ? null : $"{stem}/back-cover.jpg is {size.Width}x{size.Height}, not 3:4 (the reader frames it 3:4 and would crop it)";
    }

    private const double BackCoverAspect = 0.75;
    private const double BackCoverAspectTolerance = 0.01;

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    internal static IEnumerable<string> IdProblems() =>
        Books.All.GroupBy(b => b.Info.Id).Where(g => g.Count() > 1).Select(g => $"the books {string.Join(", ", g.Select(b => b.Stem))} share the id {g.Key}");

    /// <summary>The problem with a store book's <c>titles.json</c>: missing, or not a non-blank title for exactly every other language; null when it is fine.</summary>
    public static string? TitlesProblem(string stem, IReadOnlyDictionary<string, string>? titles)
    {
        if (titles is null)
        {
            return $"{stem} has no titles.json (its title in every other language, so the app needs no row for it)";
        }

        string[] expected = UiLanguage.Supported.Where(code => code != UiLanguage.English).ToArray();
        string[] bad = expected.Where(code => !titles.TryGetValue(code, out string? title) || string.IsNullOrWhiteSpace(title)).Concat(titles.Keys.Except(expected)).ToArray();
        return bad.Length == 0 ? null : $"{stem}/titles.json needs a title for exactly the other languages (problem: {string.Join(",", bad)})";
    }

    private static IEnumerable<string> CheckStoreTitles() =>
        Books.StoreOnly.Select(b => TitlesProblem(b.Stem, b.Titles)).OfType<string>();

    /// <summary>Slice 375 B: every store book with a complete <c>high-res/</c> set has JPEG, 3:4 pictures and builds a high-res pack that carries the same text, audio list and version as its standard pack.</summary>
    private static async Task<IReadOnlyList<string>> CheckHighResAsync()
    {
        var problems = new List<string>();
        foreach (BookFolder book in Books.StoreOnly.Where(b => b.Text is not null))
        {
            if (HighResPack.Find(book) is not { HasPointers: false } files)
            {
                continue; // none, or not pulled (a unit-test checkout): publish-store fails such a book itself (StoreValidation)
            }

            problems.AddRange(HighResPack.PictureProblems(book.Stem, files));
            if (await PackPairProblemAsync(book) is { } problem)
            {
                problems.Add(problem);
            }
        }

        return problems;
    }

    private static async Task<string?> PackPairProblemAsync(BookFolder book)
    {
        string? previous = StoreBuilder.OutputFolder;
        string folder = Path.Combine(Path.GetTempPath(), "dino-check-hd-" + Guid.NewGuid().ToString("N"));
        StoreBuilder.OutputFolder = folder;
        try
        {
            await StarterBuilder.InScratchAsync(library => StoreBuilder.WriteStoryAsync(book.ToSpec(), library));
            string storeFolder = Path.Combine(folder, book.Info.Id);
            bool both = File.Exists(Path.Combine(storeFolder, StoreBuilder.PackageFileName(book.Info.Version))) && File.Exists(Path.Combine(storeFolder, HighResPack.HdPackageFileName(book.Info.Version)));
            return both ? null : $"{book.Stem} has a complete high-res/ folder but no high-res pack was built beside the standard one";
        }
        catch (Exception e) when (e is InvalidOperationException or JsonException or IOException)
        {
            return $"{book.Stem}: {e.Message}";
        }
        finally
        {
            StoreBuilder.OutputFolder = previous;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

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
                    problems.Add($"the committed starter {fileName} differs from content/dino/{book.Stem} (run: dotnet run --project tools/dino/DinoReader.BookBuilder -- build {book.Stem})");
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
