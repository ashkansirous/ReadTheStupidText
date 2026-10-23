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
    private const int GoodEnoughColour = 2;
    private const string LinesFile = "lines.json";
    private const string PiecesSuffix = ".pieces.json";
    private const string StoryLanguage = "en";

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

    /// <summary>The package file name of every shipped starter, the picture one first (shelf order).</summary>
    public static IReadOnlyList<string> AllFileNames { get; } = [GoodEnoughFile, .. Starters.Select(s => s.FileName)];

    public static string StartersFolder() =>
        Path.Combine(RepoRoot(), "src", "DinoReader.App", "Resources", "Raw", "starters");

    public static string TranslationsFolder() =>
        Path.Combine(RepoRoot(), "tests", "DinoReader.Tests", "Starters", "translations");

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
        return StoryCast.ToMembers(attribution.Characters, cast);
    }

    public static async Task WriteAllAsync()
    {
        Directory.CreateDirectory(StartersFolder());
        await InScratchAsync(async library =>
        {
            foreach (var (id, title, text, colour, fileName) in Starters)
            {
                Story story = WithTranslations(Build(id, title, text, colour), fileName);
                await library.AddAsync(story);
                await WriteOneAsync(story, Path.Combine(StartersFolder(), fileName), library);
            }

            Story goodEnough = WithTranslations(await BuildGoodEnoughAsync(library), GoodEnoughFile);
            await WriteOneAsync(goodEnough, Path.Combine(StartersFolder(), GoodEnoughFile), library);
        });
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
        var bare = new Story(GoodEnoughId, GoodEnoughTitle, StoryLanguage, Created, pages, [], CoverStyle.Colour, GoodEnoughColour, NarratorVoice, TranslateToMyLanguage: true);
        await library.AddAsync(bare);
        foreach (StoryPage page in pages)
        {
            File.Copy(Path.Combine(folder, page.ImageFile!), library.TryResolveFile(GoodEnoughId, page.ImageFile)!, overwrite: true);
        }

        string coverSource = Path.Combine(folder, doc.RootElement.GetProperty("cover").GetString()!);
        string cover = await library.StoreFileAsync(GoodEnoughId, Story.CoverFilePrefix, Story.CoverFileExtension,
            target => { File.Copy(coverSource, target, overwrite: true); return Task.CompletedTask; });
        Story withCover = bare with { CoverStyle = CoverStyle.Picture, CoverFile = cover };
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

    private static async Task WriteOneAsync(Story story, string target, FileStoryLibrary library)
    {
        await using FileStream output = File.Create(target);
        await StoryPackageWriter.WriteAsync(story, library, output);
    }
}
