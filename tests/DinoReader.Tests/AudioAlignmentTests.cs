using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary><c>check-audio</c> (Slice 272): a manifest must hash the app's piece at every position, with no entry left over.</summary>
public sealed class AudioAlignmentTests
{
    private static SpokenSentence Piece(int index, string text) => new(index, index, text, text, null, true);

    private static ManifestEntry Entry(int index, string text) => new(index, PremadeAudio.HashOf(text), text, text + ".m4a");

    private static long? Present(string clip) => 1000;

    [Fact]
    public void AManifestThatHashesEveryPiece_HasNoGap()
    {
        SpokenSentence[] dump = [Piece(0, "a"), Piece(1, "b")];

        Assert.Empty(AudioCoverage.Gaps([Entry(0, "a"), Entry(1, "b")], dump, Present));
    }

    [Fact]
    public void ASoundWordEntry_ShiftsEveryLaterPosition_AndTheFirstBadOneIsNamed()
    {
        SpokenSentence[] dump = [Piece(0, "a"), Piece(1, "b"), Piece(2, "c")];
        ManifestEntry[] manifest = [Entry(0, "a"), Entry(1, "roar!"), Entry(2, "b"), Entry(3, "c")];

        IReadOnlyList<string> gaps = AudioCoverage.Gaps(manifest, dump, Present);

        Assert.Contains(gaps, g => g.Contains("2 of 3 sentence(s) have no clip") && g.Contains("position 1 \"b\""));
        Assert.Contains(gaps, g => g.Contains("clip(s) are for text that no longer exists") && g.Contains("roar!"));
    }

