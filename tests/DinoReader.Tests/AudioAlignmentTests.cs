using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary><c>check-audio</c> (Slice 272): a manifest must hash the app's piece at every position, with no entry left over.</summary>
public sealed class AudioAlignmentTests
{
    private static SpokenSentence Piece(int index, string text) => new(index, index, text, text, null, true);

    private static ManifestEntry Entry(int index, string text) => new(index, PremadeAudio.HashOf(text), text);

    [Fact]
    public void AManifestThatHashesEveryPiece_HasNoProblem()
    {
        SpokenSentence[] dump = [Piece(0, "a"), Piece(1, "b")];

        Assert.Null(AudioAlignment.FirstProblem([Entry(0, "a"), Entry(1, "b")], dump));
    }

    [Fact]
    public void ASoundWordEntry_ShiftsEveryLaterPosition_AndTheFirstBadOneIsNamed()
    {
        SpokenSentence[] dump = [Piece(0, "a"), Piece(1, "b"), Piece(2, "c")];
        ManifestEntry[] manifest = [Entry(0, "a"), Entry(1, "roar!"), Entry(2, "b"), Entry(3, "c")];

        string? problem = AudioAlignment.FirstProblem(manifest, dump);

        Assert.NotNull(problem);
        Assert.Contains("first bad position 1", problem);
        Assert.Contains("roar!", problem);
        Assert.Contains("4 manifest entries, 3 pieces read", problem);
    }

    [Fact]
    public void AMissingEntry_IsNamedAtItsPosition()
    {
        string? problem = AudioAlignment.FirstProblem([Entry(0, "a")], [Piece(0, "a"), Piece(1, "b")]);

        Assert.Contains("first bad position 1: the manifest has no entry", problem);
    }

    [Fact]
    public void AnEntryPastTheLastPiece_IsAProblemEvenWhenEveryPieceMatches()
    {
        string? problem = AudioAlignment.FirstProblem([Entry(0, "a"), Entry(1, "extra")], [Piece(0, "a")]);

        Assert.Contains("entries past the last piece", problem);
    }

    [Fact]
    public void ReadEntries_TakesIndexHashAndText_AndSkipsAnEntryWithoutAHashOrAClip()
    {
        string json = """{"version":3,"entries":[{"index":0,"hash":"ABC","clip":"a.m4a","text":"x"},{"index":1,"clip":"c.m4a"},{"index":2,"hash":"def"},{"index":3,"hash":"ghi","clip":""}]}""";

        ManifestEntry only = Assert.Single(AudioAlignment.ReadEntries(json));
        Assert.Equal(new ManifestEntry(0, "abc", "x"), only);
    }

    [Fact]
    public void AnEntryTheGeneratorCouldNotRender_LeavesItsPositionWithoutAnEntry()
    {
        string json = $$"""{"version":3,"entries":[{"index":0,"hash":"{{PremadeAudio.HashOf("a")}}","clip":"a.m4a"},{"index":1,"hash":"{{PremadeAudio.HashOf("b")}}"}]}""";

        string? problem = AudioAlignment.FirstProblem(AudioAlignment.ReadEntries(json), [Piece(0, "a"), Piece(1, "b")]);

        Assert.Contains("first bad position 1: the manifest has no entry", problem);
    }

    [Theory]
    [InlineData("""{"version":2,"language":"fa","entries":[]}""")]
    [InlineData("""{"version":3,"entries":[]}""")]
    [InlineData("""{"version":3,"language":"fa","entries":{}}""")]
    [InlineData("""{"version":3,"language":"fa","entries":[{"index":0,"hash":"abc","clip":"../x.m4a"}]}""")]
    [InlineData("not json")]
    public void AManifestTheAppRejects_IsAProblem(string json)
    {
        Assert.Contains("the app rejects the manifest", AudioAlignment.ParseProblem(json));
    }

    [Fact]
    public void AManifestTheAppAccepts_HasNoParseProblem()
    {
        Assert.Null(AudioAlignment.ParseProblem("""{"version":3,"language":"fa","entries":[{"index":0,"hash":"abc","clip":"a.m4a"}]}"""));
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
