using DinoReader.Core;
using DinoReader.BookBuilder;
using ReadTheStupidText.Application.Reading;
using Xunit;

namespace DinoReader.Tests;

// Slice 255: the animals' real recordings replace the voiced sound word.
public class AnimalSoundClipTests
{
    private static string ClipFolder => Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "Resources", "Raw", "animal-sounds");

    private static VoicedSegment Speech(string text, int start) => new(text, "voice", start, start + text.Length, start);

    private static VoicedSegment Slot(AnimalKind kind, int at) => new("Oink!", "voice", at, at, at, new AnimalClipMarker(kind));

    [Fact]
    public void EveryAnimalButFour_HasAClip_AndTheFourPickedNoneHaveNoSound()
    {
        Assert.Equal(15, AnimalSoundClips.Kinds.Count);
        Assert.All(new[] { AnimalKind.Bear, AnimalKind.Lion, AnimalKind.Rabbit, AnimalKind.Wolf }, k => Assert.False(AnimalSoundClips.Has(k)));
    }

    [Fact]
    public void EachClip_IsBundledAsAFile_AndNothingElseIsInTheFolder()
    {
        string[] expected = AnimalSoundClips.Kinds.Select(AnimalSoundClips.FileNameOf).Order().ToArray();
        string[] found = Directory.GetFiles(ClipFolder).Select(Path.GetFileName).OfType<string>().Order().ToArray();

        Assert.Equal(expected, found);
        Assert.All(Directory.GetFiles(ClipFolder), f => Assert.InRange(new FileInfo(f).Length, 1_000, 60_000));
    }

    [Fact]
    public void EachClip_IsCreditedInStoreMd()
    {
        string store = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "STORE.md"));

        Assert.All(AnimalSoundClips.Kinds, k => Assert.Contains($"`{AnimalSoundClips.FileNameOf(k)}`", store));
    }

    [Fact]
    public void TheAssetNames_AreFlat_LikeTheStarterBooks()
    {
        Assert.Equal("animal-pig.m4a", AnimalSoundClips.AssetNameOf(AnimalKind.Pig));
        string csproj = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "DinoReader.App.csproj"));
        Assert.Contains("Resources\\Raw\\animal-sounds\\*.m4a\" LogicalName=\"animal-%(Filename)%(Extension)\"", csproj);
    }

    [Fact]
    public void NoSlot_IsOneRunAndNoClip()
    {
        VoicedSegment[] segments = [Speech("Hello.", 0), Speech("Bye.", 10)];

        AnimalClipRuns.Cut cut = AnimalClipRuns.Next(segments);

        Assert.Equal(segments, cut.Run);
        Assert.Null(cut.Clip);
        Assert.Empty(cut.Rest);
    }

    [Fact]
    public void ASlot_SplitsTheReadIntoWhatComesBefore_TheClip_AndWhatFollows()
    {
        VoicedSegment[] segments = [Speech("One.", 0), Slot(AnimalKind.Pig, 3), Speech("Two.", 10), Slot(AnimalKind.Cow, 13), Speech("Three.", 20)];

        AnimalClipRuns.Cut first = AnimalClipRuns.Next(segments);
        AnimalClipRuns.Cut second = AnimalClipRuns.Next(first.Rest);

        Assert.Equal(["One."], first.Run.Select(s => s.Text));
        Assert.Equal(AnimalKind.Pig, first.Clip);
        Assert.Equal(["Two.", "Oink!", "Three."], first.Rest.Select(s => s.Text));
        Assert.Equal(["Two."], second.Run.Select(s => s.Text));
        Assert.Equal(AnimalKind.Cow, second.Clip);
        Assert.Equal(["Three."], second.Rest.Select(s => s.Text));
    }

    [Fact]
    public void ALeadingAndATrailingSlot_LeaveAnEmptyRunAndAnEmptyRest()
    {
        AnimalClipRuns.Cut leading = AnimalClipRuns.Next([Slot(AnimalKind.Duck, 0), Speech("A.", 5)]);
        AnimalClipRuns.Cut trailing = AnimalClipRuns.Next([Speech("A.", 0), Slot(AnimalKind.Duck, 1)]);

        Assert.Empty(leading.Run);
        Assert.Single(leading.Rest);
        Assert.Single(trailing.Run);
        Assert.Empty(trailing.Rest);
    }

    [Fact]
    public void Steps_CoverConsecutiveSlots_AMissingTail_AndASlotThatEndsTheRead()
    {
        VoicedSegment[] segments = [Speech("A.", 0), Slot(AnimalKind.Pig, 1), Slot(AnimalKind.Hen, 2), Speech("B.", 5), Slot(AnimalKind.Cow, 9)];

        var steps = AnimalClipRuns.Steps(segments);

        Assert.Equal([AnimalKind.Pig, AnimalKind.Hen, AnimalKind.Cow], steps.Select(s => s.Clip));
        Assert.Equal([1, 0, 1], steps.Select(s => s.Run.Count));
        Assert.Empty(AnimalClipRuns.Steps([]));
        Assert.Equal([(AnimalKind?)null], AnimalClipRuns.Steps([Speech("A.", 0)]).Select(s => s.Clip));
    }

    [Fact]
    public void WithClip_KeepsOnlyAnimalsWithARecording_InAnyLanguage()
    {
        var animals = new Dictionary<string, AnimalKind> { ["p"] = AnimalKind.Pig, ["w"] = AnimalKind.Wolf };

        Assert.Equal(["p"], AnimalSoundClips.WithClip(animals).Keys);
    }

    [Fact]
    public void TheSession_ChecksTheReadIsStillCurrent_AfterEveryAwait_AndDiscardsAClipThatFailed()
    {
        string session = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "ReadingSession.cs"));

        Assert.Contains("cancellation.ThrowIfCancellationRequested(); // a Stop, Back", session);
        Assert.Contains("read.Cancellation.ThrowIfCancellationRequested()", session);
        Assert.Contains("AnimalClipFiles.Discard(clip)", session);
    }

    [Fact]
    public void Plan_IsAFlatListOfSpeakAndPlaySteps_NeverEmpty()
    {
        VoicedSegment[] segments = [Speech("A.", 0), Slot(AnimalKind.Pig, 1), Slot(AnimalKind.Hen, 2), Speech("B.", 5), Slot(AnimalKind.Cow, 9)];

        var plan = AnimalClipRuns.Plan(segments);

        Assert.Equal([null, AnimalKind.Pig, AnimalKind.Hen, null, AnimalKind.Cow], plan.Select(s => s.Clip));
        Assert.Equal([1, 0, 0, 1, 0], plan.Select(s => s.Run.Count));
        Assert.Single(AnimalClipRuns.Plan([]));
        Assert.Single(AnimalClipRuns.Plan([Speech("A.", 0)]));
    }

    [Theory]
    [InlineData(false, false, false, AnimalClipRuns.Advance.Start)]
    [InlineData(false, false, true, AnimalClipRuns.Advance.Defer)]
    [InlineData(true, false, false, AnimalClipRuns.Advance.Ignore)]
    [InlineData(false, true, false, AnimalClipRuns.Advance.Ignore)]
    [InlineData(true, true, true, AnimalClipRuns.Advance.Ignore)]
    [InlineData(false, true, true, AnimalClipRuns.Advance.Ignore)]
    public void Decide_DropsAStaleOrCancelledRead_WaitsWhenPaused_ElseStarts(bool stale, bool cancelled, bool paused, AnimalClipRuns.Advance expected) =>
        Assert.Equal(expected, AnimalClipRuns.Decide(stale, cancelled, paused));

    [Theory]
    [InlineData(5, 5, 3, 3, 3, true)] // normal: current run, seen Playing
    [InlineData(5, 5, 3, 3, 0, false)] // late Completed before this run played (or a duplicate: playing run cleared)
    [InlineData(5, 5, 2, 3, 3, false)] // stamped by an earlier run
    [InlineData(5, 5, 4, 4, 3, false)] // stopped/new run not yet playing: playing run is older
    [InlineData(5, 0, 3, 3, 3, false)] // read stopped
    [InlineData(5, 6, 3, 3, 3, false)] // read replaced
    [InlineData(0, 0, 3, 3, 3, false)]
    public void AcceptCompleted_OnlyForTheCurrentRunSeenPlaying(int readId, int activeId, int stamped, int current, int playing, bool expected) =>
        Assert.Equal(expected, AnimalClipRuns.AcceptCompleted(readId, activeId, stamped, current, playing));

    [Fact]
    public void EveryEndSignalOfAStep_GoesThroughAdvanceStep()
    {
        string session = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "ReadingSession.cs"));

        Assert.Contains("AdvanceStep(id); // the run has been spoken", session);
        Assert.Contains("AdvanceStep(read.ReadId);", session);
        Assert.Contains("MainThread.BeginInvokeOnMainThread(() => AdvanceIfCurrent(read, index))", session);
        Assert.Contains("_pendingAdvance = true;", session);
        Assert.DoesNotContain("ContinueAfterClip", session);
    }

    // Slice 255 moved the slot filter from "has a sound word in the story's language" to "has a recording". The plan counts each character's own lines, so a character's
    // slot positions do not depend on which other characters have slots: the highlight and the parent recordings, which key on story positions, see the same positions.
    [Fact]
    public void AnotherCharactersSlot_NeverMovesTheSlotsOfAnAnimalThatKeepsOne()
    {
        var pieces = new List<SplitSegment>();
        for (int i = 0; i < 9; i++)
        {
            pieces.Add(new SplitSegment(new Segment(SegmentKind.Quote, "Good morning.", i * 100, i * 100 + 13, "Sheep"), i * 100));
            pieces.Add(new SplitSegment(new Segment(SegmentKind.Quote, "Oh my.", i * 100 + 50, i * 100 + 56, "Pig"), i * 100 + 50));
        }

        var sheepOnly = new Dictionary<string, AnimalKind> { ["Sheep"] = AnimalKind.Sheep };
        var both = new Dictionary<string, AnimalKind> { ["Sheep"] = AnimalKind.Sheep, ["Pig"] = AnimalKind.Pig };

        var before = AnimalSounds.Plan(pieces, sheepOnly, "en").OrderBy(p => p.Key).ToList();
        var after = AnimalSounds.Plan(pieces, both, "en").Where(p => p.Value == AnimalKind.Sheep).OrderBy(p => p.Key).ToList();

        Assert.NotEmpty(before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void TheSlotFilterChange_OnlyAddsAnimalsWithARecording_AndOnlyDropsTheFourWithout()
    {
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            string code = AppLanguages.Code(language);
            var all = Enum.GetValues<AnimalKind>().ToDictionary(k => k.ToString(), k => k);
            var before = AnimalSounds.WithSound(all, code).Keys.ToHashSet();
            var after = AnimalSoundClips.WithClip(all).Keys.ToHashSet();

            Assert.All(before.Except(after), k => Assert.False(AnimalSoundClips.Has(all[k])));
            Assert.All(after.Except(before), k => Assert.Null(AnimalSounds.SoundOf(all[k], code)));
        }
    }

    [Fact]
    public void KindsIn_ListsEachAnimalOnce()
    {
        VoicedSegment[] segments = [Speech("A.", 0), Slot(AnimalKind.Pig, 1), Slot(AnimalKind.Pig, 5), Slot(AnimalKind.Hen, 9)];

        Assert.Equal([AnimalKind.Pig, AnimalKind.Hen], AnimalClipRuns.KindsIn(segments));
    }

    [Fact]
    public void TheMarker_IsAnInertTransform_ThatNeverReachesAnEngine()
    {
        var marker = new AnimalClipMarker(AnimalKind.Sheep);
        float[] samples = [0.1f, 0.2f];

        Assert.Same(samples, marker.Apply(samples, 22050));
        Assert.Equal("clip-Sheep", marker.CacheTag);
    }

    [Fact]
    public void TheVoicing_MarksTheSlot_NotAFlavouredVoice_AndSkipsAnimalsWithoutAClip()
    {
        string voicing = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "StoryVoicing.cs"));

        Assert.Contains("new AnimalClipMarker(placement.Kind)", voicing);
        Assert.Contains("!AnimalSoundClips.Has(placement.Kind)", voicing);
    }

    [Fact]
    public void TheSession_SpeaksOnlyUpToASlot_ThenPlaysTheClip()
    {
        string session = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "ReadingSession.cs"));

        Assert.Contains("AnimalClipRuns.Plan(segments)", session);
        Assert.Contains("PlayClip(read, clip);", session);
        Assert.Contains("[Reading] animal clip {clip} plays", session);
    }
}
