using System.Globalization;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.Reading;

/// <summary>One generated clip: mono samples and their rate.</summary>
public sealed record SpokenTake(float[] Samples, int SampleRate);

/// <summary>A rescued unit's final take, and whether the guard gave up (the clip is still silent or far too short).</summary>
public sealed record RescueResult(SpokenTake Take, bool GaveUp);

/// <summary>
/// Catches a voice that returned no speech for real text (Slice 149): Piper <c>mana</c> drops short Farsi
/// sound words («تق!», «دنگ!») in about one take in four, with no error. A dropped word is a hole in the
/// story, so a silent take is generated again; the engines start from noise, so a new take is a new chance.
/// </summary>
public static class SilentOutputGuard
{
    /// <summary>How many more takes a silent one gets (3 takes in all: 26% drop becomes under 2%).</summary>
    public const int ExtraTakes = 2;

    // A sample under this is silence (the Slice 145 dropped clips peaked near zero; padding and room tone sit far below).
    internal const float VoicedAmplitude = 0.01f;

    // Slice 145 run5: a dropped mana word measured 0-97 ms of sound, the shortest legitimate clip (gyro «ووش!») 113 ms.
    // The floor is for pace 1.0; the engine's Speed shrinks a clip, so it scales with 1 / pace.
    private const double MinVoicedMs = 80;

    // Far fewer ms per letter than any voice speaks: the quickest legitimate clips (ganji at pace 1.15, es Supertonic
    // carrier) measured 54-64 ms per letter, gyro's 3-letter «ووش!» 37.6. 30 gave 0 false rescues in 972 clips of
    // ganji, ganji-adabi, gyro, Supertonic and Kokoro (en, es, zh, fa; paces 0.85-1.15).
    private const double MinVoicedMsPerLetter = 30;

    // Slice 341, measured with the app's own engines (tmp/341-peaks, 2026-11-09): Piper mana returns a hiss that passes the
    // span floor above (12 of 12 takes of «میو میو!» peaked 0.018-0.059 and ran 192-528 ms; «بوووم!» and «سلام.» the same), while
    // its spoken takes of the same units peak 0.101 and up (sound words 0.106-0.41, sentences 0.14-0.30). Between 0.045 and 0.09
    // the takes are a continuum with no clean gap, so the check errs towards a retake (it costs one more take, never a hole).
    // The check is for drop-prone voices only: Piper nb mnn speaks at a peak of 0.027-0.040, so a global floor would
    // retake every nb sentence. The measured speech peaks of the other voices are far higher (ganji, ganji-adabi, gyro 0.115+,
    // Supertonic 0.167+, Kokoro 0.196+), but those voices are not checked.

    /// <summary>A checked (drop-prone) voice's take whose loudest finite sample is under this holds no speech, however long its quiet trace is.</summary>
    internal const float MinPeak = 0.09f;

    // A sample at or over this counts as sound for the loud-duration test: above every hiss take measured (0.059), so a hiss
    // never adds up to sound, and a click or pop is a millisecond or less.
    internal const float SoundAmplitude = 0.05f;

    // A take with less sound than this (ms of samples at or over SoundAmplitude) is a click, not speech, whatever its peak:
    // the quietest spoken take measured (peak 0.095) holds 5 ms, a pop 0.1 ms.
    internal const double MinSoundMs = 1;

    private const double NormalPace = 1.0;

    // A first take that passes under this many times the floor is a near miss, sent to the debug log only.
    private const double NearMissFactor = 1.25;

    /// <summary>True when the take holds no speech, or far too little, for text that has something to say, or holds any NaN sample (Slice 220).</summary>
    public static bool IsSilent(SpokenTake take, string text, double pace = NormalPace, bool quietCheck = false) =>
        Failed(TakeStats.From(take), text, pace, quietCheck);

    // A NaN sample is a failed take whatever else it holds (Slice 220: the S21's silent Kokoro takes are full length and NaN).
    private static bool Failed(TakeStats stats, string text, double pace, bool quietCheck) =>
        stats.NanCount > 0 || IsTooQuiet(stats, text, quietCheck) || IsTooShort(stats.VoicedMs, text, pace);

