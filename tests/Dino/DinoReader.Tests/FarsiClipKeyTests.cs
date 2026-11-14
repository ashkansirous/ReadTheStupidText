using DinoReader.BookBuilder;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

/// <summary>
/// Slice 296: a MODEL of how many of a Farsi read's pieces find a clip; it is not a run of the app or the hook. The fixture pack is this test's own small copy of what the Debug hook
/// <c>install-audio-pack</c> writes (<c>DinoTestCommands.WriteFixturePackAsync</c>: one entry per spoken piece, hash of the piece, every third hash corrupted when <c>miss=text_mismatch</c>); keep it in step
/// with that method by hand. The reader's side is built from the same Core calls the reading session makes (<see cref="StoryPieces.Split"/> with whole pieces, <see cref="BundledTranslations.Compose"/>,
/// <see cref="StoryTranslation.TranslateJoinedAsync"/>) and looked up with the reader's <see cref="InstalledPack.Find"/> over real files, at the position the reader uses
/// (<see cref="PremadeAudio.SpokenCount"/> of the pieces before). <c>StoryVoicing</c> and <c>StoryTranslator</c> are MAUI-side and cannot be reached from this project, so the device run is the proof.
/// </summary>
public sealed class FarsiClipKeyTests : IDisposable
{
    private const string PigsStem = "three-little-pigs";
    private const string Farsi = "fa";
    private const string LivePackEnv = "DINO_LIVE_FA_MANIFEST";
    private const int PigsSpokenPieces = 78;
    private const int PigsMatchedWithCorruption = 52;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "farsi-clip-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    // The pieces the reader plays and keys its clips by: the reading session's Prepare (wholePieces, as a bundled table read) and the book's table through the translate-and-join.
    private static async Task<IReadOnlyList<string>> ReaderTextsAsync(Story story, int fromPosition = 0)
    {
        LanguageRules rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        LineBreakMap breaks = story.BreakMap();
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, rules, breaks);
        AttributionResult resolved = StoryCast.Resolve(new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, breaks)), story.Cast);
        IReadOnlyList<SplitSegment> whole = StoryPieces.Split(story, resolved, spans, fromPosition, wholePieces: true);
        IReadOnlyList<SplitSegment> translated = await StoryTranslation.TranslateJoinedAsync(
            whole, BundledTranslations.Compose(story, Farsi, null), Farsi, story.Language, story.Text, spans, CancellationToken.None);
        return translated.Select(p => p.Segment.Text).ToList();
    }

    // The fixture pack the hook writes, as an installed folder: its manifest and one file per clip.
    private InstalledPack InstallFixture(IReadOnlyList<string> texts, bool mismatch)
    {
        var spoken = texts.Where(t => t.Any(char.IsLetterOrDigit)).ToList(); // mirrors the hook's filter
        var entries = spoken.Select((t, i) => new { index = i, hash = PremadeAudio.HashOf(mismatch && i % MismatchEvery == 0 ? t + "#" : t), clip = ClipOf(i) });
        var manifest = new { version = PremadeAudio.ManifestVersion, book = PigsStem, language = Farsi, entries };
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, PremadeAudio.ManifestFile), System.Text.Json.JsonSerializer.Serialize(manifest));
        for (int i = 0; i < spoken.Count; i++)
        {
            File.WriteAllBytes(Path.Combine(_folder, ClipOf(i)), [0]);
        }

        return Open(_folder);
    }

    // Mirrors DinoTestCommands.MismatchEvery (the `i % MismatchEvery == 0` corruption rule) and FixtureClipOf.
    private const int MismatchEvery = 3;

    private static string ClipOf(int index) => $"fixture-{index}.m4a";

    private static InstalledPack Open(string folder) =>
        new(folder, 1, PremadeAudio.Parse(File.ReadAllText(Path.Combine(folder, PremadeAudio.ManifestFile))));

    // The reader's lookup (StoryVoicing.PremadeSource.PathOf) over a read's pieces: how many find a clip, and how many missed for each reason.
    private static (int Matched, int TextMismatch, int FileMissing) Lookup(IReadOnlyList<string> readerTexts, InstalledPack pack, int offset = 0)
    {
        int matched = 0, mismatch = 0, missing = 0;
        for (int piece = 0; piece < readerTexts.Count; piece++)
        {
            if (!PremadeAudio.IsSpoken(readerTexts[piece]))
            {
                continue;
            }

            ClipLookup found = pack.Find(offset + PremadeAudio.SpokenCount(readerTexts.Take(piece)), readerTexts[piece]);
            matched += found.Path is null ? 0 : 1;
            mismatch += found.Miss is ClipMiss.TextMismatch or ClipMiss.NoClip ? 1 : 0;
            missing += found.Miss == ClipMiss.FileMissing ? 1 : 0;
        }

        return (matched, mismatch, missing);
    }

    private static Task<IReadOnlyList<string>> PigsReadAsync() => ReaderTextsAsync(StoreBuilder.Build(Books.SpecOf(PigsStem)));

    [Fact]
    public async Task ACompleteFixturePack_MatchesEveryPiece_OfAFarsiRead()
    {
        IReadOnlyList<string> reader = await PigsReadAsync();

        (int matched, int mismatch, int missing) = Lookup(reader, InstallFixture(reader, mismatch: false));

        Assert.Equal(PigsSpokenPieces, matched);
        Assert.Equal(reader.Count(PremadeAudio.IsSpoken), matched);
        Assert.Equal(0, mismatch + missing);
    }

    // Slice 316 item 2: a read that starts on a later page (StoryVoicing.ClipOffsetAsync: the whole book's spoken count minus the partial read's) finds the clips that belong to its pieces, not the first ones.
    [Fact]
    public async Task AReadStartingMidBook_WithTheClipOffset_MatchesEveryPieceItPlays()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        IReadOnlyList<string> whole = await ReaderTextsAsync(story);
        IReadOnlyList<string> partial = await ReaderTextsAsync(story, story.Text.Length / 2);
        int offset = PremadeAudio.ClipOffset(whole, partial); // the method StoryVoicing.ClipOffsetAsync returns
        InstalledPack pack = InstallFixture(whole, mismatch: false);

        (int matched, int mismatch, int missing) = Lookup(partial, pack, offset);

        Assert.InRange(partial.Count(PremadeAudio.IsSpoken), 1, PigsSpokenPieces - 1);
        Assert.True(offset > 0);
        Assert.Equal(partial.Count(PremadeAudio.IsSpoken), matched);
        Assert.Equal(0, mismatch + missing);
    }

    [Fact]
    public void ClipOffset_CountsOnlySpokenPiecesBeforeTheStart()
    {
        string[] whole = ["One.", "...", "Two.", "Three."];

        Assert.Equal(1, PremadeAudio.ClipOffset(whole, ["Two.", "Three."]));
        Assert.Equal(2, PremadeAudio.ClipOffset(whole, ["Three."]));
        Assert.Equal(0, PremadeAudio.ClipOffset(whole, whole));
    }

    [Fact]
    public async Task AReadStartingMidBook_WithoutTheClipOffset_MissesItsClips()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        IReadOnlyList<string> whole = await ReaderTextsAsync(story);
        IReadOnlyList<string> partial = await ReaderTextsAsync(story, story.Text.Length / 2);

        (int matched, _, _) = Lookup(partial, InstallFixture(whole, mismatch: false));

        Assert.True(matched < partial.Count(PremadeAudio.IsSpoken));
    }

    [Fact]
    public async Task TheTextMismatchFixture_MissesOnlyEveryThirdPiece_NotMostOfThem()
    {
        IReadOnlyList<string> reader = await PigsReadAsync();
        int spoken = reader.Count(PremadeAudio.IsSpoken);
        int corrupted = (spoken + MismatchEvery - 1) / MismatchEvery;

        (int matched, int mismatch, int missing) = Lookup(reader, InstallFixture(reader, mismatch: true));

        Assert.Equal(PigsSpokenPieces, spoken);
        Assert.Equal(PigsMatchedWithCorruption, matched);
        Assert.Equal(spoken - corrupted, matched);
        Assert.Equal(corrupted, mismatch);
        Assert.Equal(0, missing);
    }

    // Reads the live pack's manifest (downloaded read-only by hand, with its clip files not needed: they are created empty) when DINO_LIVE_FA_MANIFEST names it.
    [Fact]
    public async Task TheLiveStorePack_MatchesTheReadersPieces()
    {
        string? path = Environment.GetEnvironmentVariable(LivePackEnv);
        Assert.SkipWhen(string.IsNullOrEmpty(path) || !File.Exists(path), $"Set {LivePackEnv} to a downloaded fa manifest.json to run this.");

        IReadOnlyList<string> texts = await PigsReadAsync();
        List<string> spoken = texts.Where(PremadeAudio.IsSpoken).ToList();
        Directory.CreateDirectory(_folder);
        File.Copy(path!, Path.Combine(_folder, PremadeAudio.ManifestFile));
        InstalledPack live = Open(_folder);
        Assert.Equal(live.Audio.Count, spoken.Count);
        for (int i = 0; i < spoken.Count; i++)
        {
            if (live.Audio.ClipAt(i, spoken[i]) is { } clip)
            {
                File.WriteAllBytes(Path.Combine(_folder, clip), [0]);
            }
        }

        (int matched, int mismatch, int missing) = Lookup(texts, live);

        Assert.Equal(0, mismatch + missing);
        Assert.Equal(live.Audio.Count, matched);
    }
}
