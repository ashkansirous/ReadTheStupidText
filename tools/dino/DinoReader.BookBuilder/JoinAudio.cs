using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>What <see cref="JoinAudio.Rewrite"/> did to one manifest: its new JSON (null when it could not be made), the entries it changed and every piece or entry it could not account for.</summary>
internal sealed record JoinRewrite(string? Json, int Rewritten, IReadOnlyList<string> Problems);

/// <summary>
/// <c>join-audio [&lt;stem&gt;...]</c> (Slice 280): rewrites the Persian and Arabic <c>audio/&lt;lang&gt;/manifest.json</c> of each book to the speaker-first join the app builds as it translates (<see cref="SpeakerFirst.Join"/>), from the clips
/// there already are: no translation, no recording. Slice 254 gave the pieces this order but looked a tag up by the text with its colon, so the manifests held a clip made for «پیم گفت:». The colon is display
/// only now, so a tag is looked up by the text it was translated as: its entry takes that text's hash and text and keeps the clip file it has (the same words). Never a file name is changed: no rename, no adopting
/// another file. Every piece the app reads must end up with an entry that has a clip, or the command fails and writes nothing for that manifest. It does not touch the clips; run <c>build-audio</c> afterwards to
/// raise each pack's version.
/// </summary>
internal static class JoinAudio
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true, IndentSize = 1, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static int Run(IReadOnlyList<string> stems)
    {
        var problems = new List<string>();
        foreach (BookFolder book in stems.Count == 0 ? Books.All : stems.Select(Books.Folder))
        {
            Story story = SentenceDump.StoryOf(book.Stem);
            foreach (string language in UiLanguage.Supported.Where(SpeakerFirst.Applies).Where(l => UiLanguage.Resolve(l) != UiLanguage.Resolve(story.Language)))
            {
                problems.AddRange(RewriteBook(book, story, language));
            }
        }

        problems.ForEach(Console.Error.WriteLine);
        return problems.Count == 0 ? 0 : 1;
    }

    private static IEnumerable<string> RewriteBook(BookFolder book, Story story, string language)
    {
        string folder = Path.Combine(book.Dir, "audio", language);
        string manifest = Path.Combine(folder, PremadeAudio.ManifestFile);
        if (!File.Exists(manifest) || AudioPacks.IsLfsPointer(manifest))
        {
            return [$"{book.Stem} {language}: no manifest to rewrite (missing, or a Git LFS pointer: git lfs pull --include \"content/dino/{book.Stem}/audio/{language}/**\" --exclude \"\")"];
        }

        JoinRewrite result = Rewrite(File.ReadAllText(manifest), SentenceDump.Build(book.Stem, story, language).Sentences);
        if (result.Json is not null)
        {
            File.WriteAllText(manifest, result.Json);
            Console.WriteLine($"{book.Stem} {language}: {result.Rewritten} entries rewritten");
        }

        return result.Problems.Select(p => $"{book.Stem} {language}: {p}");
    }

    /// <summary>
    /// The manifest <paramref name="json"/> lined up with the sentences the app reads now (<paramref name="dump"/>). An entry that already hashes its piece's text stays; one that hashes the piece's text with the display colon
    /// (<see cref="SpeakerFirst.Colon"/>) gets the text as it is (hash and text only; every entry keeps the clip file it has, and one with no clip is a problem); any other piece has no clip and is a problem,
    /// as is an entry no piece uses. With a problem no JSON is returned.
    /// </summary>
    internal static JoinRewrite Rewrite(string json, IReadOnlyList<SpokenSentence> dump)
    {
        JsonNode root = JsonNode.Parse(json) ?? throw new InvalidOperationException("The manifest is empty.");
        Dictionary<int, JsonObject> byIndex = EntriesByIndex(root);
        var problems = new List<string>();
        int rewritten = 0;
        foreach (SpokenSentence piece in dump)
        {
            if (!byIndex.TryGetValue(piece.Index, out JsonObject? entry))
            {
                problems.Add($"position {piece.Index} \"{piece.Text}\" has no entry");
                continue;
            }

            if (!HasClip(entry))
            {
                problems.Add($"position {piece.Index} \"{piece.Text}\" has an entry with no clip");
                continue;
            }

            switch (Match(entry, piece.Text))
            {
                case EntryMatch.Same:
                    break;
                case EntryMatch.WithColon:
                    Retarget(entry, piece.Text);
                    rewritten++;
                    break;
                default:
                    problems.Add($"position {piece.Index} \"{piece.Text}\" has no clip: the entry there is for \"{entry["text"]}\"");
                    break;
            }
        }

        int unused = byIndex.Keys.Count(index => dump.All(p => p.Index != index));
        if (unused > 0)
        {
            problems.Add($"{unused} entr{(unused == 1 ? "y" : "ies")} of the manifest belong to no piece the app reads");
        }

        return new JoinRewrite(problems.Count == 0 ? Write(root) : null, rewritten, problems);
    }

    private enum EntryMatch { Same, WithColon, Other }

    private static EntryMatch Match(JsonObject entry, string text)
    {
        string hash = (string?)entry["hash"] ?? string.Empty;
        if (hash == PremadeAudio.HashOf(text))
        {
            return EntryMatch.Same;
        }

        return hash == PremadeAudio.HashOf(SpeakerFirst.Colon(text)) ? EntryMatch.WithColon : EntryMatch.Other;
    }

    // Only the text hash and the text change; the clip file name is never touched, so no clip is renamed, adopted or orphaned.
    private static void Retarget(JsonObject entry, string text)
    {
        entry["hash"] = PremadeAudio.HashOf(text);
        entry["text"] = text;
    }

    private static bool HasClip(JsonObject entry) => !string.IsNullOrWhiteSpace((string?)entry["clip"]);

    private static Dictionary<int, JsonObject> EntriesByIndex(JsonNode root)
    {
        JsonArray entries = root["entries"]?.AsArray() ?? throw new InvalidOperationException("The manifest has no entries.");
        var byIndex = new Dictionary<int, JsonObject>();
        foreach (JsonObject entry in entries.OfType<JsonObject>().Where(e => e["index"] is not null))
        {
            int index = (int)entry["index"]!;
            if (!byIndex.TryAdd(index, entry))
            {
                throw new InvalidOperationException($"The manifest has two entries at position {index}; fix the manifest before joining.");
            }
        }

        return byIndex;
    }

    private static string Write(JsonNode root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            root.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