    // Only a checked voice's text with something to say needs sound; a mark-only unit is never silent.
    private static bool IsTooQuiet(TakeStats stats, string text, bool quietCheck) =>
        quietCheck && stats.IsQuiet && HasSpeakableText(text);

    // True when the quiet check alone caught the take: no NaN and long enough, so the span rule would have let it play.
    private static bool CaughtOnlyByQuietCheck(TakeStats stats, string text, double pace, bool quietCheck) =>
        stats.NanCount == 0 && IsTooQuiet(stats, text, quietCheck) && !IsTooShort(stats.VoicedMs, text, pace);

    private static bool HasSpeakableText(string text) => text.Any(char.IsLetterOrDigit);

    /// <summary>
    /// Runs <paramref name="generate"/>, and again while the take is silent or holds NaN samples (at most
    /// <see cref="ExtraTakes"/> more times), then once through <paramref name="fallback"/> (a sibling voice; it returns
    /// null when there is none). Returns the first voiced take, else the one with the most sound (and
    /// <see cref="RescueResult.GaveUp"/>); <paramref name="report"/> gets one line per rescue or give-up, naming
    /// <paramref name="voiceId"/>. The retry that follows a take with NaN samples runs through
    /// <paramref name="singleThreadGenerate"/> when there is one (Slice 220). A cancelled <paramref name="token"/>
    /// stops between takes.
    /// </summary>
    public static async Task<RescueResult> RescueAsync(
        string text, string voiceId, double pace, Func<Task<SpokenTake>> generate, Action<string> report,
        CancellationToken token = default, Func<Task<SpokenTake?>>? fallback = null, Action<string>? debug = null,
        Func<Task<SpokenTake>>? singleThreadGenerate = null, IFallbackSink? fallbacks = null, string? language = null, bool quietCheck = false)
    {
        SpokenTake best = await generate();
        TakeStats bestStats = TakeStats.From(best);
        ReportNan(bestStats, voiceId, text, debug);
        TakeStats lastStats = bestStats;
        bool quietCause = CaughtOnlyByQuietCheck(bestStats, text, pace, quietCheck);
        var all = new List<TakeStats> { bestStats };
        for (int attempt = 1; attempt <= ExtraTakes && Failed(lastStats, text, pace, quietCheck); attempt++)
        {
            token.ThrowIfCancellationRequested();
            // Slice 220: the retry after a NaN take runs on the single-thread engine; any other retry stays plain (Slice 210).
            bool failedQuiet = quietCause; // the cause of the take being retried, not of a later one
            bool single = singleThreadGenerate is not null && lastStats.NanCount > 0;
            debug?.Invoke($"retry take {attempt + 1} ({(single ? "single-thread" : "plain")}, voice {voiceId}, {text.Length} chars)");
            SpokenTake take = single ? await GenerateSingleThreadAsync(singleThreadGenerate!, generate, voiceId, text, debug) : await generate();
            lastStats = TakeStats.From(take);
            ReportNan(lastStats, voiceId, text, debug);
            all.Add(lastStats);
            quietCause = CaughtOnlyByQuietCheck(lastStats, text, pace, quietCheck);
            if (!Failed(lastStats, text, pace, quietCheck))
            {
                report($"silent voice output rescued by {(single ? "single-thread " : string.Empty)}take {attempt + 1} (voice {voiceId}, {text.Length} chars, {Measured(all, text, pace)}{Cause(failedQuiet)})");
                fallbacks?.Report(FallbackArea.Speech, failedQuiet ?FallbackReason.QuietRetake : FallbackReason.SilentRetake, language); // Slice 273 part B
                return new RescueResult(take, false);
            }

            if (IsBetter(lastStats, bestStats, quietCheck))
            {
                (best, bestStats) = (take, lastStats);
            }
        }

        if (Failed(lastStats, text, pace, quietCheck))
        {
            return await TrySiblingAsync(text, voiceId, pace, (best, bestStats, all), fallback, report, token, fallbacks, language, quietCheck, quietCause);
        }

        ReportNearMiss(text, voiceId, pace, all, debug, quietCheck);
        return new RescueResult(best, false);
    }

