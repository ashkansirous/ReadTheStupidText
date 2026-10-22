using DinoReader.Core;

namespace DinoReader.Tests;

public class GateChallengeTests
{
    [Fact]
    public void Operands_StayWithinBounds()
    {
        var rng = new Random(1);
        for (int i = 0; i < 500; i++)
        {
            var gate = new GateChallenge(rng);
            Assert.InRange(gate.Left, GateChallenge.MinOperand, GateChallenge.MaxOperand);
            Assert.InRange(gate.Right, GateChallenge.MinOperand, GateChallenge.MaxOperand);
            gate.Submit(-1);
        }
    }

    [Fact]
    public void BoundsAreSixToNineteen()
    {
        Assert.Equal(6, GateChallenge.MinOperand);
        Assert.Equal(19, GateChallenge.MaxOperand);
    }

    [Fact]
    public void SameSeed_GivesTheSameSum()
    {
        var a = new GateChallenge(new Random(7));
        var b = new GateChallenge(new Random(7));
        Assert.Equal(a.Answer, b.Answer);
    }

    [Fact]
    public void CorrectAnswer_Passes()
    {
        var gate = new GateChallenge(new Random(3));
        Assert.Equal(GateOutcome.Passed, gate.Submit(gate.Left + gate.Right));
    }

    [Fact]
    public void WrongAnswer_RerollsTheSum()
    {
        var gate = new GateChallenge(new Random(3));
        int firstLeft = gate.Left;
        int firstRight = gate.Right;
        var seen = new HashSet<(int, int)> { (firstLeft, firstRight) };
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(GateOutcome.WrongRerolled, gate.Submit(gate.Answer + 1));
            seen.Add((gate.Left, gate.Right));
        }

        Assert.True(seen.Count > 1);
    }
}

public class HoldTrackerTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void NotHolding_HasNoProgress()
    {
        var hold = new HoldTracker(HoldTracker.GateHold);
        Assert.Equal(0, hold.Progress(Second));
        Assert.False(hold.IsComplete(Second * 10));
    }

    [Fact]
    public void Holding_ProgressRisesAndCompletesAtThreeSeconds()
    {
        var hold = new HoldTracker(HoldTracker.GateHold);
        hold.Start(Second);
        Assert.Equal(0.5, hold.Progress(Second + TimeSpan.FromSeconds(1.5)), 3);
        Assert.False(hold.IsComplete(Second + TimeSpan.FromSeconds(2.9)));
        Assert.True(hold.IsComplete(Second + TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Releasing_ResetsTheHold()
    {
        var hold = new HoldTracker(HoldTracker.GateHold);
        hold.Start(TimeSpan.Zero);
        hold.Cancel();
        Assert.False(hold.IsComplete(Second * 5));
        Assert.Equal(0, hold.Progress(Second * 5));
    }
}

public class StoryJsonTests
{
    internal static Story Sample() => new(
        StoryIds.New(), "The Little Lighthouse", "en", new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
        [new StoryPage("Once upon a time."), new StoryPage("The end.", "page-02.jpg")],
        [new CastMember("peter", "Peter", Gender.Male, "M1"), new CastMember("mum", "Mum", Gender.Female, "F2")],
        CoverStyle.Colour, 2, "F1");

    [Fact]
    public void RoundTrip_KeepsEverything()
    {
        Story story = Sample();
        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));
        Assert.Equal(story.Id, back.Id);
        Assert.Equal(story.Title, back.Title);
        Assert.Equal(story.Created, back.Created);
        Assert.Equal(story.CoverStyle, back.CoverStyle);
        Assert.Equal(story.CoverColourIndex, back.CoverColourIndex);
        Assert.Equal(story.NarratorVoiceId, back.NarratorVoiceId);
        Assert.Equal(story.Pages, back.Pages);
        Assert.Equal(story.Cast, back.Cast);
    }

    [Fact]
    public void Serialized_CarriesTheSchemaVersion()
    {
        Assert.Contains($"\"Version\": {StoryJson.CurrentVersion}", StoryJson.Serialize(Sample()));
    }

    [Fact]
    public void UnknownFields_AreIgnored()
    {
        string json = StoryJson.Serialize(Sample()).Replace("\"Title\"", "\"FutureField\": {\"a\": [1, 2]}, \"Title\"");
        Assert.Equal("The Little Lighthouse", StoryJson.Deserialize(json).Title);
    }

    [Fact]
    public void MissingOptionalFields_GetDefaults()
    {
        Story story = StoryJson.Deserialize($"{{\"Version\":1,\"Id\":\"{StoryIds.New()}\",\"Title\":\"T\"}}");
        Assert.Empty(story.Pages);
        Assert.Empty(story.Cast);
        Assert.Equal("en", story.Language);
    }

    [Fact]
    public void NewerVersion_IsRefused()
    {
        Assert.Throws<StoryFormatException>(() =>
            StoryJson.Deserialize($"{{\"Version\":99,\"Id\":\"{StoryIds.New()}\",\"Title\":\"T\"}}"));
    }

    [Fact]
    public void Garbage_IsRefused()
    {
        Assert.Throws<StoryFormatException>(() => StoryJson.Deserialize("{not json"));
    }

    [Theory]
    [InlineData("\"Pages\":null")]
    [InlineData("\"Cast\":null")]
    [InlineData("\"Title\":null")]
    [InlineData("\"Pages\":[null]")]
    [InlineData("\"Pages\":[{\"Text\":null}]")]
    [InlineData("\"Cast\":[null]")]
    public void NullFields_AreAFormatErrorNotACrash(string field)
    {
        string json = $"{{\"Version\":1,\"Id\":\"{StoryIds.New()}\",{field}}}";

        Assert.Throws<StoryFormatException>(() => StoryJson.Deserialize(json));
    }
}

