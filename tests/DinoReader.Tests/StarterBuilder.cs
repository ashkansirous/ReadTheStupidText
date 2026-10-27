using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// Builds the bundled starter <c>.dino</c> files (Slice 91) from <see cref="StarterTexts"/> and the "Good Enough" pictures (Slice 94),
/// casting voices the way the app does (segment, attribute, cast over the ten Supertonic styles) and merging the checked-in translations
/// (Slice 95). Run once with <c>DINO_BUILD_STARTERS=1</c> to rewrite <c>src/DinoReader.App/Resources/Raw/starters/</c>; the other tests
/// then check the shipped files. <c>DINO_DUMP_PIECES=1</c> writes the piece texts each starter needs translated.
/// </summary>
internal static class StarterBuilder
{
    public const string BuildSwitch = "DINO_BUILD_STARTERS";
    public const string DumpSwitch = "DINO_DUMP_PIECES";
    public const string NarratorVoice = "supertonic:M1";

    // "Good Enough" (Slice 94): the user's own picture book, built from its pictures and the proofread lines.json.
    public const string GoodEnoughId = "61bb25b8f132413c82a4208827c7563b";
    public const string GoodEnoughTitle = "Good Enough";
    public const string GoodEnoughFile = "good-enough.dino";
    public const int GoodEnoughColourIndex = 2;
    private const string LinesFile = "lines.json";
    private const string PiecesSuffix = ".pieces.json";
    internal const string StoryLanguage = "en";
    private const string PicturesDirectory = "pictures";
    private const string PictureExtension = ".jpg";
    private const string CoverPictureFile = "cover" + PictureExtension;