    [Fact]
    public void AMissingEntry_IsNamedAtItsPosition()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "a")], [Piece(0, "a"), Piece(1, "b")], Present);

        Assert.Contains("position 1 \"b\"", Assert.Single(gaps));
    }

    [Fact]
    public void AnEntryPastTheLastPiece_IsAGapEvenWhenEveryPieceMatches()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "a"), Entry(1, "extra")], [Piece(0, "a")], Present);

        Assert.Contains("no longer exists", Assert.Single(gaps));
    }

    [Fact]
    public void TwoEntriesAtOnePosition_AreAGap_EvenWhenOneOfThemMatches()
    {
        IReadOnlyList<string> gaps = AudioCoverage.Gaps([Entry(0, "a"), Entry(0, "orphan")], [Piece(0, "a")], Present);

        Assert.Contains("more than one manifest entry", Assert.Single(gaps));
    }

    [Fact]
    public void AManifestForAnotherLanguage_IsRejected()
    {
        const string json = """{"version":3,"language":"de","entries":[{"index":0,"hash":"abc","clip":"a.m4a"}]}""";

        Assert.Contains("not \"fa\"", AudioAlignment.ParseProblem(json, "fa"));
        Assert.Null(AudioAlignment.ParseProblem(json, "de"));
    }

    [Fact]
    public void TheVerdictFile_CarriesStemIdAndWhetherEachBookIsComplete()
    {
        string json = AudioAlignment.VerdictJson([new BookVerdict("a", "id-a", []), new BookVerdict("b", "id-b", ["gap"]), new BookVerdict("c", "id-c", [], "TOOL ERROR c")]);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var books = document.RootElement.GetProperty("books").EnumerateArray().ToList();
        Assert.Equal([true, false, false], books.Select(b => b.GetProperty("complete").GetBoolean()));
        Assert.Equal(["id-a", "id-b", "id-c"], books.Select(b => b.GetProperty("id").GetString()));
    }

    [Fact]
    public void ABookWhoseCheckThrows_GetsAVerdictThatIsNotComplete_AndAGapBookToo()
    {
        BookFolder[] books = [Books.Folder("three-little-pigs"), Books.Folder("lion-and-mouse")];

        AudioCheckResult result = AudioAlignment.CheckBooks(books, (book, problems) =>
        {
            if (book.Stem == "three-little-pigs")
            {
                throw new NullReferenceException("bug");
            }

            problems.Add("a gap");
            return 1;
        });

        Assert.Equal(2, result.BookVerdicts.Count);
        Assert.All(result.BookVerdicts, v => Assert.False(v.Complete));
        Assert.Equal(books[0].Info.Id, result.BookVerdicts[0].Id);
    }

    [Fact]
    public void ReadEntries_TakesIndexHashAndText_AndSkipsAnEntryWithoutAHashOrAClip()
    {
        string json = """{"version":3,"entries":[{"index":0,"hash":"ABC","clip":"a.m4a","text":"x"},{"index":1,"clip":"c.m4a"},{"index":2,"hash":"def"},{"index":3,"hash":"ghi","clip":""}]}""";

        ManifestEntry only = Assert.Single(AudioAlignment.ReadEntries(json));
        Assert.Equal(new ManifestEntry(0, "abc", "x", "a.m4a"), only);
    }

    [Fact]
    public void AnEntryTheGeneratorCouldNotRender_LeavesItsPositionWithoutAnEntry()
    {
        string json = $$"""{"version":3,"entries":[{"index":0,"hash":"{{PremadeAudio.HashOf("a")}}","clip":"a.m4a"},{"index":1,"hash":"{{PremadeAudio.HashOf("b")}}"}]}""";

        IReadOnlyList<string> gaps = AudioCoverage.Gaps(AudioAlignment.ReadEntries(json), [Piece(0, "a"), Piece(1, "b")], Present);

        Assert.Contains("position 1 \"b\"", Assert.Single(gaps));
    }

    [Theory]
    [InlineData("""{"version":2,"language":"fa","entries":[]}""")]
    [InlineData("""{"version":3,"entries":[]}""")]
    [InlineData("""{"version":3,"language":"fa","entries":{}}""")]
    [InlineData("""{"version":3,"language":"fa","entries":[{"index":0,"hash":"abc","clip":"../x.m4a"}]}""")]
    [InlineData("not json")]
    public void AManifestTheAppRejects_IsAProblem(string json)
    {
        Assert.Contains("the app rejects the manifest", AudioAlignment.ParseProblem(json, "fa"));
    }

    [Fact]
    public void AManifestTheAppAccepts_HasNoParseProblem()
    {
        Assert.Null(AudioAlignment.ParseProblem("""{"version":3,"language":"fa","entries":[{"index":0,"hash":"abc","clip":"a.m4a"}]}""", "fa"));
    }

    [Fact]
    public void AToolBugInOneBook_IsAToolError_AndTheOtherBooksAreStillChecked()
    {
        BookFolder[] books = [Books.Folder("three-little-pigs"), Books.Folder("lion-and-mouse")];

        AudioCheckResult result = AudioAlignment.CheckBooks(books, (book, problems) =>
        {
            if (book.Stem == "three-little-pigs")
            {
                throw new NullReferenceException("bug");
            }

            problems.Add("lion-and-mouse fa: a manifest problem");
            return 1;
        });

        Assert.Single(result.ToolErrors);
        Assert.Contains("three-little-pigs", result.ToolErrors[0]);
        Assert.Single(result.Problems);
        Assert.Equal(1, result.Checked);
        Assert.Equal(2, AudioAlignment.ExitCodeOf(result));
    }

    [Fact]
    public void ExitCodes_SeparateManifestProblemsFromSuccess()
    {
        Assert.Equal(0, AudioAlignment.ExitCodeOf(new AudioCheckResult(3, [], [])));
        Assert.Equal(1, AudioAlignment.ExitCodeOf(new AudioCheckResult(3, ["x"], [])));
    }

    [Fact]
    public void ZeroManifestsChecked_IsAFailure_NotAllLineUp()
    {
        Assert.Equal(1, AudioAlignment.ExitCodeOf(new AudioCheckResult(0, [], [])));
    }

    [Fact]
    public void ThePieceCountIsTheCoreRule_TheAppAndTheCheckShare()
    {
        string[] texts = ["Hello.", "...", "!", "Ali 2", ""];

        Assert.Equal(2, PremadeAudio.SpokenCount(texts));
        Assert.False(PremadeAudio.IsSpoken("..."));
    }

    [Fact]
    public void TheDumpOfARealBook_ListsOnlySpokenPieces_NumberedInOrder()
    {
        SentenceListing listing = SentenceDump.Build("three-little-pigs", SentenceDump.StoryOf("three-little-pigs"), "fa");

        Assert.All(listing.Sentences, s => Assert.True(PremadeAudio.IsSpoken(s.Text)));
        Assert.Equal(listing.Sentences.Count, PremadeAudio.SpokenCount(listing.Sentences.Select(s => s.Text)));
        Assert.Equal(Enumerable.Range(0, listing.Sentences.Count), listing.Sentences.Select(s => s.Index));
    }
}