public sealed class FileStoryLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-stories-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Make(string title, int day) => new(
        StoryIds.New(), title, "en", new DateTimeOffset(2026, 10, day, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("Text.")], [], CoverStyle.Colour, 0, "F1");

    [Fact]
    public async Task Added_SurvivesANewLibraryInstance()
    {
        Story story = Make("Moon Soup", 1);
        await new FileStoryLibrary(_root).AddAsync(story);

        Story? back = await new FileStoryLibrary(_root).GetAsync(story.Id);

        Assert.Equal("Moon Soup", back?.Title);
    }

    [Fact]
    public async Task List_IsNewestFirst_AndSkipsUnreadableFolders()
    {
        var library = new FileStoryLibrary(_root);
        await library.AddAsync(Make("Old", 1));
        await library.AddAsync(Make("New", 2));
        string broken = library.FolderOf(StoryIds.New());
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "story.json"), "{nope", TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_root, "not-an-id"));

        IReadOnlyList<Story> stories = await library.ListAsync();

        Assert.Equal(["New", "Old"], stories.Select(s => s.Title));
    }

    [Fact]
    public async Task Update_ReplacesTheStory_AndLeavesNoTempFile()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make("Before", 1);
        await library.AddAsync(story);

        await library.UpdateAsync(story with { Title = "After" });

        Assert.Equal("After", (await library.GetAsync(story.Id))!.Title);
        Assert.Empty(Directory.GetFiles(library.FolderOf(story.Id), "*.tmp"));
    }

    [Fact]
    public async Task Add_RejectsADuplicate_AndUpdateRejectsAnUnknownStory()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make("Once", 1);
        await library.AddAsync(story);

        await Assert.ThrowsAsync<InvalidOperationException>(() => library.AddAsync(story));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.UpdateAsync(Make("Never saved", 2)));
    }

    [Fact]
    public async Task Delete_RemovesTheWholeStoryFolder()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make("Gone", 1);
        await library.AddAsync(story);
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(story.Id), "page-01.jpg"), "x", TestContext.Current.CancellationToken);

        await library.DeleteAsync(story.Id);

        Assert.False(Directory.Exists(library.FolderOf(story.Id)));
        Assert.Null(await library.GetAsync(story.Id));
    }

    [Fact]
    public async Task NonIdPaths_AreNeverTouched()
    {
        var library = new FileStoryLibrary(_root);
        Directory.CreateDirectory(_root);

        await library.DeleteAsync("..");

        Assert.True(Directory.Exists(_root));
        Assert.Null(await library.GetAsync("../x"));
    }
}

public class StoryCastTests
{
    private static readonly Character Peter = new("peter", "Peter", Gender.Male, 10);
    private static readonly Character Mum = new("mum", "Mum", Gender.Female, 5);

    [Fact]
    public void ToMembers_CopiesNameGenderAndVoice()
    {
        var cast = new VoiceCast("F1", [new VoiceAssignment("peter", "M1", false), new VoiceAssignment("mum", "F2", false)]);

        IReadOnlyList<CastMember> members = StoryCast.ToMembers([Peter, Mum], cast);

        Assert.Equal([new CastMember("peter", "Peter", Gender.Male, "M1"), new CastMember("mum", "Mum", Gender.Female, "F2")], members);
    }

    private static VoiceCast Recast(IReadOnlyList<Character> characters, string narrator, params CastMember[] stored)
    {
        VoiceOption[] voices =
        [
            new("M1", Gender.Male), new("M2", Gender.Male), new("M3", Gender.Male),
            new("F1", Gender.Female), new("F2", Gender.Female),
        ];
        return new VoiceCaster().Cast(characters, voices, narrator, StoryCast.Pinned(stored));
    }

