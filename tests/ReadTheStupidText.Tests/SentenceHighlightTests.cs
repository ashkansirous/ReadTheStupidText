using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 190: the phone highlights one sentence at a time.
public class SentenceHighlightTests
{
    private const string Three = "The quick brown fox jumps over the dog. A second sentence follows here. And the third one ends it.";

    [Fact]
    public void Spans_PointAtEachSentenceOfTheSource()
    {
        IReadOnlyList<SentenceSpan>? spans = SentenceSpans.Compute(Three, Three, 0);

        Assert.NotNull(spans);
        Assert.Equal(3, spans!.Count);
        Assert.Equal("The quick brown fox jumps over the dog.", Three[spans[0].Start..spans[0].End]);
        Assert.Equal("A second sentence follows here.", Three[spans[1].Start..spans[1].End]);
        Assert.Equal("And the third one ends it.", Three[spans[2].Start..spans[2].End]);
    }

    [Fact]
    public void Spans_StartFromTheChunksSourceStartAndSkipWhitespaceRewrites()
    {
        string full = "Intro line here.\n\nFirst   sentence of the chunk.\nSecond sentence of the chunk.";
        int start = full.IndexOf("First", StringComparison.Ordinal);
        string chunk = "First sentence of the chunk. Second sentence of the chunk.";

        IReadOnlyList<SentenceSpan>? spans = SentenceSpans.Compute(chunk, full, start);

        Assert.Equal(2, spans!.Count);
        Assert.Equal("First   sentence of the chunk.", full[spans[0].Start..spans[0].End]);
        Assert.Equal("Second sentence of the chunk.", full[spans[1].Start..spans[1].End]);
    }

    [Fact]
    public void Spans_ForUnpunctuatedTextAreOne()
    {
        const string text = "just some words with no full stop at all";

        IReadOnlyList<SentenceSpan>? spans = SentenceSpans.Compute(text, text, 0);

        Assert.Single(spans!);
        Assert.Equal(new SentenceSpan(0, text.Length), spans![0]);
    }

    [Fact]
    public void Spans_ForFarsiTextCoverEachSentence()
    {
        const string text = "این جمله اول برای آزمایش است. این هم جمله دوم برای آزمایش است.";

        IReadOnlyList<SentenceSpan>? spans = SentenceSpans.Compute(text, text, 0);

        Assert.Equal(2, spans!.Count);
        Assert.Equal("این جمله اول برای آزمایش است.", text[spans[0].Start..spans[0].End]);
        Assert.Equal("این هم جمله دوم برای آزمایش است.", text[spans[1].Start..spans[1].End]);
    }

    [Fact]
    public void Spans_AreNullWhenTheChunkIsNotFromTheText()
    {
        Assert.Null(SentenceSpans.Compute(Three, "Something else entirely, not the chunk.", 0));
        Assert.Null(SentenceSpans.Compute(Three, Three, Three.Length + 1));
    }