    // The single-thread twin can fail to load (out of memory, native error): that must not fail the sentence, so the plain retry runs instead.
    private static async Task<SpokenTake> GenerateSingleThreadAsync(
        Func<Task<SpokenTake>> singleThread, Func<Task<SpokenTake>> generate, string voiceId, string text, Action<string>? debug)
    {
        try
        {
            return await singleThread();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // no-fallback: a plain retry with the same voice runs instead; a silent result is reported by the guard's own SilentRetake/SilentGaveUp reports.
            debug?.Invoke($"single-thread take failed, plain retry instead (voice {voiceId}, {text.Length} chars): {e.GetType().Name}");
            return await generate();
        }
    }

    // A take without NaN beats one with; then, for a checked voice, a take with real sound (enough ms at SoundAmplitude, not one
    // pop) beats a quiet one whatever its span; then more voiced span beats less.
    private static bool IsBetter(TakeStats candidate, TakeStats best, bool quietCheck)
    {
        if ((candidate.NanCount > 0) != (best.NanCount > 0))
        {
            return candidate.NanCount == 0;
        }

        return quietCheck && candidate.IsQuiet != best.IsQuiet ? !candidate.IsQuiet : candidate.VoicedMs > best.VoicedMs;
    }

    // Debug log only, never the text: how many samples of the take are NaN (Slice 220).
    private static void ReportNan(TakeStats stats, string voiceId, string text, Action<string>? debug)
    {
        if (stats.NanCount > 0)
        {
            debug?.Invoke($"take has NaN samples (voice {voiceId}, {text.Length} chars, nan {stats.NanCount} of {stats.Samples})");
        }
    }

    // A first take that passed with under 1.25 times the floor (Slice 210; debug log only, so it cannot flood the activity log): the device run reads these to choose a floor.
    private static void ReportNearMiss(string text, string voiceId, double pace, List<TakeStats> takes, Action<string>? debug, bool quietCheck)
    {
        if (debug is null)
        {
            return;
        }

        if (takes[0].VoicedMs < NeededMs(text, pace) * NearMissFactor)
        {
            debug($"voice output passed near the floor (voice {voiceId}, {text.Length} chars, {Measured(takes, text, pace)})");
        }
        else if (quietCheck && HasSpeakableText(text) && takes[0].Peak < MinPeak * NearMissFactor)
        {
            debug($"voice output passed near the peak floor (voice {voiceId}, {text.Length} chars, {Measured(takes, text, pace)})");
        }
    }

    // Marks a rescue line the quiet check alone caused (Slice 341), so the log tells it from a too-short take.
    private static string Cause(bool quietCause) => quietCause ? ", caught by the peak check" : string.Empty;

    // The measure behind a rescue line (Slice 210, peaks Slice 341): voiced ms and peak of each take and the floors they were held to. No text.
    private static string Measured(List<TakeStats> takes, string text, double pace) =>
        $"voiced ms {string.Join("/", takes.Select(t => (int)t.VoicedMs))}, needed {(int)NeededMs(text, pace)}, "
        + $"peak {string.Join("/", takes.Select(t => t.Peak.ToString("0.000", CultureInfo.InvariantCulture)))}, min peak {MinPeak:0.000}, pace {pace:0.00}";