    [Fact]
    public void Pinned_KeepsStoredVoices()
    {
        var peter = new Character("peter", "Peter", Gender.Male, 10);
        var cast = Recast([peter], "F1", new CastMember("peter", "Peter", Gender.Male, "M3"));

        Assert.Equal("M3", cast.VoiceFor("peter"));
    }

    [Fact]
    public void Pinned_DropsAStoredVoiceThatIsNowTheNarrators_WithoutACollision()
    {
        var characters = new[]
        {
            new Character("dad", "Dad", Gender.Male, 20),
            new Character("peter", "Peter", Gender.Male, 10),
        };

        var cast = Recast(
            characters, "M3",
            new CastMember("peter", "Peter", Gender.Male, "M2"),
            new CastMember("dad", "Dad", Gender.Male, "M3"));

        Assert.Equal("M2", cast.VoiceFor("peter"));
        Assert.NotEqual("M2", cast.VoiceFor("dad"));
        Assert.NotEqual("M3", cast.VoiceFor("dad"));
        Assert.All(cast.Assignments, a => Assert.False(a.IsShared));
    }

    [Fact]
    public void Pinned_VoiceIsNotTakenByANewCharacterWhileOthersAreFree()
    {
        var characters = new[]
        {
            new Character("new", "New", Gender.Male, 50),
            new Character("peter", "Peter", Gender.Male, 10),
        };

        var cast = Recast(characters, "F1", new CastMember("peter", "Peter", Gender.Male, "M1"));

        Assert.Equal("M1", cast.VoiceFor("peter"));
        Assert.NotEqual("M1", cast.VoiceFor("new"));
        Assert.All(cast.Assignments, a => Assert.False(a.IsShared));
    }

    [Fact]
    public void NoPins_CastsLikeBefore()
    {
        var cast = Recast([new Character("mum", "Mum", Gender.Female, 5)], "M1");

        Assert.Equal("F1", cast.VoiceFor("mum"));
    }
}

public class StoryHelpersTests
{
    [Fact]
    public void Palette_WrapsAndHandlesNegativeIndexes()
    {
        Assert.Equal(CoverPalette.Colours[0], CoverPalette.ColourAt(4));
        Assert.Equal(CoverPalette.Colours[3], CoverPalette.ColourAt(-1));
    }

    [Fact]
    public void TitleFromText_UsesTheFirstLine_CutAtAWord()
    {
        Assert.Equal("Moon Soup", StoryTitles.FromText("\n  Moon Soup \nOnce there was"));
        string cut = StoryTitles.FromText("A very long first line that keeps going on and on and on forever");
        Assert.True(cut.Length <= 40);
        Assert.StartsWith("A very long first line", cut);
        Assert.Equal(string.Empty, StoryTitles.FromText("  \n "));
    }

