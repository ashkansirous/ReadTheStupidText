using System.Text.Json.Nodes;
using DinoReader.BookBuilder;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 281: the plan of a listening track (order, pages, gaps, a missing clip) and the ffmpeg join when ffmpeg is installed.</summary>
public sealed class ListenTrackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-listen-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story TwoPageStory() =>
        new("a02c0731b7ac475a896e9184f3575e7b", "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One. Two."), new StoryPage("Three.")], [], CoverStyle.Colour, 0, string.Empty);

    private static SentenceListing Listing(params int[] sentenceOfEachPiece) =>
        new("book", "en", sentenceOfEachPiece.Select((s, i) => new SpokenSentence(i, s, "text " + i, "text " + i, null, true)).ToList());

    [Fact]
    public void TheSentenceGapIsTheAppsOwn()
    {
        Assert.Equal((int)VoicedAudio.DefaultSentenceGap.TotalMilliseconds, ListenTrackPlan.SentenceGapMs);
        Assert.Equal(PhraseSplitter.SentenceGapMs, ListenTrackPlan.SentenceGapMs);
        Assert.True(ListenTrackPlan.PageGapMs > ListenTrackPlan.SentenceGapMs);
    }

    [Fact]
    public void PagesOfSentences_PutsEachSentenceOnItsPage()
    {
        Story story = TwoPageStory();
        LanguageRules rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, rules, story.BreakMap());

        Assert.Equal([0, 0, 1], ListenTrackPlan.PagesOfSentences(story, spans));
    }

    [Fact]
    public void Clips_AreInReadingOrder_WithTheirPage()
    {
        var byIndex = new Dictionary<int, string> { [0] = "c.m4a", [1] = "a.m4a", [2] = "b.m4a" };

        IReadOnlyList<ListenClip> clips = ListenTrackPlan.Clips(Listing(0, 1, 2), [0, 0, 1], byIndex);

        Assert.Equal(["c.m4a", "a.m4a", "b.m4a"], clips.Select(c => c.File));
        Assert.Equal([0, 0, 1], clips.Select(c => c.Page));
    }

    [Fact]
    public void Steps_PutTheSentenceGapWithinAPage_AndThePageGapAtAPageTurn_NoneAtTheEnds()
    {
        ListenClip[] clips = [new(0, 0, "a"), new(1, 0, "b"), new(2, 1, "c")];

        IReadOnlyList<ListenStep> steps = ListenTrackPlan.Steps(clips);

        Assert.Equal(["a", null, "b", null, "c"], steps.Select(s => s.File));
        Assert.Equal([ListenTrackPlan.SentenceGapMs, ListenTrackPlan.PageGapMs], steps.Where(s => s.File is null).Select(s => s.SilenceMs));
    }

    [Fact]
    public void Clips_AMissingClipFailsTheBuild_NamingThePiece()
    {
        var byIndex = new Dictionary<int, string> { [0] = "a.m4a", [2] = "c.m4a" };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ListenTrackPlan.Clips(Listing(0, 1, 2), [0, 0, 0], byIndex));

        Assert.Contains("piece 1", error.Message);
    }

    [Fact]
    public void Clips_ABookWithNothingToSayFails()
    {
        Assert.Throws<InvalidOperationException>(() => ListenTrackPlan.Clips(Listing(), [0], new Dictionary<int, string>()));
    }

    [Fact]
    public void ClipsByIndex_ReadsTheManifest()
    {
        Directory.CreateDirectory(_root);
        WriteManifest("en", Entry(0, "a.m4a"), Entry(1, "b.m4a"));
        File.WriteAllBytes(Path.Combine(_root, "a.m4a"), [1]);
        File.WriteAllBytes(Path.Combine(_root, "b.m4a"), [1]);

        IReadOnlyDictionary<int, string> byIndex = AudioPacks.ClipsByIndex(_root, "en");

        Assert.Equal("b.m4a", byIndex[1]);
        Assert.Equal(2, byIndex.Count);
    }

    [Fact]
    public void ClipsByIndex_UsesThePacksValidation_LanguageLfsPointerAndPlainNames()
    {
        Directory.CreateDirectory(_root);
        WriteManifest("en", Entry(0, "a.m4a"));
        Assert.Throws<InvalidOperationException>(() => AudioPacks.ClipsByIndex(_root, "fa")); // another language
        Assert.Throws<InvalidOperationException>(() => AudioPacks.ClipsByIndex(_root, "en")); // a.m4a is missing

        File.WriteAllText(Path.Combine(_root, "a.m4a"), "version https://git-lfs.github.com/spec/v1\noid sha256:0\nsize 1\n");
        InvalidOperationException pointer = Assert.Throws<InvalidOperationException>(() => AudioPacks.ClipsByIndex(_root, "en"));
        Assert.Contains("LFS", pointer.Message);

        WriteManifest("en", Entry(0, "../a.m4a"));
        Assert.Throws<InvalidOperationException>(() => AudioPacks.ClipsByIndex(_root, "en")); // a path, not a plain name
    }

    private void WriteManifest(string language, params JsonObject[] entries)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, AudioPacks.ManifestFile), new JsonObject
        {
            ["version"] = AudioPacks.ManifestVersion,
            ["language"] = language,
            [AudioPacks.EntriesKey] = new JsonArray(entries.Select(e => (JsonNode)e).ToArray()),
        }.ToJsonString());
    }

    [Fact]
    public void Join_MakesOneTrackWhoseLengthIsTheClipsPlusTheGaps_WhenFfmpegIsInstalled()
    {
        Assert.SkipWhen(!MakeTone(Path.Combine(_root, "a.m4a")), "ffmpeg is not installed here");
        MakeTone(Path.Combine(_root, "b.m4a"));
        ListenStep[] steps = [new("a.m4a", 0), new(null, ListenTrackPlan.PageGapMs), new("b.m4a", 0)];
        string target = Path.Combine(_root, "out", ListenTrackPlan.FileName("en", 1));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        int seconds = ListenTrack.Join(steps, _root, target);

        Assert.Equal(3, seconds); // 1 s + 0.9 s + 1 s = 2.9 s, rounded up
        Assert.True(new FileInfo(target).Length > 0);
        Assert.Single(Directory.GetFileSystemEntries(Path.GetDirectoryName(target)!)); // no scratch left behind
    }

    [Fact]
    public void Join_AMissingClipFails()
    {
        Directory.CreateDirectory(_root);

        Assert.Throws<InvalidOperationException>(() => ListenTrack.Join([new ListenStep("nope.m4a", 0)], _root, Path.Combine(_root, "t.m4a")));
    }

    [Fact]
    public void Join_ClipsOfDifferentSampleRatesFailTheTrack_NamingTheClip()
    {
        Assert.SkipWhen(!MakeTone(Path.Combine(_root, "a.m4a")), "ffmpeg is not installed here");
        MakeTone(Path.Combine(_root, "b.m4a"), sampleRate: 44100);

        var error = Assert.Throws<InvalidOperationException>(() => ListenTrack.Join([new ListenStep("a.m4a", 0), new ListenStep("b.m4a", 0)], _root, Path.Combine(_root, "t.m4a")));

        Assert.Contains("b.m4a is 44100 Hz", error.Message);
    }

    [Fact]
    public void ReadFormat_ReadsTheSampleRateAndChannelsOfAClip()
    {
        Assert.SkipWhen(!MakeTone(Path.Combine(_root, "a.m4a")), "ffmpeg is not installed here");
        MakeTone(Path.Combine(_root, "b.m4a"), sampleRate: 44100);

        Assert.Equal((24000, 1), ListenTrack.ReadFormat(Path.Combine(_root, "a.m4a")));
        Assert.Equal((44100, 1), ListenTrack.ReadFormat(Path.Combine(_root, "b.m4a")));
    }

    [Fact]
    public void ReadFormat_WalksTheBoxesOfAFaststartHeader()
    {
        string path = Path.Combine(_root, "walk.m4a");
        Directory.CreateDirectory(_root);
        byte[] stsd = Box("stsd", [0, 0, 0, 0, 0, 0, 0, 1], Mp4aEntry(channels: 2, rate: 44100));
        byte[] moov = Box("moov", Box("trak", Box("mdia", Box("minf", Box("stbl", stsd)))));
        File.WriteAllBytes(path, [.. Box("ftyp", "M4A "u8.ToArray(), [0, 0, 0, 0]), .. moov, .. Box("mdat", new byte[100])]);

        Assert.Equal((44100, 2), ListenTrack.ReadFormat(path));
    }

    [Fact]
    public void ReadFormat_IgnoresAnMp4aPatternInsideMediaData_AndAsksFfprobeWhenMoovIsAtTheEnd()
    {
        Assert.SkipWhen(!MakeTone(Path.Combine(_root, "slow.m4a"), seconds: 90), "ffmpeg is not installed here");
        string path = Path.Combine(_root, "slow.m4a");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 70_000, "the clip must be long enough for moov to sit past the first 64 KB");
        Assert.Equal((24000, 1), ListenTrack.ReadFormat(path)); // ffmpeg's default mp4 has moov last: the walk finds nothing and ffprobe answers
        int mdat = bytes.AsSpan().IndexOf("mdat"u8) + 4;
        "mp4a"u8.CopyTo(bytes.AsSpan(mdat + 100));
        File.WriteAllBytes(path, bytes);
        Assert.Equal((24000, 1), ListenTrack.ReadFormat(path)); // raw "mp4a" bytes in the media data are not a sample entry
    }

    private static byte[] Mp4aEntry(int channels, int rate)
    {
        var entry = new List<byte>(new byte[16]);
        entry.AddRange([(byte)(channels >> 8), (byte)channels, 0, 16, 0, 0, 0, 0, (byte)(rate >> 8), (byte)rate, 0, 0]);
        return Box("mp4a", entry.ToArray());
    }

    private static byte[] Box(string type, params byte[][] parts)
    {
        byte[] payload = parts.SelectMany(p => p).ToArray();
        byte[] size = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(size, (uint)(payload.Length + 8));
        return [.. size, .. System.Text.Encoding.ASCII.GetBytes(type), .. payload];
    }

    private static bool MakeTone(string path, int sampleRate = 24000, int seconds = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string ffmpeg = Environment.GetEnvironmentVariable(ListenTrack.FfmpegEnv) is { Length: > 0 } named ? named : "ffmpeg";
        var start = new System.Diagnostics.ProcessStartInfo(ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}:sample_rate=" + sampleRate, "-ac", "1", "-c:a", "aac", path])
        { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [Fact]
    public void ConcatList_HasOneFileLinePerStep_ClipsThenSilences_InOrder()
    {
        IReadOnlyList<ListenStep> steps = ListenTrackPlan.Steps([new(0, 0, "a.m4a"), new(1, 0, "b.m4a"), new(2, 1, "c.m4a")]);

        string[] lines = ListenTrackPlan.ConcatList(steps, Path.Combine(_root, "clips"), Path.Combine(_root, "tmp")).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("ffconcat version 1.0", lines[0]);
        Assert.Equal(steps.Count + 1, lines.Length);
        Assert.EndsWith("/clips/a.m4a'", lines[1]);
        Assert.EndsWith("/tmp/silence-150.m4a'", lines[2]);
        Assert.EndsWith("/tmp/silence-900.m4a'", lines[4]);
        Assert.DoesNotContain("\\", string.Concat(lines));
        Assert.Equal([150, 900], ListenTrackPlan.SilenceLengths(steps));
    }

    [Fact]
    public void ConcatList_EscapesASingleQuoteInAName()
    {
        string list = ListenTrackPlan.ConcatList([new ListenStep("it's.m4a", 0)], _root, _root);

        Assert.Contains("it'\\''s.m4a'", list);
    }

    [Fact]
    public void ALongBook_IsALongList_NotALongCommand()
    {
        ListenClip[] clips = Enumerable.Range(0, 3000).Select(i => new ListenClip(i, i / 10, $"clip-{i:D5}.m4a")).ToArray();
        IReadOnlyList<ListenStep> steps = ListenTrackPlan.Steps(clips);

        string list = ListenTrackPlan.ConcatList(steps, _root, _root);

        Assert.Equal(steps.Count + 1, list.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(ListenTrack.ArgumentsOf("l", "o").Count, ListenTrack.ArgumentsOf(Path.Combine(_root, "l"), Path.Combine(_root, "o")).Count);
        Assert.DoesNotContain(ListenTrack.ArgumentsOf("l", "o"), argument => argument.Contains("clip-"));
        Assert.Equal(2, ListenTrackPlan.SilenceLengths(steps).Count); // two silence files for 3000 clips, not one input each
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0.000000")]
    [InlineData("-3")]
    [InlineData("NaN")]
    public void DurationSeconds_ANotAvailableOrNonNumericDurationFailsTheTrack(string probe)
    {
        Assert.Throws<InvalidOperationException>(() => ListenTrack.DurationSeconds(probe + "\n", "t.m4a"));
    }

    [Fact]
    public void DurationSeconds_ReadsFfprobesNumber()
    {
        Assert.Equal(94.512, ListenTrack.DurationSeconds("94.512000\r\n", "t.m4a"), 3);
    }

    private static JsonObject Entry(int index, string clip) => new() { ["index"] = index, ["clip"] = clip };

    [Fact]
    public void FileName_CarriesLanguageAndPackVersion() => Assert.Equal("listen-fa-v3.m4a", ListenTrackPlan.FileName("fa", 3));

    [Fact]
    public void MetaOf_ListsListenSecondsOnlyOnceTheTrackWasBuilt_AndKeepsTheOldKeyOrder()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, AudioPacks.CastFile),
            """{"audioPacks":{"en":{"version":2,"hash":"h","bytes":10,"listenSeconds":95},"fa":{"version":1,"hash":"h","bytes":5}}}""");

        string json = System.Text.Json.JsonSerializer.Serialize(AudioPacks.MetaOf(new BookFolder("x", _root, new BookInfo(BookKind.Store, "a02c0731b7ac475a896e9184f3575e7b", "Tiny", 1, 3, 6, "Me", "Mine", 0, "2026-10-27", 0, null, null, null, null, null))));

        Assert.Equal("""{"en":{"version":2,"bytes":10,"listenSeconds":95},"fa":{"version":1,"bytes":5}}""", json);
    }
}
