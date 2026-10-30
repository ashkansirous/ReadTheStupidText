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
    public void FiveLetterLineNeedsOneHundredFiftyMsAtNormalPace() =>
        Assert.Equal(150, SilentOutputGuard.NeededMs("Hello"));

    [Fact]
    public async Task GaveUpLineNamesTheVoicedMsAndTheFloorButNotTheText()
    {
        var lines = new List<string>();
        SpokenTake barelyShort = Tone(120, padMs: 100);
        await SilentOutputGuard.RescueAsync("Hello", "kokoro:am_liam", 1.0, () => Task.FromResult(barelyShort), lines.Add);
        string line = Assert.Single(lines);
        Assert.Contains("gave up", line);
        Assert.Matches(@"voiced ms 1(19|20)/1(19|20)/1(19|20),", line);
        Assert.Contains("needed 150", line);
        Assert.DoesNotContain("Hello", line);
    }

    // Spanish and Portuguese have a single woman each (Dora), so no same-gender stand-in exists for them.
    private static readonly HashSet<string> OnlyWomanOfLanguage = ["kokoro:ef_dora", "kokoro:pf_dora"];

    [Fact]
    public void EveryShippedKokoroVoiceHasASiblingOfTheSameLanguageAndGender()
    {
        foreach (string language in KokoroVoiceTable.Languages)
        {
            IReadOnlyList<VoiceInfo> voices = KokoroVoiceTable.VoicesFor(language);
            var pack = new VoiceModelPaths("x", VoiceEngineKind.Kokoro, language, voices);
            foreach (VoiceInfo voice in voices.Where(v => !OnlyWomanOfLanguage.Contains(v.Id)))
            {
                VoiceInfo? sibling = SiblingVoice.Pick(pack, voice);
                Assert.True(sibling is not null, voice.Id + " has no sibling");
                Assert.NotEqual(voice.Id, sibling!.Id);
                Assert.Equal(KokoroVoiceTable.IsFemale(voice.Id), KokoroVoiceTable.IsFemale(sibling.Id));
            }
        }
    }

    [Fact]
    public async Task KokoroShortLineSilentThreeTimesIsRescuedBySiblingMan()
    {
        VoiceInfo liam = new("kokoro:am_liam", "Liam", "en", VoiceEngineKind.Kokoro, 15);
        VoiceInfo michael = new("kokoro:am_michael", "Michael", "en", VoiceEngineKind.Kokoro, 16);
        var pack = new VoiceModelPaths("x", VoiceEngineKind.Kokoro, "en", [liam, michael]);
        Assert.Equal(michael, SiblingVoice.Pick(pack, liam));
        VoiceInfo fable = new("kokoro:bm_fable", "Fable", "en", VoiceEngineKind.Kokoro, 25);
        VoiceInfo daniel = new("kokoro:bm_daniel", "Daniel", "en", VoiceEngineKind.Kokoro, 24);
        VoiceInfo puck = new("kokoro:am_puck", "Puck", "en", VoiceEngineKind.Kokoro, 18);
        var all = pack with { Voices = [liam, michael, fable, daniel, puck] };
        Assert.Equal(fable, SiblingVoice.Pick(all, michael));
        Assert.Equal(daniel, SiblingVoice.Pick(all, puck));
        var lines = new List<string>();
        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Hello", liam.Id, 1.0, () => Task.FromResult(new SpokenTake([], Rate)), lines.Add,
            fallback: () => Task.FromResult<SpokenTake?>(Tone(300)));
        Assert.False(result.GaveUp);
        Assert.Matches(@", sibling (29\d|300)\b", Assert.Single(lines));
    }

    [Fact]
    public async Task FirstTakeThatPassesUnderOneAndAQuarterTimesTheFloorGoesToTheDebugLogOnly()
    {
        var lines = new List<string>();
        var debug = new List<string>();
        RescueResult result = await SilentOutputGuard.RescueAsync(
            "Hello", "kokoro:am_liam", 1.0, () => Task.FromResult(Tone(170, padMs: 100)), lines.Add, debug: debug.Add);
        Assert.False(result.GaveUp);
        Assert.Empty(lines);
        string line = Assert.Single(debug);
        Assert.Contains("near the floor", line);
        Assert.Contains("needed 150", line);
        Assert.DoesNotContain("Hello", line);
    }

    [Fact]
    public async Task FirstTakeWellOverTheFloorLogsNothing()
    {
        var lines = new List<string>();
        await SilentOutputGuard.RescueAsync("Hello", "v", 1.0, () => Task.FromResult(Tone(400)), lines.Add);
        Assert.Empty(lines);
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
