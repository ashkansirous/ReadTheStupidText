using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

// Slice 220: a Kokoro take with NaN samples is a failed take; its retry runs on the single-thread engine; the probe logs both arms.
public sealed class KokoroNanRescueTests
{
    private const int Rate = 24000;
    private const string Kokoro = "kokoro:am_liam";

    private static SpokenTake Tone(int ms)
    {
        float[] samples = new float[ms * Rate / 1000];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.3f * MathF.Sin(i * 0.1f);
        }

        return new SpokenTake(samples, Rate);
    }

    private static SpokenTake NanTake() => new(Enumerable.Repeat(float.NaN, Rate / 2).ToArray(), Rate);

    private static SpokenTake ToneWithOneNan()
    {
        SpokenTake take = Tone(500);
        take.Samples[100] = float.NaN;
        return take;
    }

    [Fact]
    public void StatsCountNanAndIgnoreItInPeakAndVoicedSpan()
    {
        float[] samples = [0f, float.NaN, 0.5f, float.NaN, -0.7f, 0f];
        TakeStats stats = TakeStats.From(samples, 1000);
        Assert.Equal(6, stats.Samples);
        Assert.Equal(2, stats.NanCount);
        Assert.Equal(0.7f, stats.Peak);
        Assert.Equal(3.0, stats.VoicedMs, 3);
    }

    [Fact]
    public void StatsOfAnAllNanTakeHaveNoPeakAndNoVoice()
    {
        TakeStats stats = TakeStats.From(NanTake());
        Assert.Equal(Rate / 2, stats.NanCount);
        Assert.Equal(0f, stats.Peak);
        Assert.Equal(0, stats.VoicedMs);
    }

    [Fact]
    public void StatsOfNoSamplesAreZero() => Assert.Equal(new TakeStats(0, 0, 0, 0), TakeStats.From([], Rate));

    [Fact]
    public void DescribeHasNoTextAndNamesTheNanCount() =>
        Assert.Equal("samples 4, nan 1, peak 0.500, voiced ms 1", TakeStats.From([0.5f, float.NaN, 0f, 0f], 1000).Describe());

    [Fact]
    public void ATakeWithAnyNanIsSilentEvenWhenVoiced() =>
        Assert.True(SilentOutputGuard.IsSilent(ToneWithOneNan(), "Ann."));

    [Fact]
    public async Task ARetryAfterANanTakeRunsOnTheSingleThreadEngine()
    {
        var takes = new Queue<SpokenTake>([NanTake()]);
        int singleCalls = 0;
        var lines = new List<string>();
        var debug = new List<string>();

        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), lines.Add, debug: debug.Add,
            singleThreadGenerate: () =>
            {
                singleCalls++;
                return Task.FromResult(Tone(400));
            });

        Assert.False(result.GaveUp);
        Assert.Equal(1, singleCalls);
        Assert.Contains("rescued by single-thread take 2", Assert.Single(lines));
        Assert.Contains(debug, d => d.Contains("nan 12000 of 12000", StringComparison.Ordinal));
        Assert.Contains(debug, d => d.StartsWith("retry take 2 (single-thread", StringComparison.Ordinal));
        Assert.DoesNotContain(debug.Concat(lines), d => d.Contains("Ann", StringComparison.Ordinal) && !d.Contains("chars", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASilentTakeWithoutNanRetriesPlain()
    {
        var takes = new Queue<SpokenTake>([new SpokenTake(new float[Rate / 2], Rate), Tone(400)]);
        int singleCalls = 0;

        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { },
            singleThreadGenerate: () =>
            {
                singleCalls++;
                return Task.FromResult(Tone(400));
            });

        Assert.False(result.GaveUp);
        Assert.Equal(0, singleCalls);
    }

    [Fact]
    public async Task NoSingleThreadEngineMeansAPlainRetryAfterNan()
    {
        var takes = new Queue<SpokenTake>([NanTake(), Tone(400)]);
        RescueResult result = await SilentOutputGuard.RescueAsync("Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { });
        Assert.False(result.GaveUp);
    }

    [Fact]
    public void InfinityCountsAsABadSampleLikeNan()
    {
        TakeStats stats = TakeStats.From([0.5f, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 0f], 1000);
        Assert.Equal(3, stats.NanCount);
        Assert.Equal(0.5f, stats.Peak);
        Assert.True(SilentOutputGuard.IsSilent(new SpokenTake(Tone(500).Samples.Select((s, i) => i == 7 ? float.PositiveInfinity : s).ToArray(), Rate), "Ann."));
    }

    [Fact]
    public async Task AFailingSingleThreadEngineFallsBackToThePlainRetry()
    {
        var takes = new Queue<SpokenTake>([NanTake(), Tone(400)]);
        var debug = new List<string>();

        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { }, debug: debug.Add,
            singleThreadGenerate: () => throw new OutOfMemoryException());

        Assert.False(result.GaveUp);
        Assert.Contains(debug, d => d.StartsWith("single-thread take failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACancelledSingleThreadEngineStillCancels()
    {
        var takes = new Queue<SpokenTake>([NanTake()]);
        await Assert.ThrowsAsync<OperationCanceledException>(() => SilentOutputGuard.RescueAsync(
            "Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { },
            singleThreadGenerate: () => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task GivingUpAfterTheSiblingKeepsTheTakeWithoutNanEvenWhenTheSiblingHasMoreSound()
    {
        SpokenTake silent = new(new float[Rate / 4], Rate);
        SpokenTake siblingWithNan = Tone(30);
        siblingWithNan.Samples[3] = float.NaN;
        var takes = new Queue<SpokenTake>([silent, silent, silent]);

        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { }, fallback: () => Task.FromResult<SpokenTake?>(siblingWithNan));

        Assert.True(result.GaveUp);
        Assert.Equal(0, TakeStats.From(result.Take).NanCount);
    }

    [Fact]
    public async Task GivingUpWithEveryTakeNanHandsOverNoNonFiniteSample()
    {
        var takes = new Queue<SpokenTake>([NanTake(), NanTake(), NanTake()]);

        RescueResult result = await SilentOutputGuard.RescueAsync("Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), _ => { });

        Assert.True(result.GaveUp);
        Assert.All(result.Take.Samples, s => Assert.True(float.IsFinite(s)));
        Assert.Equal(Rate / 2, result.Take.Samples.Length);
    }

    [Fact]
    public async Task GivingUpKeepsATakeWithoutNanOverOneWithNan()
    {
        var takes = new Queue<SpokenTake>([ToneWithOneNan(), new SpokenTake(new float[Rate / 4], Rate), NanTake()]);
        var lines = new List<string>();

        RescueResult result = await SilentOutputGuard.RescueAsync("Ann.", Kokoro, 1.0, () => Task.FromResult(takes.Dequeue()), lines.Add);

        Assert.True(result.GaveUp);
        Assert.Equal(0, TakeStats.From(result.Take).NanCount);
        Assert.Contains("gave up", Assert.Single(lines));
    }

    [Fact]
    public async Task TheUnitRescueUsesTheSingleThreadEngineOnlyWhenTheRescueIsOn()
    {
        int singleCalls = 0;
        Task<SpokenTake> Single(string text)
        {
            singleCalls++;
            return Task.FromResult(Tone(400));
        }

        var onTakes = new Queue<SpokenTake>([NanTake(), Tone(400)]);
        await SilentOutputGuard.RescueUnitAsync(
            "Ann.", Kokoro, 1.0, false, _ => Task.FromResult(onTakes.Dequeue()), _ => { },
            singleThread: Single, nanRescue: KokoroNanRescue.SingleThreadRetry);
        Assert.Equal(1, singleCalls);

        var offTakes = new Queue<SpokenTake>([NanTake(), Tone(400)]);
        await SilentOutputGuard.RescueUnitAsync(
            "Ann.", Kokoro, 1.0, false, _ => Task.FromResult(offTakes.Dequeue()), _ => { },
            singleThread: Single, nanRescue: KokoroNanRescue.Off);
        Assert.Equal(1, singleCalls);
    }

    [Fact]
    public async Task ProbeLogsOneLinePerArmWithoutTheText()
    {
        var debug = new List<string>();
        int multi = 0;
        int single = 0;

        await SilentOutputGuard.RescueUnitAsync(
            "Ann.", Kokoro, 1.0, false,
            _ =>
            {
                multi++;
                return Task.FromResult(multi % 2 == 0 ? NanTake() : Tone(300));
            },
            _ => { }, debug: debug.Add, probe: true,
            singleThread: _ =>
            {
                single++;
                return Task.FromResult(Tone(300));
            });

        string[] probes = debug.Where(d => d.StartsWith("short line probe", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, probes.Length);
        Assert.Contains($"probe {ShortLineProbe.MultiThreadArm} ", probes[0]);
        Assert.Contains("nan 12000", probes[0]);
        Assert.Contains($"probe {ShortLineProbe.SingleThreadArm} ", probes[1]);
        Assert.Equal(ShortLineProbe.TakesPerArm, probes[1].Split("[samples").Length - 1);
        Assert.All(probes, p => Assert.DoesNotContain("Ann", p));
        Assert.Equal(ShortLineProbe.TakesPerArm, single);
    }

    [Fact]
    public async Task ProbeWithoutASingleThreadEngineLogsOnlyTheMultiThreadArm()
    {
        var debug = new List<string>();
        await SilentOutputGuard.RescueUnitAsync("Ann.", Kokoro, 1.0, false, _ => Task.FromResult(Tone(300)), _ => { }, debug: debug.Add, probe: true);
        Assert.Single(debug, d => d.StartsWith("short line probe", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Ann.", Kokoro, true)]
    [InlineData("Oh no!", Kokoro, true)]
    [InlineData("The pig ran away.", Kokoro, false)]
    [InlineData("...", Kokoro, false)]
    [InlineData("你好", Kokoro, false)]
    [InlineData("Ann.", "piper:fa-mana", false)]
    [InlineData("Eightabc", Kokoro, true)]
    [InlineData("Nineletts", Kokoro, false)]
    [InlineData("こんにちは", Kokoro, false)]
    [InlineData("안녕", Kokoro, false)]
    [InlineData("Ann 你", Kokoro, false)]
    public void TheProbeAppliesToShortKokoroLinesOnly(string text, string voice, bool expected) =>
        Assert.Equal(expected, ShortLineProbe.AppliesTo(voice, text));

    [Fact]
    public void ProbeIsOnOnlyWhileTheMarkerFileExists()
    {
        string marker = Path.Combine(Path.GetTempPath(), ShortLineProbe.MarkerFileName);
        File.Delete(marker);
        Assert.False(ShortLineProbe.InThisBuild);
        File.WriteAllText(marker, string.Empty);
        try
        {
            Assert.True(ShortLineProbe.InThisBuild);
        }
        finally
        {
            File.Delete(marker);
        }
    }
}
