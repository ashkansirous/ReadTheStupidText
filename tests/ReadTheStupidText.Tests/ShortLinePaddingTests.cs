using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class ShortLinePaddingTests
{
    private const int Rate = 24000;
    private const string Kokoro = "kokoro:am_liam";

    private static SpokenTake Tone(int ms, int padMs = 0)
    {
        int pad = padMs * Rate / 1000;
        float[] samples = new float[pad + ms * Rate / 1000 + pad];
        for (int i = pad; i < samples.Length - pad; i++)
        {
            samples[i] = 0.3f * MathF.Sin(i * 0.1f);
        }

        return new SpokenTake(samples, Rate);
    }

    private static SpokenTake Silence() => new(new float[Rate / 2], Rate);

    [Theory]
    [InlineData("Ann.", true)]
    [InlineData("Help!", true)]
    [InlineData("Oh no!", true)]
    [InlineData("...", false)]
    [InlineData("The pig ran away.", false)]
    public void AppliesToShortKokoroLinesOnly(string text, bool expected) =>
        Assert.Equal(expected, ShortLinePadding.AppliesTo(Kokoro, text));

    [Fact]
    public void OtherVoicesAreNeverPadded() =>
        Assert.False(ShortLinePadding.AppliesTo("piper:fa-mana", "Ann."));

    [Fact]
    public void PadWrapsTheTextInPauseMarks()
    {
        Assert.Equal("... Ann. ...", ShortLinePadding.Pad("Ann.", ShortLinePadStyle.Dots));
        Assert.Equal(", Ann.,", ShortLinePadding.Pad(" Ann. ", ShortLinePadStyle.Commas));
        Assert.Equal("  ", ShortLinePadding.Pad("  ", ShortLinePadStyle.Dots));
    }

    [Fact]
    public void TrimKeepsTheVoicedPartAndAMargin()
    {
        SpokenTake trimmed = ShortLinePadding.Trim(Tone(200, padMs: 500));

        int expected = (200 + 2 * 60) * Rate / 1000;
        Assert.InRange(trimmed.Samples.Length, expected - 5, expected + 5);
    }

    [Fact]
    public void TrimLeavesASilentTakeAlone()
    {
        SpokenTake silent = Silence();

        Assert.Same(silent, ShortLinePadding.Trim(silent));
    }

    [Fact]
    public void TrimKeepsASparsePaddedTakeAndAChosenDensityIsReported()
    {
        // Voiced samples alternate with silence: low density, but a long voiced span. Not discarded (the density threshold is unmeasured).
        var samples = new float[Rate];
        for (int i = 4000; i < 16000; i += 4)
        {
            samples[i] = 0.3f;
        }

        var sparse = new SpokenTake(samples, Rate);
        SpokenTake trimmed = ShortLinePadding.Trim(sparse);

        Assert.NotEmpty(trimmed.Samples);
        Assert.InRange(ShortLinePadding.VoicedDensity(sparse), 0.2, 0.3);
    }

    [Fact]
    public void ALoneClickInAPaddedTakeStaysBelowTheVoicedFloor()
    {
        var samples = new float[Rate];
        samples[Rate / 2] = 0.5f;

        SpokenTake trimmed = ShortLinePadding.Trim(new SpokenTake(samples, Rate));

        Assert.True(SilentOutputGuard.IsSilent(trimmed, "Ann."));
    }

    [Fact]
    public async Task TheSiblingVoiceSpeaksAShortLinePadded()
    {
        var spoken = new List<string>();
        Task<SpokenTake> Generate(string text) => Task.FromResult(Silence());
        Task<SpokenTake?> Sibling(string text)
        {
            spoken.Add(text);
            return Task.FromResult<SpokenTake?>(Tone(300, padMs: 400));
        }

        RescueResult result = await SilentOutputGuard.RescueUnitAsync("Ann.", Kokoro, 1.0, false, Generate, _ => { }, fallback: Sibling);

        Assert.False(result.GaveUp);
        Assert.Equal(["... Ann. ..."], spoken);
    }

    [Fact]
    public async Task ASilentShortKokoroLineIsRetriedPaddedAndTrimmed()
    {
        var spoken = new List<string>();
        Task<SpokenTake> Generate(string text)
        {
            spoken.Add(text);
            return Task.FromResult(text.StartsWith("...", StringComparison.Ordinal) ? Tone(300, padMs: 400) : Silence());
        }

        var lines = new List<string>();
        RescueResult result = await SilentOutputGuard.RescueUnitAsync("Ann.", Kokoro, 1.0, false, Generate, lines.Add);

        Assert.False(result.GaveUp);
        Assert.Equal(["Ann.", "Ann.", "... Ann. ..."], spoken);
        Assert.Single(lines);
        Assert.Contains("padded take 3", lines[0]);
        Assert.DoesNotContain("Ann", lines[0]);
        Assert.True(result.Take.Samples.Length < Tone(300, padMs: 400).Samples.Length);
    }

    [Fact]
    public async Task AVoicedShortLineIsSpokenPlainOnce()
    {
        var spoken = new List<string>();
        Task<SpokenTake> Generate(string text)
        {
            spoken.Add(text);
            return Task.FromResult(Tone(300, padMs: 100));
        }

        await SilentOutputGuard.RescueUnitAsync("Ann.", Kokoro, 1.0, false, Generate, _ => { });

        Assert.Equal(["Ann."], spoken);
    }

    [Fact]
    public async Task ALongLineIsNeverPadded()
    {
        var spoken = new List<string>();
        Task<SpokenTake> Generate(string text)
        {
            spoken.Add(text);
            return Task.FromResult(Silence());
        }

        RescueResult result = await SilentOutputGuard.RescueUnitAsync("The pig ran away home.", Kokoro, 1.0, false, Generate, _ => { });

        Assert.True(result.GaveUp);
        Assert.All(spoken, t => Assert.Equal("The pig ran away home.", t));
    }

    [Fact]
    public async Task ProbeLogsPlainAndPaddedWithoutTheText()
    {
        var debug = new List<string>();
        Task<SpokenTake> Generate(string text) => Task.FromResult(text == "Ann." ? Silence() : Tone(300, padMs: 100));

        await SilentOutputGuard.RescueUnitAsync("Ann.", Kokoro, 1.0, false, Generate, _ => { }, debug: debug.Add, probe: true);

        string line = Assert.Single(debug, d => d.StartsWith("short line probe", StringComparison.Ordinal));
        Assert.Contains("plain [samples 12000, peak 0.000, voiced ms 0, density 0.00]", line);
        Assert.Contains("dots [", line);
        Assert.Contains("commas [", line);
        Assert.DoesNotContain("Ann", line);
    }

    [Fact]
    public void Probe_is_on_only_while_the_marker_file_exists()
    {
        string marker = Path.Combine(Path.GetTempPath(), ShortLinePadding.ProbeMarkerFileName);
        File.Delete(marker);
        Assert.False(ShortLinePadding.ProbeInThisBuild);
        File.WriteAllText(marker, string.Empty);
        try
        {
            Assert.True(ShortLinePadding.ProbeInThisBuild);
        }
        finally
        {
            File.Delete(marker);
        }
    }
}
