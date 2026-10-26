using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class SilentOutputGuardTests
{
    private const int Rate = 22050;

    private static SpokenTake Tone(int ms, float padMs = 0)
    {
        int pad = (int)(padMs * Rate / 1000);
        float[] samples = new float[pad + ms * Rate / 1000 + pad];
        for (int i = pad; i < samples.Length - pad; i++)
        {
            samples[i] = 0.3f * MathF.Sin(i * 0.1f);
        }

        return new SpokenTake(samples, Rate);
    }

    [Fact]
    public void NoSamplesIsSilentForRealText() =>
        Assert.True(SilentOutputGuard.IsSilent(new SpokenTake([], Rate), "تق!"));

    [Fact]
    public void NearZeroTraceIsSilent() =>
        Assert.True(SilentOutputGuard.IsSilent(new SpokenTake(new float[Rate], Rate), "دنگ!"));

    [Fact]
    public void ShortClickIsSilent() =>
        Assert.True(SilentOutputGuard.IsSilent(Tone(40, padMs: 500), "ووش!"));

    [Fact]
    public void SpokenShortWordIsNotSilent() =>
        Assert.False(SilentOutputGuard.IsSilent(Tone(200, padMs: 500), "تق!"));

    [Fact]
    public void PunctuationOnlyIsNeverSilent() =>
        Assert.False(SilentOutputGuard.IsSilent(new SpokenTake([], Rate), "..."));

    [Fact]
    public void LongTextWithThinSoundIsSilent() =>
        Assert.True(SilentOutputGuard.IsSilent(Tone(300), new string('a', 100)));

    [Fact]
    public void ShortClipAtFastPaceIsNotSilent() =>
        Assert.False(SilentOutputGuard.IsSilent(Tone(85, padMs: 100), "ووش!", pace: 1.15));

    [Fact]
    public void ClipFarShorterThanItsTextNeedsIsSilent() =>
        Assert.True(SilentOutputGuard.IsSilent(Tone(300, padMs: 100), "بچه خوک فرار کرد. تق!"));

    private static Task<RescueResult> Rescue(
        string text, Func<Task<SpokenTake>> generate, List<string> lines, Func<Task<SpokenTake?>>? fallback = null,
        CancellationToken token = default) =>
        SilentOutputGuard.RescueAsync(text, "piper:fa-mana", 1.0, generate, lines.Add, token, fallback);

    [Fact]
    public async Task RescueReturnsTheFirstVoicedTake()
    {
        var takes = new Queue<SpokenTake>([new SpokenTake([], Rate), Tone(300)]);
        var lines = new List<string>();
        RescueResult result = await Rescue("تق!", () => Task.FromResult(takes.Dequeue()), lines);
        Assert.False(result.GaveUp);
        Assert.False(SilentOutputGuard.IsSilent(result.Take, "تق!"));
        string line = Assert.Single(lines);
        Assert.Contains("piper:fa-mana", line);
    }

    [Fact]
    public async Task RescueGivesUpAfterTheExtraTakes()
    {
        int calls = 0;
        var lines = new List<string>();
        RescueResult result = await Rescue("تق!", () => { calls++; return Task.FromResult(new SpokenTake([], Rate)); }, lines);
        Assert.Equal(SilentOutputGuard.ExtraTakes + 1, calls);
        Assert.True(result.GaveUp);
        Assert.Single(lines);
    }

    [Fact]
    public async Task GiveUpKeepsTheTakeWithTheMostSound()
    {
        var takes = new Queue<SpokenTake>([Tone(60), Tone(40), new SpokenTake([], Rate)]);
        RescueResult result = await Rescue("ووش! ووش!", () => Task.FromResult(takes.Dequeue()), []);
        Assert.True(result.GaveUp);
        Assert.Equal(Tone(60).Samples.Length, result.Take.Samples.Length);
    }

    [Fact]
    public async Task TextWithNoLettersIsNeverRetried()
    {
        int calls = 0;
        await Rescue("...", () => { calls++; return Task.FromResult(new SpokenTake([], Rate)); }, []);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task VoicedFirstTakeIsNotRetriedOrLogged()
    {
        int calls = 0;
        var lines = new List<string>();
        await Rescue("تق!", () => { calls++; return Task.FromResult(Tone(300)); }, lines);
        Assert.Equal(1, calls);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task CancelledTokenStopsBetweenTakes()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() => Rescue(
            "تق!", () => { calls++; cts.Cancel(); return Task.FromResult(new SpokenTake([], Rate)); }, [], null, cts.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SiblingVoiceRescuesWhenEveryTakeIsSilent()
    {
        var lines = new List<string>();
        RescueResult result = await Rescue(
            "تق!", () => Task.FromResult(new SpokenTake([], Rate)), lines, () => Task.FromResult<SpokenTake?>(Tone(300)));
        Assert.False(result.GaveUp);
        Assert.Contains("sibling", Assert.Single(lines));
    }

    [Fact]
    public async Task SiblingIsNotAskedWhenASameVoiceTakeWorks()
    {
        bool asked = false;
        await Rescue("تق!", () => Task.FromResult(Tone(300)), [], () => { asked = true; return Task.FromResult<SpokenTake?>(null); });
        Assert.False(asked);
    }

    private static VoiceModelPaths FarsiPack(out VoiceInfo mana, out VoiceInfo ganji, out VoiceInfo gyro)
    {
        VoiceInfo adabi = new("piper:fa-ganji-adabi", "Ganji Adabi", "fa", VoiceEngineKind.Vits, 0, "voices/ganji_adabi");
        ganji = new("piper:fa-ganji", "Ganji", "fa", VoiceEngineKind.Vits, 0, "voices/ganji");
        mana = new("piper:fa-mana", "Mana", "fa", VoiceEngineKind.Vits, 0, "voices/mana");
        gyro = new("piper:fa-gyro", "Gyro", "fa", VoiceEngineKind.Vits, 0, "voices/gyro");
        return new VoiceModelPaths("x", VoiceEngineKind.Vits, "fa", [adabi, ganji, mana, gyro]);
    }

    [Fact]
    public void ManaSiblingIsGanjiWhateverTheListOrder()
    {
        VoiceModelPaths pack = FarsiPack(out VoiceInfo mana, out VoiceInfo ganji, out VoiceInfo gyro);
        Assert.Equal(ganji, SiblingVoice.Pick(pack, mana));
        Assert.Equal(ganji, SiblingVoice.Pick(pack, gyro));
    }

    [Fact]
    public void VoiceWithoutAChosenSiblingOrPackHasNone()
    {
        VoiceModelPaths pack = FarsiPack(out VoiceInfo mana, out _, out _);
        Assert.Null(SiblingVoice.Pick(pack with { Voices = [mana] }, mana));
        Assert.Null(SiblingVoice.Pick(new VoiceModelPaths("x"), SupertonicVoiceTable.Voices[0]));
    }

    [Fact]
    public void ShortCommaFragmentIsNotJudgedSilentByTheSoundWordFloor() =>
        Assert.False(SilentOutputGuard.IsSilent(Tone(40), "و،"));

    [Fact]
    public async Task UnitJoinResamplesATakeAtAnotherRate()
    {
        var rates = new Queue<int>([22050, 44100]);
        RescueResult result = await SilentOutputGuard.RescueUnitAsync(
            "Pip ran away. تق!", "v", 1.0, true,
            phrase => Task.FromResult(new SpokenTake(Tone(500).Samples.Select(v => v).ToArray(), rates.Dequeue())),
            _ => { });
        Assert.Equal(22050, result.Take.SampleRate);
    }
}
