using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>One live store story as the live check needs it: its id, version and audio packs (language to download url).</summary>
internal sealed record LiveStory(string Id, int Version, IReadOnlyDictionary<string, string> AudioUrls);

/// <summary>
/// <c>check-audio --live</c> (Slice 277, Slice 275 item 6; it replaces the old <c>reviews/lion-fa/live_scan.py</c>): the same completeness check as the repo one, run over what the LIVE store serves. For each
/// repo book and offered language it downloads the live audio pack (cached, one download per url), then compares its manifest with the sentences the app reads and looks inside the zip for every clip.
/// A language the live store has no pack for, a pack that does not line up, and a live book version BELOW the repo's book.json version (the store is behind) are all problems. A live version above book.json's is normal (Slice 239 versions a changed book as live + 1).
/// Read only: nothing is uploaded or changed.
/// </summary>
internal static class LiveAudioCheck
{
    public const string StoriesUrl = "https://dinoreader.sirous.uk/api/stories";
    private const string UserAgent = "curl/8"; // the store's edge refuses an empty or library user agent
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(300);

    /// <summary>The stories of the live list JSON (<c>{ "stories": [ { id, version, audio: { lang: { url } } } ] }</c>); a story without an id is left out.</summary>
    public static IReadOnlyList<LiveStory> ParseList(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var stories = new List<LiveStory>();
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("stories", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The live story list has no \"stories\" array (an error response?).");
        }

        foreach (JsonElement story in list.EnumerateArray())
        {
            if (story.TryGetProperty("id", out JsonElement id) && id.GetString() is { Length: > 0 } storyId)
            {
                int version = story.TryGetProperty("version", out JsonElement v) && v.TryGetInt32(out int number) ? number : 0;
                stories.Add(new LiveStory(storyId, version, AudioUrls(story)));
            }
        }

        return stories;
    }

    private static Dictionary<string, string> AudioUrls(JsonElement story)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        if (story.TryGetProperty("audio", out JsonElement audio) && audio.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty pack in audio.EnumerateObject().Where(p => p.Value.TryGetProperty("url", out _)))
            {
                urls[pack.Name] = pack.Value.GetProperty("url").GetString() ?? string.Empty;
            }
        }

        return urls;
    }

    /// <summary>The gaps of one live pack: the manifest inside the zip against the sentences, and every clip the manifest names inside the zip (its size is the entry's length).</summary>
    public static IReadOnlyList<string> PackGaps(ZipArchive zip, IReadOnlyList<SpokenSentence> dump, string language)
    {
        ZipArchiveEntry? manifest = zip.GetEntry(PremadeAudio.ManifestFile);
        if (manifest is null)
        {
            return [$"the live pack has no {PremadeAudio.ManifestFile}"];
        }

        string json;
        using (var reader = new StreamReader(manifest.Open(), Encoding.UTF8))
        {
            json = reader.ReadToEnd();
        }

        return AudioAlignment.ParseProblem(json, language) is { } rejected
            ? [rejected]
            : AudioCoverage.Gaps(AudioAlignment.ReadEntries(json), dump, clip => zip.GetEntry(clip)?.Length);
    }

    /// <summary>The live-versus-repo problems of one book: its live version differs from book.json's, it is not live at all, or a language lacks a pack or has gaps. <paramref name="packOf"/> gives the downloaded pack of a url.</summary>
    internal static List<string> BookProblems(BookFolder book, LiveStory? live, Func<string, ZipArchive> packOf)
    {
        if (live is null)
        {
            return [$"{book.Stem}: not in the live store list"];
        }

        var lines = new List<string>();
        // Store versions are derived at publish time (Slice 239: live + 1 when the content changed), so a live version ABOVE book.json's is normal; only a live version below it means the store is behind.
        if (live.Version < book.Info.Version)
        {
            lines.Add($"{book.Stem}: the live store has v{live.Version}, behind content/dino/{book.Stem}/book.json v{book.Info.Version}");
        }

        Story story = SentenceDump.StoryOf(book.Stem);
        foreach (string language in AudioCoverage.OfferedLanguages)
        {
            IEnumerable<string> found = live.AudioUrls.TryGetValue(language, out string? url)
                ? LivePackGaps(packOf, url, book.Stem, story, language)
                : ["the live store lists no audio pack"];
            lines.AddRange(found.Select(problem => $"{book.Stem} {language}: {problem}"));
        }

        return lines;
    }

    private static IReadOnlyList<string> LivePackGaps(Func<string, ZipArchive> packOf, string url, string stem, Story story, string language)
    {
        using ZipArchive zip = packOf(url);
        return PackGaps(zip, SentenceDump.Build(stem, story, language).Sentences, language);
    }

    /// <summary>Runs the live check over the named books (all when none); the result carries one verdict per book.</summary>
    public static async Task<AudioCheckResult> RunAsync(IReadOnlyList<string> stems, string listUrl, string cacheFolder)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        IReadOnlyList<LiveStory> list = ParseList(await http.GetStringAsync(listUrl));
        Directory.CreateDirectory(cacheFolder);
        Uri origin = new(listUrl);
        ZipArchive PackOf(string url) => OpenPack(http, new Uri(origin, url), Path.Combine(cacheFolder, CacheName(url)));

        IEnumerable<BookFolder> books = stems.Count == 0 ? Books.All : stems.Select(Books.Folder);
        return AudioAlignment.CheckBooks(books, (book, problems) =>
        {
            problems.AddRange(BookProblems(book, list.FirstOrDefault(s => string.Equals(s.Id, book.Info.Id, StringComparison.OrdinalIgnoreCase)), PackOf));
            return AudioCoverage.OfferedLanguages.Count;
        });
    }

    private static string CacheName(string url) => string.Concat(url.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));

    /// <summary>Opens a pack from the cache, or downloads it. A cached file that is not a readable zip (a write cut short, a bad earlier download) is dropped and downloaded once more.</summary>
    private static ZipArchive OpenPack(HttpClient http, Uri url, string cachePath)
    {
        if (File.Exists(cachePath) && TryOpen(File.ReadAllBytes(cachePath)) is { } cached)
        {
            return cached;
        }

        File.Delete(cachePath);
        byte[] bytes = DownloadAsync(http, url, cachePath).GetAwaiter().GetResult();
        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    private static ZipArchive? TryOpen(byte[] bytes)
    {
        try
        {
            return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            // no-fallback: an unreadable cached pack is dropped by the caller, which downloads it again
            return null;
        }
    }

    // The cache file appears whole or not at all: written beside it, then renamed over it.
    private static async Task<byte[]> DownloadAsync(HttpClient http, Uri url, string cachePath)
    {
        await Task.Delay(Pause);
        byte[] bytes = await http.GetByteArrayAsync(url);
        string temp = $"{cachePath}.{Environment.ProcessId}.tmp";
        await File.WriteAllBytesAsync(temp, bytes);
        File.Move(temp, cachePath, overwrite: true);
        return bytes;
    }
}