    [Fact]
    public void Timeline_SwitchesAtEachClipBoundaryIncludingTheGap()
    {
        var timeline = new SentenceTimeline(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)], TimeSpan.FromMilliseconds(200));

        Assert.Equal(0, timeline.IndexAt(TimeSpan.Zero));
        Assert.Equal(0, timeline.IndexAt(TimeSpan.FromMilliseconds(2199)));
        Assert.Equal(1, timeline.IndexAt(TimeSpan.FromMilliseconds(2200)));
        Assert.Equal(1, timeline.IndexAt(TimeSpan.FromMilliseconds(5399)));
        Assert.Equal(2, timeline.IndexAt(TimeSpan.FromMilliseconds(5400)));
        Assert.Equal(2, timeline.IndexAt(TimeSpan.FromSeconds(60)));
    }

    private static SentenceTimeline Timeline(int units) =>
        new(Enumerable.Repeat(TimeSpan.FromSeconds(1), units).ToList(), TimeSpan.Zero);

    private sealed class Recorder
    {
        public SentenceProgress Progress { get; } = new();
        public List<string> Log { get; } = [];

        public Recorder()
        {
            Progress.Changed += c => Log.Add($"S{c.ChunkIndex}.{c.SentenceIndex}");
        }

        public void Play(int chunk, string path)
        {
            Progress.Select(path);
            Progress.Begin(chunk, path, () => Log.Add($"C{chunk}"));
        }

        public void At(int ms) => Progress.Update(TimeSpan.FromMilliseconds(ms));
    }

    private static Recorder Started(int chunk, int units)
    {
        var r = new Recorder();
        r.Progress.Register("a.wav", Timeline(units));
        r.Play(chunk, "a.wav");
        r.Log.Clear();
        return r;
    }

    [Fact]
    public void Progress_ReportsEachSwitchOnceWithItsChunk()
    {
        Recorder r = Started(4, 3);

        r.At(500);
        r.At(1100);
        r.At(1500);
        r.At(2100);

        Assert.Equal(["S4.1", "S4.2"], r.Log);
    }

    [Fact]
    public void Progress_BeginAnnouncesTheChunkThenTheFirstSentenceOnlyWhenTracked()
    {
        var r = new Recorder();
        r.Progress.Register("a.wav", Timeline(2));
        r.Progress.Register("b.wav", Timeline(1));

        r.Play(3, "a.wav");
        r.Play(4, "b.wav");
        r.Play(5, "unknown.wav");

        Assert.Equal(["C3", "S3.0", "C4", "C5"], r.Log);
    }

    [Fact]
    public void Progress_BeginResetsToTheFirstSentenceForTheNextChunk()
    {
        Recorder r = Started(0, 2);
        r.At(1500);
        r.Progress.Register("b.wav", Timeline(2));
        r.Log.Clear();

        r.Play(1, "b.wav");
        r.At(200);
        r.At(1200);

        Assert.Equal(["C1", "S1.0", "S1.1"], r.Log);
    }

    [Fact]
    public void Progress_ReportsNothingForOneSentenceOrAfterClear()
    {
        Recorder one = Started(0, 1);
        one.At(4000);
        Assert.Empty(one.Log);

        Recorder cleared = Started(0, 2);
        cleared.Progress.Clear();
        cleared.At(1500);
        Assert.Empty(cleared.Log);
    }

    [Fact]
    public void Progress_SelectStopsTheOldChunksTimelineBeforeTheNewOneIsAnnounced()
    {
        Recorder r = Started(0, 3);
        r.At(2500);
        r.Progress.Register("b.wav", Timeline(3));
        r.Log.Clear();

        r.Progress.Select("b.wav");
        r.At(0);
        r.At(100);
        r.Progress.Begin(1, "b.wav", () => r.Log.Add("C1"));

        Assert.Equal(["C1", "S1.0"], r.Log);
    }

    [Fact]
    public void Progress_DiscardedOrResetTimelinesAreForgotten()
    {
        var r = new Recorder();
        r.Progress.Register("a.wav", Timeline(2));
        r.Progress.Register("b.wav", Timeline(2));

        r.Progress.Discard("a.wav");
        r.Play(0, "a.wav");
        r.Progress.Reset();
        r.Play(1, "b.wav");

        Assert.Equal(["C0", "C1"], r.Log);
    }

    [Fact]
    public void Progress_TheSameChunkFileAnnouncedTwiceStillTracks()
    {
        var r = new Recorder();
        r.Progress.Register("a.wav", Timeline(2));

        r.Play(0, "a.wav");
        r.Progress.Begin(0, "a.wav", () => r.Log.Add("C0"));
        r.At(1200);

        Assert.Equal(["C0", "S0.0", "C0", "S0.0", "S0.1"], r.Log);
    }

    [Fact]
    public void Progress_ABeginForAFileThatIsNoLongerSelectedIsIgnored()
    {
        var r = new Recorder();
        r.Progress.Register("a.wav", Timeline(3));
        r.Progress.Register("b.wav", Timeline(3));
        r.Progress.Select("a.wav");
        r.Progress.Select("b.wav");

        r.Progress.Begin(0, "a.wav", () => r.Log.Add("C0"));
        r.At(2500);

        Assert.Empty(r.Log);
    }

    [Fact]
    public void Progress_TimelinesRegisteredAfterAStopAreNotKept()
    {
        var r = new Recorder();
        r.Progress.Clear();
        r.Progress.Register("a.wav", Timeline(2));

        r.Play(0, "a.wav");

        Assert.Equal(["C0"], r.Log);
    }

    [Fact]
    public void FromClips_JoinsReadableClipsWithTheGap()
    {
        byte[] oneSecond = WavFile.Encode(new float[1000], 1000);

        SentenceTimeline? timeline = SentenceTimeline.FromClips([oneSecond, oneSecond], TimeSpan.FromMilliseconds(500));

        Assert.NotNull(timeline);
        Assert.Equal(0, timeline!.IndexAt(TimeSpan.FromMilliseconds(1499)));
        Assert.Equal(1, timeline.IndexAt(TimeSpan.FromMilliseconds(1500)));
    }

    [Fact]
    public void FromClips_IsNullWhenAnyClipLengthIsUnreadableOrEmpty()
    {
        byte[] good = WavFile.Encode(new float[1000], 1000);
        byte[] empty = WavFile.Encode([], 1000);
        byte[] garbage = new byte[10];

        Assert.Null(SentenceTimeline.FromClips([good, garbage], TimeSpan.Zero));
        Assert.Null(SentenceTimeline.FromClips([good, empty], TimeSpan.Zero));
    }

    [Fact]
    public void Gate_IgnoresASentenceChangeUntilTheChunksFirstSentenceIsAnnounced()
    {
        var gate = new SentenceHighlightGate();
        gate.Reset(0);

        Assert.False(gate.Accept(new SentenceChange(0, 1)));
        Assert.True(gate.Accept(new SentenceChange(0, 0)));
        Assert.True(gate.Accept(new SentenceChange(0, 1)));
    }

    [Fact]
    public void Gate_DropsOtherChunksAndEverythingAfterTheHighlightIsCleared()
    {
        var gate = new SentenceHighlightGate();
        gate.Reset(2);
        gate.Accept(new SentenceChange(2, 0));

        Assert.False(gate.Accept(new SentenceChange(1, 1)));

        gate.Reset(-1);
        Assert.False(gate.Accept(new SentenceChange(2, 0)));
    }

    [Fact]
    public void Gate_RearmsForAFreshReadOfTheSameChunk()
    {
        var gate = new SentenceHighlightGate();
        gate.Reset(0);
        gate.Accept(new SentenceChange(0, 0));
        gate.Reset(0);

        Assert.False(gate.Accept(new SentenceChange(0, 1)));
    }
}