    /// <summary>
    /// As <see cref="RescueAsync"/> for a whole unit. With <paramref name="splitPhrases"/> (a drop-prone voice) the unit
    /// is spoken phrase by phrase (<see cref="PhraseSplitter"/>), each phrase guarded and rescued on its own, and the
    /// takes are joined with a short silence, so a sound word glued to a sentence cannot vanish unseen. Other voices
    /// speak the unit whole. <paramref name="splitPhrases"/> also switches on the quiet check (<see cref="MinPeak"/>), which only
    /// the drop-prone voice needs. <see cref="RescueResult.GaveUp"/> is true when any phrase gave up.
    /// </summary>
    public static async Task<RescueResult> RescueUnitAsync(
        string text, string voiceId, double pace, bool splitPhrases, Func<string, Task<SpokenTake>> generate,
        Action<string> report, CancellationToken token = default, Func<string, Task<SpokenTake?>>? fallback = null,
        Action<string>? debug = null, bool probe = false, Func<string, Task<SpokenTake>>? singleThread = null,
        KokoroNanRescue nanRescue = KokoroNanRescue.Off, IFallbackSink? fallbacks = null, string? language = null)
    {
        IReadOnlyList<Phrase> phrases = splitPhrases ? PhraseSplitter.Split(text) : [new Phrase(text, 0)];
        var takes = new List<SpokenTake>();
        bool gaveUp = false;
        foreach (Phrase phrase in phrases)
        {
            token.ThrowIfCancellationRequested();
            if (probe && ShortLineProbe.AppliesTo(voiceId, phrase.Text))
            {
                await ShortLineProbe.RunAsync(phrase.Text, voiceId, generate, singleThread, debug, token);
            }

            bool rescueOnSingleThread = singleThread is not null && nanRescue == KokoroNanRescue.SingleThreadRetry;
            RescueResult one = await RescueAsync(
                phrase.Text, voiceId, pace, () => generate(phrase.Text), report, token,
                fallback is null ? null : () => fallback(phrase.Text), debug,
                rescueOnSingleThread ? () => singleThread!(phrase.Text) : null, fallbacks, language, quietCheck: splitPhrases);
            takes.Add(one.Take);
            gaveUp |= one.GaveUp;
        }

        return new RescueResult(takes.Count == 1 ? takes[0] : Join(takes, phrases.Select(p => p.GapAfterMs).ToList()), gaveUp);
    }

    // The takes in one clip, a silence after each but the last. A take from another model (the sibling voice) can have
    // another rate; it is resampled to the first take's so it plays at the right speed and pitch.
    private static SpokenTake Join(List<SpokenTake> takes, List<int> gapsMs)
    {
        int rate = takes[0].SampleRate;
        var samples = new List<float>();
        for (int i = 0; i < takes.Count; i++)
        {
            samples.AddRange(takes[i].SampleRate == rate ? takes[i].Samples : Resample(takes[i], rate));
            if (i < takes.Count - 1)
            {
                samples.AddRange(new float[rate * gapsMs[i] / 1000]);
            }
        }

        return new SpokenTake(samples.ToArray(), rate);
    }

    // Linear interpolation: enough for speech joined into one clip.
    private static float[] Resample(SpokenTake take, int rate)
    {
        float[] source = take.Samples;
        if (source.Length == 0 || take.SampleRate <= 0)
        {
            return source;
        }

        var result = new float[(int)((long)source.Length * rate / take.SampleRate)];
        double step = (double)take.SampleRate / rate;
        for (int i = 0; i < result.Length; i++)
        {
            double position = i * step;
            int index = Math.Min((int)position, source.Length - 1);
            float next = source[Math.Min(index + 1, source.Length - 1)];
            result[i] = (float)(source[index] + (next - source[index]) * (position - index));
        }

        return result;
    }

    private static async Task<RescueResult> TrySiblingAsync(
        string text, string voiceId, double pace, (SpokenTake Take, TakeStats Stats, List<TakeStats> Takes) best, Func<Task<SpokenTake?>>? fallback,
        Action<string> report, CancellationToken token, IFallbackSink? fallbacks, string? language, bool quietCheck, bool quietCause)
    {
        token.ThrowIfCancellationRequested();
        SpokenTake? sibling = fallback is null ? null : await fallback();
        TakeStats? siblingStats = sibling is null ? null : TakeStats.From(sibling);
        string measured = Measured(best.Takes, text, pace)
            + (siblingStats is null ? string.Empty : $", sibling {(int)siblingStats.VoicedMs}, sibling peak {siblingStats.Peak.ToString("0.000", CultureInfo.InvariantCulture)}")
            + Cause(quietCause);
        if (siblingStats is not null && !Failed(siblingStats, text, pace, quietCheck))
        {
            report($"silent voice output rescued by the sibling voice (voice {voiceId}, {text.Length} chars, {measured})");
            fallbacks?.Report(FallbackArea.Speech, quietCause ? FallbackReason.QuietSibling : FallbackReason.SilentSibling, language);
            return new RescueResult(sibling!, false);
        }

        fallbacks?.Report(FallbackArea.Speech, quietCause ? FallbackReason.QuietGaveUp : FallbackReason.SilentGaveUp, language);
        report($"silent voice output after {ExtraTakes + 1} takes{(fallback is null ? string.Empty : " and the sibling voice")}; gave up (voice {voiceId}, {text.Length} chars, {measured})");
        bool siblingBetter = siblingStats is not null && IsBetter(siblingStats, best.Stats, quietCheck);
        return new RescueResult(WithoutNonFinite(siblingBetter ? sibling! : best.Take), true);
    }

