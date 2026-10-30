using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>
/// Builds the bundled starter <c>.dino</c> files (Slice 91) from their <c>content/dino/&lt;stem&gt;</c> folders (Slice 157) and the "Good Enough" pictures (Slice 94),
/// casting voices the way the app does (segment, attribute, cast over the ten Supertonic styles) and merging the checked-in translations
/// (Slice 95). <c>build</c> rewrites <c>src/DinoReader.App/Resources/Raw/starters/</c> when a file's content changed; the tests then check the shipped files.
/// <c>dump-pieces</c> writes the piece texts each book needs translated.
/// </summary>
internal static class StarterBuilder
{
    public const string NarratorVoice = "supertonic:M1";

    // "Good Enough" (Slice 94): the user's own picture book, built from its pictures and the proofread lines.json.
    public const string GoodEnoughStem = "good-enough";
    public const string GoodEnoughFile = GoodEnoughStem + ".dino";
    private const int ContentNameLength = 12;
    internal const string StoryLanguage = "en";
    public const string PictureExtension = ".jpg";

    public static readonly DateTimeOffset Created = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<VoiceOption> Voices =
        Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:F{i}", Gender.Female))
            .Concat(Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:M{i}", Gender.Male))).ToList();

    public static string GoodEnoughId => Books.IdOf(GoodEnoughStem);

    public static string GoodEnoughTitle => Books.Folder(GoodEnoughStem).Info.Title;

    /// <summary>The text starters (the ones built from a <c>story.md</c>, not Good Enough), in shelf order.</summary>
    public static IReadOnlyList<(string Id, string Title, string Text, int ColourIndex, string FileName)> Starters =>
        Books.Starters.Where(b => b.Stem != GoodEnoughStem).Select(b => (b.Info.Id, b.Info.Title, b.Text!, b.Info.Colour, b.FileName)).ToList();

    /// <summary>The content version of each bundled starter, by package file name: written to the package manifest and to the store entry (Decision 130).
    /// It lives in the starter's <c>book.json</c>; <c>BookBuilder stamp</c> raises it when the starter's text or pictures change, so installed copies are refreshed (<see cref="StarterUpdate"/>).</summary>
    public static IReadOnlyDictionary<string, int> StarterVersions => Books.Starters.ToDictionary(b => b.FileName, b => b.Info.Version);

    /// <summary>The package file name of every shipped starter, the picture one first (shelf order).</summary>
    public static IReadOnlyList<string> AllFileNames => Books.Starters.Select(b => b.FileName).ToList();

    public static string StartersFolder() =>
        Path.Combine(RepoRoot(), "src", "DinoReader.App", "Resources", "Raw", "starters");

    /// <summary><c>content/dino/&lt;stem&gt;/translations</c>: a book's checked-in translations (<c>&lt;lang&gt;.json</c>, <c>&lt;lang&gt;.speak.json</c>, <c>pieces.json</c>).</summary>
    public static string TranslationsFolder(string stem) => Books.Folder(stem).TranslationsPath;

    /// <summary><c>content/dino/&lt;stem&gt;/pictures</c>: the page pictures (<c>1.jpg</c> ... <c>N.jpg</c>) that turn a text book into a picture + text book (Slice 98).</summary>
    public static string PicturesFolder(string stem) => Books.Folder(stem).PicturesPath;

    /// <summary>The starter's page pictures in natural file-name order, or none when it has no pictures folder.</summary>
    public static IReadOnlyList<string> PictureFilesOf(string stem) =>
        PictureFilesIn(PicturesFolder(stem));

    /// <summary>The page pictures (<c>*.jpg</c>) of a <c>pictures</c> folder in natural file-name order, or none when the folder is missing.</summary>
    public static IReadOnlyList<string> PictureFilesIn(string folder)
    {
        return Directory.Exists(folder) ? PictureTextBook.SortNaturally(Directory.GetFiles(folder).Where(f => f.EndsWith(PictureExtension, StringComparison.OrdinalIgnoreCase)).ToArray()) : [];
    }

    /// <summary>The story with picture <i>n</i> on page <i>n</i> (a picture + text book). No pictures leaves it as it is; a different count fails,
    /// so a page break moved in a <c>story.md</c> cannot silently shift every picture. The files are copied as they are (JPEG), since
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

    /// <summary><c>content/dino/&lt;stem&gt;/cover.jpg</c> when the book has a picture cover, else null.</summary>
    public static string? CoverPictureOf(string stem) => ExistingFile(Books.Folder(stem).CoverPath);

    /// <summary>The path when the file exists, else null.</summary>
    public static string? ExistingFile(string path) => File.Exists(path) ? path : null;

    /// <summary>The story with <paramref name="coverSource"/> stored as its cover photo (<see cref="CoverStyle.Picture"/>); no cover leaves it as it is.
    /// The story must already be in <paramref name="library"/>.</summary>
    public static async Task<Story> WithPictureCoverAsync(Story story, string? coverSource, FileStoryLibrary library)
    {
        if (coverSource is null)
        {
            return story;
        }

        string stored = await library.StoreFileAsync(story.Id, Story.CoverFilePrefix, Story.CoverFileExtension,
            target => { File.Copy(coverSource, target, overwrite: true); return Task.CompletedTask; });
        return story with { CoverStyle = CoverStyle.Picture, CoverFile = RenameToContentName(story.Id, stored, library) };
    }

    // The library names a cover at random; a shipped package names it by its bytes, so rebuilding unchanged pictures changes nothing in story.json.
    private static string RenameToContentName(string storyId, string stored, FileStoryLibrary library)
    {
        string path = library.TryResolveFile(storyId, stored)!;
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))[..ContentNameLength].ToLowerInvariant();
        string name = $"{Story.CoverFilePrefix}-{hash}{Story.CoverFileExtension}";
        File.Move(path, library.TryResolveFile(storyId, name)!, overwrite: true);
        return name;
    }

    public static void CopyPictures(Story story, IReadOnlyList<string> pictures, FileStoryLibrary library)
    {
        for (int i = 0; i < pictures.Count; i++)
        {
            File.Copy(pictures[i], library.TryResolveFile(story.Id, PictureName(i))!, overwrite: true);
        }
    }

    public static string RepoRoot() => Books.RepoRoot();

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
        IReadOnlyList<CastMember> withGenders = StarterVoices.WithGenders(story.Id, members);
        IReadOnlyList<CastMember> withAnimals = StarterAnimals.WithNotAnimals(story.Id, withGenders);
        return StarterAnimals.Explicit(story.Id, story.Text, attribution.Characters, BookVoices(withAnimals, rules));
    }

    /// <summary>Gives each member whose book chose its voice (Slice 156) the engine voice and flavour that choice resolves to in English, in place of the plain caster's, so an app
    /// build that ignores <see cref="CastMember.SuggestedVoice"/> plays the chosen voice too.</summary>
    internal static IReadOnlyList<CastMember> BookVoices(IReadOnlyList<CastMember> members, LanguageRules rules)
    {
        if (!members.Any(m => m.HasAuthoredVoice))
        {
            return members;
        }

        var characters = members.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, StoryLanguage, members);
        IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(StoryLanguage);
        IReadOnlyDictionary<string, PinnedCharacter> pinned = BookPins(members);
        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(speakers, NarratorVoice), CharacterRoster.For(StoryLanguage), NarratorVoice, c => FlavourGuess.Guess(c.Name, rules), pinned,
            animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, StoryLanguage) : null);
        return members.Select(m => m.HasAuthoredVoice && cast.VoiceFor(m.CharacterId) is { } voice ? m with { VoiceId = voice, Flavour = cast.Flavours is not null && cast.Flavours.TryGetValue(m.CharacterId, out VoiceFlavour flavour) ? flavour : m.Flavour } : m).ToList();
    }

    // What the build pins before casting: every member the book decided something for, with its voice left for the roster to resolve; every other member keeps the voice
    // the plain caster gave it (held as a pick), so a book-chosen voice never lands on a sound a plain member already has.
    private static IReadOnlyDictionary<string, PinnedCharacter> BookPins(IReadOnlyList<CastMember> members)
    {
        var pins = StoryCast.PinnedCharacters(members.Where(m => m.Edited || m.HasAnimalSetting || m.HasAuthoredVoice).Select(m => m with { VoiceId = string.Empty }).ToList()).ToDictionary(p => p.Key, p => p.Value);
        foreach (CastMember plain in members.Where(m => !pins.ContainsKey(m.CharacterId)))
        {
            pins[plain.CharacterId] = new PinnedCharacter(plain.VoiceId, plain.Flavour, true, null, null);
        }

        return pins;
    }

    /// <summary>Every shipped starter as it is packed (translations merged, page pictures and picture cover copied into <paramref name="library"/>),
    /// the text starters first, then Good Enough. The store builds the same stories under the same ids (Decision 128).</summary>
    public static async Task<IReadOnlyList<(string FileName, Story Story)>> BuildAllAsync(FileStoryLibrary library, Func<string, bool>? include = null)
    {
        var built = new List<(string FileName, Story Story)>();
        foreach (var (id, title, text, colour, fileName) in Starters.Where(s => include?.Invoke(s.FileName) ?? true))
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            IReadOnlyList<string> pictures = PictureFilesOf(stem);
            Story story = WithTranslations(WithPictures(Build(id, title, text, colour), pictures), stem);
            await library.AddAsync(story);
            CopyPictures(story, pictures, library);
            built.Add((fileName, await WithPictureCoverAsync(story, CoverPictureOf(stem), library)));
        }

        if (include?.Invoke(GoodEnoughFile) ?? true)
        {
            built.Add((GoodEnoughFile, WithTranslations(await BuildGoodEnoughAsync(library), GoodEnoughStem)));
        }

        return built;
    }

    /// <summary>The story with every <c>content/dino/&lt;stem&gt;/translations/&lt;lang&gt;.json</c> (source piece text to translation) merged into <see cref="Story.Translations"/>,
    /// and the book's <c>textInPictures</c> flag (Slice 196) set: every built package passes through here, so the flag is applied in one place.</summary>
    public static Story WithTranslations(Story story, string stem)
    {
        string folder = TranslationsFolder(stem);
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (string language in UiLanguage.Supported.Where(l => l != UiLanguage.Resolve(story.Language)))
        {
            string path = Path.Combine(folder, $"{language}.json");
            if (File.Exists(path))
            {
                tables[language] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            }

            string speakPath = Path.Combine(folder, $"{language}.speak.json");
            if (File.Exists(speakPath))
            {
                tables[BundledTranslations.SpeakKey(language)] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(speakPath)) ?? [];
            }
        }

        return story with { Translations = tables.Count == 0 ? null : tables, TextInPictures = Books.Folder(stem).Info.TextInPictures == true };
    }

    /// <summary>The picture starter: pages from <c>lines.json</c> (lines joined by a newline, so the geometry rules apply; a page with only
    /// <c>text</c> is spoken with no boxes), the cover as a stored cover photo, and the cast. The pictures are copied into <paramref name="library"/>.</summary>
    public static async Task<Story> BuildGoodEnoughAsync(FileStoryLibrary library)
    {
        BookFolder book = Books.Folder(GoodEnoughStem);
        string folder = book.Dir;
        using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(book.LinesPath));
        IReadOnlyList<StoryPage> pages = doc.RootElement.GetProperty("pages").EnumerateArray().Select(PageOf).ToList();
        var bare = new Story(GoodEnoughId, GoodEnoughTitle, StoryLanguage, Created, pages, [], CoverStyle.Colour, book.Info.Colour, NarratorVoice, TranslateToMyLanguage: true);
        await library.AddAsync(bare);
        foreach (StoryPage page in pages)
        {
            File.Copy(Path.Combine(book.PicturesPath, page.ImageFile!), library.TryResolveFile(GoodEnoughId, page.ImageFile)!, overwrite: true);
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

    internal static async Task InScratchAsync(Func<FileStoryLibrary, Task> work)
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

    internal static async Task WriteOneAsync(Story story, string target, FileStoryLibrary library, int version)
    {
        await using FileStream output = File.Create(target);
        await StoryPackageWriter.WriteAsync(story, library, output, version: version);
    }
}
