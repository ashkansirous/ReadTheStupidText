using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary><c>join-audio</c> (Slice 280): the Persian and Arabic manifests follow the speaker-first join from the clips there already are; no piece the app reads may lack one.</summary>
public sealed class JoinAudioTests
{
    private const string Tag = "پیم گفت.";
    private const string Said = "A.";

    private static SpokenSentence Piece(int index, string text) => new(index, index, text, text, null, true);

    private static string Manifest(params (int Index, string Text, string Clip)[] entries) =>
        "{\"version\":3,\"language\":\"fa\",\"entries\":[" + string.Join(",", entries.Select(e =>
            $"{{\"index\":{e.Index},\"hash\":\"{PremadeAudio.HashOf(e.Text)}\",\"voice\":\"Hali\",\"clip\":\"{e.Clip}\",\"text\":\"{e.Text}\"}}")) + "]}";

    private static string ClipFor(string text) => PremadeAudio.HashOf(text)[..16] + "-Hali-gemini-tts-6de2d2f6.m4a";

    private static IEnumerable<(string Hash, string Clip, string Text)> Entries(string json) =>
        AudioAlignment.ReadEntries(json).Select(e => (e.Hash, e.Clip!, e.Text!));

    [Fact]
    public void ATagMadeForItsColonText_TakesTheTextAsTranslated_AndKeepsItsClipFileName()
    {
        string colon = SpeakerFirst.Colon(Tag);
        string json = Manifest((0, colon, ClipFor(colon)), (1, Said, ClipFor(Said)));

        JoinRewrite result = JoinAudio.Rewrite(json, [Piece(0, Tag), Piece(1, Said)]);

        Assert.Empty(result.Problems);
        Assert.Equal(1, result.Rewritten);
        (string hash, string clip, string text) = Entries(result.Json!).First();
        Assert.Equal((PremadeAudio.HashOf(Tag), ClipFor(colon), Tag), (hash, clip, text));
    }

    [Fact]
    public void AClipNotNamedFromItsHash_IsNeverRenamed()
    {
        string colon = SpeakerFirst.Colon(Tag);
        string json = Manifest((0, colon, "pim-said.m4a"));

        JoinRewrite result = JoinAudio.Rewrite(json, [Piece(0, Tag)]);

        Assert.Empty(result.Problems);
        Assert.Equal("pim-said.m4a", Entries(result.Json!).Single().Clip);
    }

    [Fact]
    public void AnEntryWithNoClip_FailsTheRewriteAndWritesNothing()
    {
        string json = Manifest((0, Said, ""));

        JoinRewrite result = JoinAudio.Rewrite(json, [Piece(0, Said)]);

        Assert.Null(result.Json);
        Assert.Contains("has an entry with no clip", Assert.Single(result.Problems));
    }

    [Fact]
    public void ARewrittenManifest_RewritesToItselfAndTheAppFindsEveryPiece()
    {
        string colon = SpeakerFirst.Colon(Tag);
        SpokenSentence[] dump = [Piece(0, Tag), Piece(1, Said)];
        string once = JoinAudio.Rewrite(Manifest((0, colon, ClipFor(colon)), (1, Said, ClipFor(Said))), dump).Json!;

        JoinRewrite twice = JoinAudio.Rewrite(once, dump);

        Assert.Equal(once, twice.Json);
        Assert.Equal(0, twice.Rewritten);
        PremadeAudio audio = PremadeAudio.Parse(once);
        Assert.NotNull(audio.ClipAt(0, Tag));
        Assert.NotNull(audio.ClipAt(1, Said));
    }

    [Fact]
    public void APieceWithNoClip_FailsTheRewriteAndWritesNothing()
    {
        string json = Manifest((0, Said, ClipFor(Said)));

        JoinRewrite result = JoinAudio.Rewrite(json, [Piece(0, Said), Piece(1, "never recorded.")]);

        Assert.Null(result.Json);
        Assert.Contains("position 1 \"never recorded.\" has no entry", Assert.Single(result.Problems));
    }

    [Fact]
    public void AnEntryForOtherWords_IsNoClip_AndAnEntryNoPieceUsesIsReported()
    {
        string json = Manifest((0, "other words.", ClipFor("other words.")), (1, Said, ClipFor(Said)));

        JoinRewrite result = JoinAudio.Rewrite(json, [Piece(0, Said)]);

        Assert.Null(result.Json);
        Assert.Contains(result.Problems, p => p.Contains("position 0") && p.Contains("has no clip"));
        Assert.Contains(result.Problems, p => p.Contains("belong to no piece"));
    }

    // The same rule as the real-manifest test below (check-audio's coverage: zero new clips), on a small manifest made here, so it runs in CI where the real manifests are Git LFS pointers.
    [Fact]
    public void ASyntheticManifest_IsStaleBeforeTheRewriteAndCompleteAfterIt()
    {
        string colon = SpeakerFirst.Colon(Tag);
        SpokenSentence[] dump = [Piece(0, Tag), Piece(1, Said), Piece(2, "B.")];
        string json = Manifest((0, colon, ClipFor(colon)), (1, Said, ClipFor(Said)), (2, "B.", ClipFor("B.")));
        Assert.NotEmpty(AudioCoverage.Gaps(AudioAlignment.ReadEntries(json), dump, _ => 1));

        JoinRewrite result = JoinAudio.Rewrite(json, dump);

        Assert.Empty(result.Problems);
        Assert.Empty(AudioCoverage.Gaps(AudioAlignment.ReadEntries(result.Json!), dump, _ => 1));
    }

    // Every store book in Persian and Arabic: each piece the app reads, tag first, has a clip with the text it is looked up by (check-audio's rule), so the join needs no new clip.
    // Manifests are Git LFS: with only pointers in the checkout (a normal CI test run) this has nothing to read, and the check-audio step of tools-selftests.yml and dino-store.yml does the same job.
    [Fact]
    public void EveryPieceOfTheJoinedBooks_HasAClip_InPersianAndArabic()
    {
        var problems = new List<string>();
        int checkedPacks = 0;
        foreach (BookFolder book in Books.All)
        {
            Story story = SentenceDump.StoryOf(book.Stem);
            foreach (string language in new[] { SpeakerFirst.Persian, SpeakerFirst.Arabic })
            {
                string folder = Path.Combine(book.Dir, "audio", language);
                string manifest = Path.Combine(folder, PremadeAudio.ManifestFile);
                Assert.SkipWhen(!File.Exists(manifest) || AudioPacks.IsLfsPointer(manifest), "the audio manifests are Git LFS pointers here: git lfs pull --include \"content/dino/*/audio/*/manifest.json\" --exclude \"\"");
                problems.AddRange(AudioAlignment.LanguageProblems(book.Stem, story, language, folder).Select(p => $"{book.Stem} {language}: {p}"));
                checkedPacks++;
            }
        }

        Assert.True(checkedPacks >= 2 * 16, "every one of the 16 books has a Persian and an Arabic pack");
        Assert.Empty(problems);
    }
}