    // A give-up take is still played once: its NaN or infinite samples become silence, so the player never gets a sample whose PCM16 value is undefined (a click or burst).
    internal static SpokenTake WithoutNonFinite(SpokenTake take)
    {
        if (take.Samples.All(float.IsFinite))
        {
            return take;
        }

        float[] clean = take.Samples.Select(v => float.IsFinite(v) ? v : 0f).ToArray();
        return new SpokenTake(clean, take.SampleRate);
    }

    private static bool IsTooShort(double voicedMs, string text, double pace) =>
        voicedMs < NeededMs(text, pace);

    /// <summary>The voiced milliseconds a take of <paramref name="text"/> must hold (0 when it has no letter or digit).</summary>
    internal static double NeededMs(string text, double pace = NormalPace)
    {
        if (!HasSpeakableText(text))
        {
            return 0;
        }

        int letters = text.Count(char.IsLetterOrDigit);

        // A clause fragment cut at a comma ("و،") is not a sound word: it gets the per-letter floor only.
        double floor = EndsAtComma(text) ? 0 : MinVoicedMs;
        return Math.Max(floor, letters * MinVoicedMsPerLetter) / Math.Max(pace, 0.1);
    }

    private static bool EndsAtComma(string text) => text.TrimEnd() is { Length: > 0 } t && ",،".Contains(t[^1]);

    // Span between the first and last voiced sample, in ms: a click and a gap in the middle count as speech
    // only as far as they stretch, but a whole-clip near-zero trace stays at ~0.
    internal static double VoicedMs(SpokenTake take) => TakeStats.From(take).VoicedMs;
}

/// <summary>
/// What one take holds (Slice 220): sample count, NaN count, peak over the finite samples and the voiced span in ms.
/// A NaN or infinite sample (counted in <c>NanCount</c>) is never voiced and never counts toward the peak; any makes the take a failed one.
/// <c>LoudMs</c> is how long the take is at <see cref="SilentOutputGuard.SoundAmplitude"/> or louder, counting samples (not the span):
/// a click or pop adds almost nothing, where speech adds tens of ms. It is positional so a hand-built value never defaults to quiet.
/// </summary>
public sealed record TakeStats(int Samples, int NanCount, float Peak, double VoicedMs, double LoudMs)
{
    public static TakeStats From(SpokenTake take) => From(take.Samples, take.SampleRate);

    public static TakeStats From(float[] samples, int sampleRate)
    {
        int nan = 0;
        float peak = 0;
        int first = -1;
        int last = -1;
        int loud = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float v = samples[i];
            if (!float.IsFinite(v))
            {
                nan++;
                continue;
            }

            float magnitude = Math.Abs(v);
            peak = Math.Max(peak, magnitude);
            loud += magnitude >= SilentOutputGuard.SoundAmplitude ? 1 : 0;
            if (magnitude > SilentOutputGuard.VoicedAmplitude)
            {
                first = first < 0 ? i : first;
                last = i;
            }
        }

        double voicedMs = first < 0 || sampleRate <= 0 ? 0 : (last - first + 1) * 1000.0 / sampleRate;
        double loudMs = sampleRate <= 0 ? 0 : loud * 1000.0 / sampleRate;
        return new TakeStats(samples.Length, nan, peak, voicedMs, loudMs);
    }

    /// <summary>True when the take holds no speech by sound alone: its peak is under <see cref="SilentOutputGuard.MinPeak"/> or it holds under <see cref="SilentOutputGuard.MinSoundMs"/> of sound. One rule, for the guard and for ranking takes.</summary>
    public bool IsQuiet => Peak < SilentOutputGuard.MinPeak || LoudMs < SilentOutputGuard.MinSoundMs;

    /// <summary>One log fragment, no text: <c>samples N, nan N, peak 0.000, voiced ms N</c>.</summary>
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"samples {Samples}, nan {NanCount}, peak {Peak:0.000}, voiced ms {(int)VoicedMs}");
}