    public static readonly DateTimeOffset Created = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<VoiceOption> Voices =
        Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:F{i}", Gender.Female))
            .Concat(Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:M{i}", Gender.Male))).ToList();

    public static IReadOnlyList<(string Id, string Title, string Text, int ColourIndex, string FileName)> Starters { get; } =
    [
        (StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3, "three-little-pigs.dino"),
        (StarterTexts.GingerbreadId, "The Gingerbread Man", StarterTexts.Gingerbread, 1, "gingerbread-man.dino"),
        (StarterTexts.TortoiseId, "The Tortoise and the Hare", StarterTexts.Tortoise, 0, "tortoise-and-hare.dino"),
    ];

    /// <summary>The content version of each bundled starter, by package file name: written to the package manifest and to the store entry (Decision 130).
    /// Raise it whenever a starter's text or pictures change, so installed copies are refreshed (<see cref="StarterUpdate"/>).</summary>
    public static IReadOnlyDictionary<string, int> StarterVersions { get; } = new Dictionary<string, int>
    {
        ["three-little-pigs.dino"] = 10,
        [GoodEnoughFile] = 4,
        ["gingerbread-man.dino"] = 6,
        ["tortoise-and-hare.dino"] = 8,
    };

    /// <summary>The package file name of every shipped starter, the picture one first (shelf order).</summary>
    public static IReadOnlyList<string> AllFileNames { get; } = [GoodEnoughFile, .. Starters.Select(s => s.FileName)];

    public static string StartersFolder() =>
        Path.Combine(RepoRoot(), "src", "DinoReader.App", "Resources", "Raw", "starters");

    public static string TranslationsFolder() =>
        Path.Combine(RepoRoot(), "tests", "DinoReader.Tests", "Starters", "translations");

    /// <summary><c>Starters/&lt;stem&gt;/pictures</c>: the page pictures (<c>1.jpg</c> ... <c>N.jpg</c>) that turn a text starter into a picture + text book (Slice 98).</summary>
    public static string PicturesFolder(string stem) =>
        Path.Combine(RepoRoot(), "tests", "DinoReader.Tests", "Starters", stem, PicturesDirectory);

    /// <summary>The starter's page pictures in natural file-name order, or none when it has no pictures folder.</summary>
    public static IReadOnlyList<string> PictureFilesOf(string stem) =>
        PictureFilesIn(PicturesFolder(stem));

    /// <summary>The page pictures (<c>*.jpg</c>) of a <c>pictures</c> folder in natural file-name order, or none when the folder is missing.</summary>
    public static IReadOnlyList<string> PictureFilesIn(string folder)
    {
        return Directory.Exists(folder) ? PictureTextBook.SortNaturally(Directory.GetFiles(folder, "*" + PictureExtension)) : [];
    }

    /// <summary>The story with picture <i>n</i> on page <i>n</i> (a picture + text book). No pictures leaves it as it is; a different count fails,
    /// so a page break moved in <see cref="StarterTexts"/> cannot silently shift every picture. The files are copied as they are (JPEG), since
    /// the test project has no image encoder to re-encode them at quality 85.</summary>
    public static Story WithPictures(Story story, IReadOnlyList<string> pictures)
    {
        if (pictures.Count == 0)
        {
            return story;
        }

        if (pictures.Count != story.Pages.Count)
        {
            throw new InvalidOperationException($"\"{story.Title}\" has {story.Pages.Count} pages but {pictures.Count} pictures.");
        }

        IReadOnlyList<StoryPage> pages = story.Pages.Select((p, i) => p with { ImageFile = PictureName(i) }).ToList();
        return story with { Pages = pages };
    }

    public static string PictureName(int pageIndex) => $"page-{pageIndex + 1}{PictureExtension}";

    /// <summary><c>Starters/&lt;stem&gt;/cover.jpg</c> when the book has a picture cover, else null.</summary>
    public static string? CoverPictureOf(string stem) => ExistingFile(Path.Combine(StarterFolder(stem), CoverPictureFile));

    /// <summary>The path when the file exists, else null.</summary>
    public static string? ExistingFile(string path) => File.Exists(path) ? path : null;

    public static string StarterFolder(string stem) =>
        Path.Combine(RepoRoot(), "tests", "DinoReader.Tests", "Starters", stem);

    /// <summary>The story with <paramref name="coverSource"/> stored as its cover photo (<see cref="CoverStyle.Picture"/>); no cover leaves it as it is.
    /// The story must already be in <paramref name="library"/>.</summary>
    public static async Task<Story> WithPictureCoverAsync(Story story, string? coverSource, FileStoryLibrary library)
    {
        if (coverSource is null)
        {
            return story;
        }

        string cover = await library.StoreFileAsync(story.Id, Story.CoverFilePrefix, Story.CoverFileExtension,
            target => { File.Copy(coverSource, target, overwrite: true); return Task.CompletedTask; });
        return story with { CoverStyle = CoverStyle.Picture, CoverFile = cover };
    }

    public static void CopyPictures(Story story, IReadOnlyList<string> pictures, FileStoryLibrary library)
    {
        for (int i = 0; i < pictures.Count; i++)
        {
            File.Copy(pictures[i], library.TryResolveFile(story.Id, PictureName(i))!, overwrite: true);
        }
    }

    public static string GoodEnoughFolder() =>
        Path.Combine(RepoRoot(), "tests", "DinoReader.Tests", "Starters", "good-enough");

    public static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ReadTheStupidText.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    public static Story Build(string id, string title, string text, int colourIndex)
    {
        IReadOnlyList<StoryPage> pages = text.ReplaceLineEndings("\n").Split("\n---\n").Select(p => new StoryPage(p.Trim())).ToList();
        var bare = new Story(id, title, StoryLanguage, Created, pages, [], CoverStyle.Colour, colourIndex, NarratorVoice, TranslateToMyLanguage: true);
        return bare with { Cast = CastOf(bare) };
    }

    private static IReadOnlyList<CastMember> CastOf(Story story)
    {
        var rules = LanguageRules.English;
        AttributionResult attribution = new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, story.BreakMap()));
        VoiceCast cast = new VoiceCaster().Cast(attribution.Characters, Voices, NarratorVoice);
        IReadOnlyList<CastMember> members = StoryCast.ToMembers(
            attribution.Characters, cast, rules, animals: StarterAnimals.For(story.Id, attribution.Characters), authored: StarterVoices.For(story.Id, attribution.Characters));
        return StarterAnimals.Explicit(story.Id, story.Text, attribution.Characters, StarterAnimals.WithNotAnimals(story.Id, StarterVoices.WithGenders(story.Id, members)));
    }

    public static async Task WriteAllAsync()
    {
        Directory.CreateDirectory(StartersFolder());
        await InScratchAsync(async library =>
        {
            foreach (var (fileName, story) in await BuildAllAsync(library))
            {
                await WriteOneAsync(story, Path.Combine(StartersFolder(), fileName), library, StarterVersions[fileName]);
            }
        });
    }

    /// <summary>Every shipped starter as it is packed (translations merged, page pictures and picture cover copied into <paramref name="library"/>),
    /// the text starters first, then Good Enough. The store builds the same stories under the same ids (Decision 128).</summary>
    public static async Task<IReadOnlyList<(string FileName, Story Story)>> BuildAllAsync(FileStoryLibrary library)
    {
        var built = new List<(string FileName, Story Story)>();
        foreach (var (id, title, text, colour, fileName) in Starters)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            IReadOnlyList<string> pictures = PictureFilesOf(stem);
            Story story = WithTranslations(WithPictures(Build(id, title, text, colour), pictures), fileName);
            await library.AddAsync(story);
            CopyPictures(story, pictures, library);
            built.Add((fileName, await WithPictureCoverAsync(story, CoverPictureOf(stem), library)));
        }

        built.Add((GoodEnoughFile, WithTranslations(await BuildGoodEnoughAsync(library), GoodEnoughFile)));
        return built;
    }

    /// <summary>Writes <c>translations/&lt;stem&gt;.pieces.json</c> for every starter: the distinct piece texts, in story order, that need a translation.</summary>
    public static async Task DumpPiecesAsync()
    {
        Directory.CreateDirectory(TranslationsFolder());
        var stories = Starters.Select(s => (s.FileName, Story: Build(s.Id, s.Title, s.Text, s.ColourIndex))).ToList();
        await InScratchAsync(async library => stories.Add((GoodEnoughFile, await BuildGoodEnoughAsync(library))));
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var (fileName, story) in stories)
        {
            string path = Path.Combine(TranslationsFolder(), Path.GetFileNameWithoutExtension(fileName) + PiecesSuffix);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(StoryPieces.TranslatablePieceTexts(story), options));
        }
    }

    /// <summary>The story with every <c>translations/&lt;stem&gt;.&lt;lang&gt;.json</c> (source piece text to translation) merged into <see cref="Story.Translations"/>.</summary>
    public static Story WithTranslations(Story story, string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (string language in UiLanguage.Supported.Where(l => l != UiLanguage.Resolve(story.Language)))
        {
            string path = Path.Combine(TranslationsFolder(), $"{stem}.{language}.json");
            if (File.Exists(path))
            {
                tables[language] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            }
        }

        return story with { Translations = tables.Count == 0 ? null : tables };
    }

    /// <summary>The picture starter: pages from <c>lines.json</c> (lines joined by a newline, so the geometry rules apply; a page with only
    /// <c>text</c> is spoken with no boxes), the cover as a stored cover photo, and the cast. The pictures are copied into <paramref name="library"/>.</summary>
    public static async Task<Story> BuildGoodEnoughAsync(FileStoryLibrary library)
    {
        string folder = GoodEnoughFolder();
        using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, LinesFile)));
        IReadOnlyList<StoryPage> pages = doc.RootElement.GetProperty("pages").EnumerateArray().Select(PageOf).ToList();
        var bare = new Story(GoodEnoughId, GoodEnoughTitle, StoryLanguage, Created, pages, [], CoverStyle.Colour, GoodEnoughColourIndex, NarratorVoice, TranslateToMyLanguage: true);
        await library.AddAsync(bare);
        foreach (StoryPage page in pages)
        {
            File.Copy(Path.Combine(folder, page.ImageFile!), library.TryResolveFile(GoodEnoughId, page.ImageFile)!, overwrite: true);
        }

        string coverSource = Path.Combine(folder, doc.RootElement.GetProperty("cover").GetString()!);
        Story withCover = await WithPictureCoverAsync(bare, coverSource, library);
        return withCover with { Cast = CastOf(withCover) };
    }

    private static StoryPage PageOf(JsonElement page)
    {
        string image = page.GetProperty("image").GetString()!;
        if (!page.TryGetProperty("lines", out JsonElement lineElements) || lineElements.GetArrayLength() == 0)
        {
            return new StoryPage(page.GetProperty("text").GetString()!, image);
        }

        IReadOnlyList<PageLine> lines = lineElements.EnumerateArray().Select(LineOf).ToList();
        return new StoryPage(string.Join("\n", lines.Select(l => l.Text)), image, lines);
    }

    private static PageLine LineOf(JsonElement line) => new(
        line.GetProperty("text").GetString()!,
        new NormalizedRect(line.GetProperty("x").GetDouble(), line.GetProperty("y").GetDouble(), line.GetProperty("w").GetDouble(), line.GetProperty("h").GetDouble()));

    private static async Task InScratchAsync(Func<FileStoryLibrary, Task> work)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "dino-starters-" + Guid.NewGuid().ToString("N"));
        try
        {
            await work(new FileStoryLibrary(scratch));
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    private static async Task WriteOneAsync(Story story, string target, FileStoryLibrary library, int version)
    {
        await using FileStream output = File.Create(target);
        await StoryPackageWriter.WriteAsync(story, library, output, version: version);
    }
}
