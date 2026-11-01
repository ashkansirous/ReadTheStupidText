using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.BookBuilder;

/// <summary>Whether a book ships inside the app (and also sits in the store, Decision 128) or only in the store.</summary>
[JsonConverter(typeof(BookKindConverter))]
internal enum BookKind
{
    Starter,
    Store,
}

/// <summary>Writes the kind as <c>starter</c> / <c>store</c>.</summary>
internal sealed class BookKindConverter : JsonStringEnumConverter<BookKind>
{
    public BookKindConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}

/// <summary>What a book's author decided for one character's voice (Slice 156, Decision 161): the suggested roster id, and the traits that stay true when the voice changes.</summary>
internal sealed record AuthoredCharacter(string Name, string Voice, Gender Gender, CharacterAge Age, CharacterBeing? Being, VoiceQuality[] Qualities);

/// <summary>
/// <c>content/dino/&lt;stem&gt;/book.json</c> (Slice 157, Decision 162): everything about a book that is not its text and pictures. <see cref="Characters"/> are the names the
/// book's speech tags must give a voice (a check, not shipped); <see cref="Cast"/> are the writer's voice choices (Slice 156), <see cref="Animals"/> the characters whose names do not
/// say their animal (Slice 125), <see cref="NotAnimals"/> the ones that are people although the text suggests an animal. <see cref="ContentHash"/> is the hash of the book's
/// content at <see cref="Version"/> (<see cref="BookHash"/>).
/// </summary>
internal sealed record BookInfo(
    BookKind Kind, string Id, string Title, int Version, int AgeMin, int AgeMax, string Author, string Credits, int Colour, string Added, int Order,
    string[]? Characters, AuthoredCharacter[]? Cast, Dictionary<string, AnimalKind>? Animals, string[]? NotAnimals, string ContentHash, bool? TextInPictures = null,
    Dictionary<string, string>? CreditsByLanguage = null, string? StoryBy = null, bool? Retold = null, string? Illustration = null)
{
    /// <summary>The book's silent back cover (Slice 237) from <c>storyBy</c>, <c>retold</c>, <c>illustration</c> and the month of <see cref="Added"/>; null when it cannot be made (<c>check</c> fails such a book).</summary>
    [JsonIgnore]
    public BackCover? BackCover => DinoReader.Core.BackCover.TryCreate(StoryBy, Retold == true, Illustration, Added);
}

/// <summary>One free story in the Dino store (Slice 102): its fixed package id, the folder name its files are stored under, the
/// credit line, the age range and the author the store card and filters use (Slice 112), the generated cover colour and the characters its speech tags must give a voice.</summary>
internal sealed record StoreStorySpec(string Id, string Title, string Stem, string Text, int ColourIndex, string Credits, int AgeMin, int AgeMax, string Author, IReadOnlyList<string> Characters, int Version, string Added,
    IReadOnlyDictionary<string, string>? CreditsByLanguage = null);

/// <summary>One book's folder under <c>content/dino</c>: its files, its <c>book.json</c> and its text.</summary>
internal sealed class BookFolder(string stem, string path, BookInfo info)
{
    public const string StoryFile = "story.md";
    public const string LinesFile = "lines.json";
    public const string BookFile = "book.json";
    public const string CoverFile = "cover.jpg";
    public const string CoverThumbFile = "cover-thumb.jpg";
    public const string PicturesDirectory = "pictures";
    public const string TranslationsDirectory = "translations";
    public const string PiecesFile = "pieces.json";

    public string Stem { get; } = stem;

    public string Dir { get; } = path;

    public BookInfo Info { get; } = info;

    public string FileName => Stem + ".dino";

    public string PicturesPath => Path.Combine(Dir, PicturesDirectory);

    public string TranslationsPath => Path.Combine(Dir, TranslationsDirectory);

    public string CoverPath => Path.Combine(Dir, CoverFile);

    public string CoverThumbPath => Path.Combine(Dir, CoverThumbFile);

    public string LinesPath => Path.Combine(Dir, LinesFile);

    /// <summary>The page text (pages separated by a line holding only "---"), or null for a picture book whose pages come from <c>lines.json</c> (Good Enough).</summary>
    public string? Text => File.Exists(Path.Combine(Dir, StoryFile)) ? File.ReadAllText(Path.Combine(Dir, StoryFile)).ReplaceLineEndings("\n").TrimEnd('\n') : null;

    public StoreStorySpec ToSpec() =>
        new(Info.Id, Info.Title, Stem, Text ?? string.Empty, Info.Colour, Info.Credits, Info.AgeMin, Info.AgeMax, Info.Author, Info.Characters ?? [], Info.Version, Info.Added, Info.CreditsByLanguage);
}

/// <summary>Every book under <c>content/dino</c> (Slice 157), read once: the shared loader of the builder and of the tests that validate the books.</summary>
internal static class Books
{
    public const string ContentFolder = "content/dino";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ReadOptions = new(JsonOptions) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    private static readonly string[] RequiredKeys = ["kind", "id", "title", "version", "ageMin", "ageMax", "author", "credits", "colour", "added", "order", "contentHash", "storyBy"];

    private const int MaxAge = 18;
    private const string AddedFormat = "yyyy-MM-dd";

    private static readonly Lazy<IReadOnlyList<BookFolder>> Loaded = new(Load);

    public static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ReadTheStupidText.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    public static string ContentRoot() => Path.Combine(RepoRoot(), "content", "dino");

    /// <summary>Every book: the starters in shelf order, then the store-only books in store order.</summary>
    public static IReadOnlyList<BookFolder> All => Loaded.Value;