    [Fact]
    public void CoverImageFile_ResolvesPicturePageAndPhoto_AndIgnoresABadIndex()
    {
        var story = new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("One.", "p1.jpg"), new StoryPage("Two.", "p2.jpg")], [], CoverStyle.Picture, 0, "F1");

        Assert.Equal("p2.jpg", (story with { CoverPageIndex = 1 }).CoverImageFile);
        Assert.Equal("cover.jpg", (story with { CoverFile = "cover.jpg" }).CoverImageFile);
        Assert.Null((story with { CoverPageIndex = 5 }).CoverImageFile);
        Assert.Null((story with { CoverStyle = CoverStyle.Colour, CoverFile = "cover.jpg" }).CoverImageFile);
    }

    [Theory]
    [InlineData(CoverStyle.FirstPage, null, null)]
    [InlineData(CoverStyle.Colour, null, null)]
    [InlineData(CoverStyle.Picture, "cover.jpg", null)]
    [InlineData(CoverStyle.Picture, null, 1)]
    public void Json_RoundTripsEachCoverStyle(CoverStyle style, string? file, int? pageIndex)
    {
        Story story = StoryJsonTests.Sample() with { CoverStyle = style, CoverFile = file, CoverPageIndex = pageIndex };
        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));
        Assert.Equal(style, back.CoverStyle);
        Assert.Equal(file, back.CoverFile);
        Assert.Equal(pageIndex, back.CoverPageIndex);
    }

    [Fact]
    public void RemoveAt_KeepsTheCoverOnTheSamePicture_OrFallsBackToTheFirstPage()
    {
        Story story = new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("1", "a.jpg"), new StoryPage("2", "b.jpg"), new StoryPage("3", "c.jpg")], [], CoverStyle.Picture, 0, "F1", CoverPageIndex: 2);

        var before = new StoryDraft(story);
        before.RemoveAt(0);
        Assert.Equal(1, before.CoverPageIndex);
        Assert.Equal("c.jpg", before.Pages[before.CoverPageIndex!.Value].ImageFile);

        var after = new StoryDraft(story);
        after.RemoveAt(1);
        Assert.Equal(1, after.CoverPageIndex);

        var onCover = new StoryDraft(story);
        onCover.RemoveAt(2);
        Assert.Null(onCover.CoverPageIndex);
        Assert.Equal(CoverStyle.FirstPage, onCover.CoverStyle);
    }

    [Fact]
    public void ChoosingAColour_DropsThePictureState_SoPageEditsLeaveTheColourAlone()
    {
        Story story = new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("1", "a.jpg"), new StoryPage("2", "b.jpg"), new StoryPage("3", "c.jpg")], [], CoverStyle.Picture, 0, "F1", CoverPageIndex: 2);

        var draft = new StoryDraft(story);
        draft.PendingCoverPhoto = "x.jpg";
        draft.ChooseCover(CoverStyle.Colour);
        draft.RemoveAt(2);

        Assert.Equal(CoverStyle.Colour, draft.CoverStyle);
        Assert.Null(draft.CoverPageIndex);
        Assert.Null(draft.CoverFile);
        Assert.Null(draft.PendingCoverPhoto);
    }

    [Fact]
    public void Move_KeepsTheCoverOnTheSamePicture()
    {
        Story story = new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("1", "a.jpg"), new StoryPage("2", "b.jpg"), new StoryPage("3", "c.jpg")], [], CoverStyle.Picture, 0, "F1", CoverPageIndex: 1);

        var draft = new StoryDraft(story);
        draft.Move(1, 1);
        Assert.Equal(2, draft.CoverPageIndex);
        draft.Move(0, 1);
        Assert.Equal(2, draft.CoverPageIndex);
        draft.Move(2, -1);
        Assert.Equal(1, draft.CoverPageIndex);
    }

    [Fact]
    public void TextsForEditedText_KeepsEveryPage_WhenTheLinesAreNotFoundOrOnlySomeAre()
    {
        IReadOnlyList<IReadOnlyList<string>> lines = [["Page one line"], ["Page two line"], ["Page three line"]];

        IReadOnlyList<string> unrelated = ImportedText.TextsForEditedText("Totally rewritten.", lines);
        Assert.Equal(["Totally rewritten.", "", ""], unrelated);

        IReadOnlyList<string> partial = ImportedText.TextsForEditedText("Page one line then\nPage three line end", lines);
        Assert.Equal(3, partial.Count);
        Assert.Equal(string.Empty, partial[1]);
        Assert.StartsWith("Page one line", partial[0]);
        Assert.StartsWith("Page three line", partial[2]);
    }

    [Fact]
    public void Text_JoinsPagesWithABlankLine_AndCoverImageNeedsAFirstPageImage()
    {
        var story = new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("One.", "p1.jpg"), new StoryPage("Two.")], [], CoverStyle.FirstPage, 0, "F1");

        Assert.Equal("One.\n\nTwo.", story.Text);
        Assert.Equal("p1.jpg", story.CoverImageFile);
        Assert.Null((story with { CoverStyle = CoverStyle.Colour }).CoverImageFile);
        Assert.Null((story with { Pages = [new StoryPage("x")] }).CoverImageFile);
    }

    [Fact]
    public void StoryIds_AcceptOnlyGuids()
    {
        Assert.True(StoryIds.IsValid(StoryIds.New()));
        Assert.False(StoryIds.IsValid(".."));
        Assert.False(StoryIds.IsValid(null));
    }
}

public class StoryDraftTests
{
    private static StoryDraft Draft(params string[] pages) => new(new Story(
        StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, pages.Select(p => new StoryPage(p)).ToList(), [],
        CoverStyle.Colour, 0, "F1"));

    [Fact]
    public void Move_SwapsNeighbours_AndIgnoresTheEnds()
    {
        StoryDraft draft = Draft("a", "b", "c");

        draft.Move(0, -1);
        draft.Move(2, 1);
        Assert.Equal(["a", "b", "c"], draft.Pages.Select(p => p.Text));

        draft.Move(1, 1);
        Assert.Equal(["a", "c", "b"], draft.Pages.Select(p => p.Text));
    }

    [Fact]
    public void TextChanged_TracksEditsAndReorders()
    {
        StoryDraft draft = Draft("a", "b");
        Assert.False(draft.TextChanged);

        draft.Pages[0] = new StoryPage("a!");
        Assert.True(draft.TextChanged);
    }
}
