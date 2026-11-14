using System.IO.Compression;
using System.Text;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 277: a store book needs a complete pack in every offered language: no sentence without a clip, no stale clip, no missing or empty clip file, no missing pack.</summary>
public sealed class AudioCoverageTests
{
    private static SpokenSentence Piece(int index, string text) => new(index, index, text, text, null, true);

    private static string ClipOf(string text) => PremadeAudio.HashOf(text)[..8] + ".m4a";

    private static ManifestEntry Entry(int index, string text) => new(index, PremadeAudio.HashOf(text), text, ClipOf(text));

    private static readonly SpokenSentence[] Dump = [Piece(0, "one"), Piece(1, "two"), Piece(2, "three")];

    private static long? Present(string clip) => 1000;

    [Fact]
    public void ACompletePack_HasNoGap()
    {
        Assert.Empty(AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "two"), Entry(2, "three")], Dump, Present));
    }

    [Fact]
    public void ASentenceWithNoClip_IsReported_WithItsPositionAndText()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "two")], Dump, Present);

        string gap = Assert.Single(gaps);
        Assert.Contains("1 of 3 sentence(s) have no clip", gap);
        Assert.Contains("position 2 \"three\"", gap);
    }

    [Fact]
    public void AClipForTextThatChanged_IsStale_AndTheNewTextHasNoClip()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "old two"), Entry(2, "three")], Dump, Present);

        Assert.Contains(gaps, g => g.Contains("1 of 3 sentence(s) have no clip") && g.Contains("\"two\""));
        Assert.Contains(gaps, g => g.Contains("1 clip(s) are for text that no longer exists") && g.Contains("\"old two\""));
    }

    [Fact]
    public void AnExtraEntryPastTheLastSentence_IsStale()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "two"), Entry(2, "three"), Entry(3, "gone")], Dump, Present);

        string gap = Assert.Single(gaps);
        Assert.Contains("no longer exists", gap);
    }

    [Fact]
    public void AnEmptyClipFile_IsReported_ByName()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "two"), Entry(2, "three")], Dump, clip => clip == ClipOf("two") ? 0 : 1000);

        string gap = Assert.Single(gaps);
        Assert.Contains("empty", gap);
        Assert.Contains(ClipOf("two"), gap);
    }

    [Fact]
    public void AClipFileThatIsNotThere_IsReported_ByName()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "one"), Entry(1, "two"), Entry(2, "three")], Dump, clip => clip == ClipOf("three") ? null : 1000);

        string gap = Assert.Single(gaps);
        Assert.Contains("missing", gap);
        Assert.Contains(ClipOf("three"), gap);
    }

    [Fact]
    public void ALanguageWithoutAManifest_IsReportedAsNoPack()
    {
        string folder = Directory.CreateTempSubdirectory("dino-cov").FullName;
        try
        {
            Story story = SentenceDump.StoryOf("lion-and-mouse");

            IEnumerable<string> problems = AudioAlignment.LanguageProblems("lion-and-mouse", story, "fa", folder);

            Assert.Contains("no audio pack", Assert.Single(problems));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ABookWithNoAudioFolder_IsRefusedByTheStoreBuild_InEveryOfferedLanguage()
    {
        string folder = Directory.CreateTempSubdirectory("dino-cov-book").FullName;
        try
        {
            BookFolder real = Books.Folder("lion-and-mouse");
            var book = new BookFolder(real.Stem, folder, real.Info);

            IReadOnlyList<string> problems = StoreValidation.AudioProblems(book);

            Assert.Equal(AudioCoverage.OfferedLanguages.Count, problems.Count(p => p.Contains("no audio pack")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The CI test job checks out without git lfs pull: the text-only validation must not look at manifests, while the store build (includeAudio) fails closed on a pointer.
    [Fact]
    public void ManifestsThatAreLfsPointers_AreIgnoredByTheTextOnlyValidation_AndRefusedByTheAudioValidation()
    {
        string folder = Directory.CreateTempSubdirectory("dino-cov-lfs").FullName;
        try
        {
            foreach (string language in AudioCoverage.OfferedLanguages)
            {
                Directory.CreateDirectory(Path.Combine(folder, "audio", language));
                File.WriteAllText(Path.Combine(folder, "audio", language, "manifest.json"), "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 10\n");
            }

            BookFolder real = Books.Folder("lion-and-mouse");
            var book = new BookFolder(real.Stem, folder, real.Info);

            IReadOnlyDictionary<string, IReadOnlyList<string>> textOnly = StoreValidation.ProblemsByBook([book]);
            IReadOnlyDictionary<string, IReadOnlyList<string>> withAudio = StoreValidation.ProblemsByBook([book], includeAudio: true);

            Assert.DoesNotContain(textOnly.GetValueOrDefault(book.Stem) ?? [], p => p.Contains("LFS pointer") || p.Contains("audio"));
            Assert.Contains(withAudio[book.Stem], p => p.Contains("the manifest is a Git LFS pointer"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ALanguageWithAManifestButNoAudioPacksRecord_IsAGap()
    {
        string folder = Directory.CreateTempSubdirectory("dino-cov-record").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "audio", "en"));
            File.WriteAllText(Path.Combine(folder, "audio", "en", "manifest.json"), "{}");
            BookFolder real = Books.Folder("lion-and-mouse");

            List<string> problems = AudioAlignment.ProblemsOf(new BookFolder(real.Stem, folder, real.Info));

            Assert.Contains(problems, p => p.StartsWith($"{real.Stem} en:") && p.Contains("audioPacks record"));
            Assert.DoesNotContain(problems, p => p.StartsWith($"{real.Stem} fa:") && p.Contains("audioPacks record"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void EveryLanguageTheAppHasIsOffered()
    {
        Assert.Equal(UiLanguage.Supported, AudioCoverage.OfferedLanguages);
        Assert.Contains("fa", AudioCoverage.OfferedLanguages);
        Assert.Contains("en", AudioCoverage.OfferedLanguages);
    }

    [Fact]
    public void AGitLfsPointerClip_IsMeasuredByTheSizeItRecords()
    {
        const string pointer = "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 37572\n";
        const string emptyPointer = "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 0\n";

        Assert.Equal(37572, AudioCoverage.LfsPointerSizeOf(pointer));
        Assert.Equal(0, AudioCoverage.LfsPointerSizeOf(emptyPointer));
    }

    [Fact]
    public void ALiveListIsParsed_WithItsPackUrls()
    {
        const string json = """{"stories":[{"id":"aa","version":12,"audio":{"fa":{"version":2,"url":"/dl/fa.zip"}}},{"title":"no id"},{"id":"bb","version":1}]}""";

        IReadOnlyList<LiveStory> stories = LiveAudioCheck.ParseList(json);

        Assert.Equal(2, stories.Count);
        Assert.Equal(12, stories[0].Version);
        Assert.Equal("/dl/fa.zip", stories[0].AudioUrls["fa"]);
        Assert.Empty(stories[1].AudioUrls);
    }

    [Fact]
    public void ALivePackWithEveryClip_HasNoGap_AndOneWithAClipMissingOrEmptyHasOne()
    {
        string manifest = ManifestJson("one", "two", "three");

        Assert.Empty(LiveAudioCheck.PackGaps(Zip(manifest, ClipOf("one"), ClipOf("two"), ClipOf("three")), Dump, "fa"));
        Assert.Contains("missing", Assert.Single(LiveAudioCheck.PackGaps(Zip(manifest, ClipOf("one"), ClipOf("two")), Dump, "fa")));
        Assert.Contains("empty", Assert.Single(LiveAudioCheck.PackGaps(ZipWithEmpty(manifest, ClipOf("one"), ClipOf("one"), ClipOf("two"), ClipOf("three")), Dump, "fa")));
    }

    [Fact]
    public void ALivePackWithoutAManifest_IsReported()
    {
        Assert.Contains("no manifest.json", Assert.Single(LiveAudioCheck.PackGaps(Zip(null), Dump, "fa")));
    }

    private static string ManifestJson(params string[] texts)
    {
        IEnumerable<string> entries = texts.Select((text, index) => $$"""{"index":{{index}},"hash":"{{PremadeAudio.HashOf(text)}}","clip":"{{ClipOf(text)}}","text":"{{text}}"}""");
        return $$"""{"version":3,"language":"fa","entries":[{{string.Join(",", entries)}}]}""";
    }

    private static ZipArchive Zip(string? manifest, params string[] clips) => ZipWithEmpty(manifest, null, clips);

    private static ZipArchive ZipWithEmpty(string? manifest, string? emptyClip, params string[] clips)
    {
        var stream = new MemoryStream();
        using (var writer = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (manifest is not null)
            {
                Add(writer, PremadeAudio.ManifestFile, manifest);
            }

            foreach (string clip in clips)
            {
                Add(writer, clip, clip == emptyClip ? string.Empty : "audio");
            }
        }

        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using Stream entry = zip.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes(content));
    }
}
