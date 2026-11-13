using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

// Slice 246 C1/C2: the manifest lookup, its hash, and the read plan that cuts pieces with a clip out of what the device voice is given.
public class PremadeAudioTests
{
    // Computed with: python -I -c "import sys; sys.path.insert(0, 'tools/premade-audio'); import clip_manifest as m; print(m.text_hash(<text>))"
    private const string GondeyeText = "گنده‌ی خاکستری";
    private const string GondeyeHash = "bdaf0f9868cf009458bca198d54b2f5498633184978527ee9f6dae8f244409a6";
    private const string SplashText = "شالاپ، شالاپ، شالاپ!";
    private const string SplashHash = "65b3e84e6c199a3fbff6ae8974d1e2980938513794036249b8d3ee9bfbeea7ed";
    private const string DecomposedAlefHamza = "أ";
    private const string DecomposedAlefHamzaHash = "dd0eaa23a748ac491594f5aa50d3d09a236ef30040bdcbdf963f3e73190f1761";

    private static string Manifest(string entries, int version = 3, string language = "fa") =>
        $$"""{"version":{{version}},"book":"b","language":"{{language}}","entries":[{{entries}}]}""";

    private static string Entry(string hash, string clip, int index = 0) => $$"""{"index":{{index}},"sentence":0,"hash":"{{hash}}","voice":"Hali","direction":"","variant":"v","clip":"{{clip}}","text":"t"}""";