/// <summary>What a Kokoro take with NaN samples gets (Slice 220).</summary>
public enum KokoroNanRescue
{
    /// <summary>A NaN take is only a failed take: the plain retry runs as in Slice 210.</summary>
    Off,

    /// <summary>The retry after a NaN take runs on a single-thread Kokoro engine, created on the first NaN take.</summary>
    SingleThreadRetry,
}

/// <summary>
/// Slice 220 diagnosis probe. On the S21 (arm64) Kokoro's silent short-line takes are full length and NaN, random per
/// take; a multi-threaded arm64 kernel is the first suspect, not proven. For each short Kokoro line the probe takes
/// three plain takes on the normal engine and three on a single-thread engine and logs one line per arm, never the text.
/// </summary>
public static class ShortLineProbe
{
    /// <summary>A line with at most this many letters or digits counts as short (not for Chinese, Japanese or Korean text, where a few characters are a whole sentence).</summary>
    public const int MaxLetters = 8;

    /// <summary>Plain takes per arm.</summary>
    public const int TakesPerArm = 3;

    /// <summary>Name of the marker file the phone check creates in the app's temp folder (Android: its cache folder) to switch the probe on.</summary>
    public const string MarkerFileName = "rtst-short-line-probe";

    /// <summary>Arm names in the log line; tools/phone/checks/_lib.ps1 reads them.</summary>
    public const string MultiThreadArm = "multi-thread";

    public const string SingleThreadArm = "single-thread";

    /// <summary>
    /// True only while the marker file exists in the temp folder: the guard then runs the probe for each short Kokoro line.
    /// Normal runs, Debug or not, never probe; the probe also needs a debug log sink.
    /// </summary>
    public static bool InThisBuild => File.Exists(Path.Combine(Path.GetTempPath(), MarkerFileName));

    /// <summary>True for a short line of a Kokoro voice: the only voices that go silent on a short line.</summary>
    public static bool AppliesTo(string voiceId, string text) =>
        voiceId.StartsWith(VoiceChoices.KokoroIdPrefix, StringComparison.Ordinal)
        && !text.Any(IsCjk)
        && text.Count(char.IsLetterOrDigit) is > 0 and <= MaxLetters;

    // Kana, CJK ideographs and Hangul: one character is a syllable or a word, so the letter count says nothing about length.
    private static bool IsCjk(char c) => c is >= '぀' and <= 'ヿ' or >= '㐀' and <= '鿿' or >= '가' and <= '힯';

    /// <summary>Runs both arms (the single-thread one only when <paramref name="singleThread"/> exists) and logs one line each.</summary>
    internal static async Task RunAsync(
        string text, string voiceId, Func<string, Task<SpokenTake>> multiThread, Func<string, Task<SpokenTake>>? singleThread,
        Action<string>? debug, CancellationToken token = default)
    {
        if (debug is null)
        {
            return;
        }

        try
        {
            await RunArmAsync(MultiThreadArm, text, voiceId, multiThread, debug, token);
            if (singleThread is not null)
            {
                await RunArmAsync(SingleThreadArm, text, voiceId, singleThread, debug, token);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // no-fallback: the short line probe is a diagnostic only; no speech changes.
            debug($"short line probe failed (voice {voiceId}): {e.GetType().Name}");
        }
    }

    private static async Task RunArmAsync(
        string arm, string text, string voiceId, Func<string, Task<SpokenTake>> generate, Action<string> debug, CancellationToken token)
    {
        var described = new List<string>();
        for (int i = 0; i < TakesPerArm; i++)
        {
            token.ThrowIfCancellationRequested();
            described.Add($"[{TakeStats.From(await generate(text)).Describe()}]");
        }

        debug($"short line probe {arm} (voice {voiceId}, {text.Length} chars): {string.Join("; ", described)}");
    }
}