    public static IReadOnlyList<BookFolder> Starters => All.Where(b => b.Info.Kind == BookKind.Starter).ToList();

    public static IReadOnlyList<BookFolder> StoreOnly => All.Where(b => b.Info.Kind == BookKind.Store).ToList();

    /// <summary>The store-only books as the store builder takes them (the starters' store entries are <see cref="StoreBuilder.StarterEntries"/>).</summary>
    public static IReadOnlyList<StoreStorySpec> Store => StoreOnly.Select(b => b.ToSpec()).ToList();

    public static BookFolder Folder(string stem) =>
        All.SingleOrDefault(b => b.Stem == stem) ?? throw new InvalidOperationException($"No book folder content/dino/{stem}.");

    public static string IdOf(string stem) => Folder(stem).Info.Id;

    public static StoreStorySpec SpecOf(string stem) => Folder(stem).ToSpec();

    public static BookInfo? InfoOfId(string storyId) => All.FirstOrDefault(b => b.Info.Id == storyId)?.Info;

    /// <summary>Reads a folder's <c>book.json</c>: an unknown key (a typo), a missing required key or an invalid value fails with a message naming the file and the problem.</summary>
    public static BookInfo ReadInfo(string folder)
    {
        string path = Path.Combine(folder, BookFolder.BookFile);
        string json = File.ReadAllText(path);
        try
        {
            string[] missing = RequiredKeys.Where(k => !HasKey(json, k)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"{path} is missing {string.Join(", ", missing)}.");
            }

            BookInfo info = JsonSerializer.Deserialize<BookInfo>(json, ReadOptions) ?? throw new InvalidOperationException($"{path} is empty.");
            string? problem = BookInfoProblem(info, folder);
            return problem is null ? info : throw new InvalidOperationException($"{path}: {problem}.");
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"{path} is not valid: {e.Message}", e);
        }
    }

    private static bool HasKey(string json, string key)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BookInfoProblem(BookInfo info, string folder)
    {
        return info switch
        {
            _ when !StoryIds.IsValid(info.Id ?? string.Empty) => $"id \"{info.Id}\" is not a valid story id",
            _ when string.IsNullOrWhiteSpace(info.Title) => "title is empty",
            _ when info.Version < 1 => "version must be 1 or more",
            _ when info.AgeMin < 0 || info.AgeMax > MaxAge || info.AgeMin > info.AgeMax => $"ages must satisfy 0 <= ageMin <= ageMax <= {MaxAge}",
            _ when info.Colour < 0 => "colour must be 0 or more",
            _ when info.Author is null || info.Credits is null || info.ContentHash is null => "author, credits and contentHash must be strings",
            _ when CreditsByLanguageProblem(info.CreditsByLanguage) is { } credits => credits,
            _ when !DateOnly.TryParseExact(info.Added, AddedFormat, out _) => $"added must be {AddedFormat}",
            _ when string.IsNullOrWhiteSpace(info.StoryBy) => "storyBy is empty (the back cover needs who wrote the story; \"traditional\" for a tale nobody owns)",
            _ when DinoReader.Core.BackCover.IsTraditional(info.StoryBy) && info.Retold != true => "a traditional tale must be retold: true",
            _ when info.Retold == true && string.Equals(info.StoryBy.Trim(), DinoReader.Core.BackCover.Maker, StringComparison.OrdinalIgnoreCase) => "a retold book must credit the original author in storyBy, not the maker",
            _ when !File.Exists(Path.Combine(folder, BookFolder.StoryFile)) && !File.Exists(Path.Combine(folder, BookFolder.LinesFile)) => "needs story.md or lines.json",
            _ => null,
        };
    }

    /// <summary>Slice 192: <c>creditsByLanguage</c> must hold a non-blank credit line for every shipped non-English language and nothing else.</summary>
    private static string? CreditsByLanguageProblem(Dictionary<string, string>? credits)
    {
        string[] expected = UiLanguage.Supported.Where(code => code != UiLanguage.English).ToArray();
        if (credits is null || credits.Count == 0)
        {
            return "creditsByLanguage is missing";
        }

        string[] absent = expected.Where(code => !credits.TryGetValue(code, out string? text) || string.IsNullOrWhiteSpace(text)).ToArray();
        string[] unknown = credits.Keys.Except(expected, StringComparer.Ordinal).ToArray();
        return absent.Length + unknown.Length == 0 ? null : $"creditsByLanguage needs exactly the {expected.Length} non-English languages (missing: {string.Join(",", absent)}; unknown: {string.Join(",", unknown)})";
    }

    public static void WriteInfo(string folder, BookInfo info) =>
        File.WriteAllText(Path.Combine(folder, BookFolder.BookFile), JsonSerializer.Serialize(info, JsonOptions).ReplaceLineEndings("\n") + "\n");

    private static IReadOnlyList<BookFolder> Load()
    {
        string[] folders = Directory.GetDirectories(ContentRoot());
        string[] withoutBook = folders.Where(d => !File.Exists(Path.Combine(d, BookFolder.BookFile))).ToArray();
        if (withoutBook.Length > 0)
        {
            throw new InvalidOperationException($"content/dino/{string.Join(", ", withoutBook.Select(Path.GetFileName))} has no {BookFolder.BookFile} (the name is case-sensitive on Linux).");
        }

        return folders.Select(d => new BookFolder(Path.GetFileName(d), d, ReadInfo(d)))
            .OrderBy(b => b.Info.Kind).ThenBy(b => b.Info.Order).ToList();
    }
}