    [Fact]
    public void Has_IsTrueOnlyForAPositionWithAClip_EvenWhenTheManifestHasAHole()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(GondeyeHash, "a.m4a", 0) + "," + Entry(SplashHash, "c.m4a", 2) + "," + Entry(SplashHash, "d.m4a", 3)));

        Assert.True(audio.Has(0));
        Assert.False(audio.Has(1)); // the hole is below Count (3) yet has no clip
        Assert.True(audio.Has(3)); // and the last position is at or past Count
        Assert.False(audio.Has(4));
    }

    [Fact]
    public void HashOf_MatchesTheGeneratorsOwnHash_ForFixedFarsiStrings()
    {
        Assert.Equal(GondeyeHash, PremadeAudio.HashOf(GondeyeText));
        Assert.Equal(SplashHash, PremadeAudio.HashOf(SplashText));
    }

    [Fact]
    public void HashOf_NormalisesToNfcFirst_SoADecomposedAndAComposedFormShareOneHash()
    {
        Assert.Equal(DecomposedAlefHamzaHash, PremadeAudio.HashOf(DecomposedAlefHamza));
        Assert.Equal(DecomposedAlefHamzaHash, PremadeAudio.HashOf("أ"));
    }

    [Fact]
    public void HashOf_IsLowerCaseHex_OfSixtyFourCharacters()
    {
        string hash = PremadeAudio.HashOf("x");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void ClipAt_FindsTheClipOfThePieceAtThatPosition_AndNullForAnyOther()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(GondeyeHash, "a.m4a", 0) + "," + Entry(SplashHash, "b.m4a", 1)));

        Assert.Equal("a.m4a", audio.ClipAt(0, GondeyeText));
        Assert.Equal("b.m4a", audio.ClipAt(1, SplashText));
        Assert.Null(audio.ClipAt(1, SplashText + " edited"));
        Assert.Null(audio.ClipAt(2, SplashText));
        Assert.Equal(2, audio.Count);
        Assert.Equal("fa", audio.Language);
    }

    [Fact]
    public void ClipAt_AnEditedSentenceMissesSoItFallsBackToTheDeviceVoice()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(GondeyeHash, "a.m4a")));

        Assert.Null(audio.ClipAt(0, GondeyeText.Replace("خاکستری", "سیاه")));
    }

    [Fact]
    public void ClipAt_TheSameLineInTwoVoices_PlaysEachVoicesOwnClip()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(SplashHash, "pig.m4a", 3) + "," + Entry(GondeyeHash, "x.m4a", 4) + "," + Entry(SplashHash, "wolf.m4a", 5)));

        Assert.Equal("pig.m4a", audio.ClipAt(3, SplashText));
        Assert.Equal("wolf.m4a", audio.ClipAt(5, SplashText));
        Assert.Null(audio.ClipAt(4, SplashText)); // the position holds another text: the hash does not confirm it
    }

    [Fact]
    public void Parse_AcceptsAnUpperCaseHash_AndSkipsAnEntryWithNoClip()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(GondeyeHash.ToUpperInvariant(), "a.m4a", 0) + "," + Entry(SplashHash, "", 1)));

        Assert.Equal("a.m4a", audio.ClipAt(0, GondeyeText));
        Assert.Null(audio.ClipAt(1, SplashText));
    }

    [Fact]
    public void Parse_ASecondEntryAtTheSameIndex_KeepsTheFirstClip()
    {
        PremadeAudio audio = PremadeAudio.Parse(Manifest(Entry(GondeyeHash, "first.m4a") + "," + Entry(GondeyeHash, "second.m4a")));

        Assert.Equal("first.m4a", audio.ClipAt(0, GondeyeText));
        Assert.Equal(["first.m4a"], audio.ClipNames);
    }

    [Theory]
    [InlineData("{\"version\":2,\"language\":\"fa\",\"entries\":[]}")]
    [InlineData("{\"version\":3,\"entries\":[]}")]
    [InlineData("{\"version\":3,\"language\":\"fa\"}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void Parse_RejectsAnythingButAVersionThreeManifest(string json)
    {
        Assert.Throws<PremadeAudioException>(() => PremadeAudio.Parse(json));
    }

    [Theory]
    [InlineData("../x.m4a")]
    [InlineData("sub/x.m4a")]
    [InlineData("sub\\x.m4a")]
    [InlineData("C:x.m4a")]
    [InlineData("..")]
    public void Parse_RejectsAClipOutsideThePacksFolder(string clip)
    {
        Assert.Throws<PremadeAudioException>(() => PremadeAudio.Parse(Manifest(Entry(GondeyeHash, clip.Replace("\\", "\\\\")))));
    }

    private static VoicedSegment Speech(string text, int start) => new(text, "voice", start, start + text.Length, start);

    private static VoicedSegment Slot(int at) => new("Oink!", "voice", at, at, at, new AnimalClipMarker(AnimalKind.Pig));

    private static VoicedSegment Clip(string text, int start) => PremadeClipMarker.Mark(Speech(text, start), "/p/" + start + ".m4a");

    [Fact]
    public void Plan_NoClips_IsExactlyTheAnimalPlan()
    {
        VoicedSegment[] segments = [Speech("a", 0), Slot(1), Speech("b", 2)];

        IReadOnlyList<PremadeAudioPlan.Step> plan = PremadeAudioPlan.Plan(segments);

        Assert.Equal(AnimalClipRuns.Plan(segments).Select(s => (s.Run.Count, s.Clip)), plan.Select(s => (s.Run.Count, s.Animal)));
        Assert.All(plan, s => Assert.Null(s.Premade));
    }

    [Fact]
    public void Plan_CutsEachClipOutOfTheRuns_SoTheEngineNeverGetsAPieceWithAClip()
    {
        VoicedSegment[] segments = [Speech("a", 0), Clip("b", 2), Clip("c", 4), Speech("d", 6), Speech("e", 8)];

        IReadOnlyList<PremadeAudioPlan.Step> plan = PremadeAudioPlan.Plan(segments);

        Assert.Equal(4, plan.Count);
        Assert.Equal(["a"], plan[0].Run.Select(s => s.Text));
        Assert.Equal("b", plan[1].Premade?.Text);
        Assert.Equal("c", plan[2].Premade?.Text);
        Assert.Equal(["d", "e"], plan[3].Run.Select(s => s.Text));
        Assert.All(plan.Where(s => s.Premade is not null), s => Assert.Empty(s.Run));
    }

    [Fact]
    public void Plan_AnAnimalSlotBesideAClip_StaysItsOwnStep_InOrder()
    {
        VoicedSegment[] segments = [Clip("quote", 0), Slot(5), Speech("next", 6)];

        IReadOnlyList<PremadeAudioPlan.Step> plan = PremadeAudioPlan.Plan(segments);

        Assert.Equal(3, plan.Count);
        Assert.NotNull(plan[0].Premade);
        Assert.Equal(AnimalKind.Pig, plan[1].Animal);
        Assert.Equal(["next"], plan[2].Run.Select(s => s.Text));
    }

    [Fact]
    public void Plan_NothingToRead_IsOneEmptyStep()
    {
        IReadOnlyList<PremadeAudioPlan.Step> plan = PremadeAudioPlan.Plan([]);

        Assert.Single(plan);
        Assert.Empty(plan[0].Run);
    }

    [Fact]
    public void AsSpoken_TurnsAClipStepIntoARunOfItsPiece_AndLeavesOtherStepsAlone()
    {
        VoicedSegment clip = Clip("b", 2);
        var step = new PremadeAudioPlan.Step([], null, clip);

        PremadeAudioPlan.Step spoken = PremadeAudioPlan.AsSpoken(step);

        Assert.Null(spoken.Premade);
        Assert.Equal("b", Assert.Single(spoken.Run).Text);
        Assert.Equal(spoken, PremadeAudioPlan.AsSpoken(spoken));
    }

    [Fact]
    public void Marker_DelegatesToTheOriginalTransform_SoAFallbackReadSoundsAsBefore()
    {
        var inner = new AnimalClipMarker(AnimalKind.Cow);
        var marker = new PremadeClipMarker(inner, "/x.m4a");
        float[] samples = [0.1f];

        Assert.Equal(inner.CacheTag, marker.CacheTag);
        Assert.Equal(inner.Pace, marker.Pace);
        Assert.Same(samples, marker.Apply(samples, 16000));
        Assert.Equal(VoicedAudio.NoTag, new PremadeClipMarker(null, "/x.m4a").CacheTag);
        Assert.Equal("/x.m4a", PremadeClipMarker.PathOf(PremadeClipMarker.Mark(Speech("a", 0), "/x.m4a")));
        Assert.Null(PremadeClipMarker.PathOf(Speech("a", 0)));
    }
}
