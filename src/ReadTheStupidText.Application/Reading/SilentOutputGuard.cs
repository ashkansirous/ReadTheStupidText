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
    private const float VoicedAmplitude = 0.01f;

    // Slice 145 run5: a dropped mana word measured 0-97 ms of sound, the shortest legitimate clip (gyro «ووش!») 113 ms.
    // The floor is for pace 1.0; the engine's Speed shrinks a clip, so it scales with 1 / pace.
    private const double MinVoicedMs = 80;

    // Far fewer ms per letter than any voice speaks: the quickest legitimate clips (ganji at pace 1.15, es Supertonic
    // carrier) measured 54-64 ms per letter, gyro's 3-letter «ووش!» 37.6. 30 gave 0 false rescues in 972 clips of
    // ganji, ganji-adabi, gyro, Supertonic and Kokoro (en, es, zh, fa; paces 0.85-1.15).
    private const double MinVoicedMsPerLetter = 30;

    private const double NormalPace = 1.0;

    // A first take that passes under this many times the floor is a near miss, sent to the debug log only.
    private const double NearMissFactor = 1.25;

    /// <summary>True when the take holds no speech, or far too little, for text that has something to say.</summary>
    public static bool IsSilent(SpokenTake take, string text, double pace = NormalPace) =>
        IsTooShort(VoicedMs(take), text, pace);

    /// <summary>
    /// Runs <paramref name="generate"/>, and again while the take is silent (at most <see cref="ExtraTakes"/>
    /// more times), then once through <paramref name="fallback"/> (a sibling voice; it returns null when there is
    /// none). Returns the first voiced take, else the one with the most sound (and <see cref="RescueResult.GaveUp"/>);
    /// <paramref name="report"/> gets one line per rescue or give-up, naming <paramref name="voiceId"/>.
    /// A cancelled <paramref name="token"/> stops between takes.
    /// </summary>
    public static async Task<RescueResult> RescueAsync(
        string text, string voiceId, double pace, Func<Task<SpokenTake>> generate, Action<string> report,
        CancellationToken token = default, Func<Task<SpokenTake?>>? fallback = null, Action<string>? debug = null)
    {
        SpokenTake best = await generate();
        double bestMs = VoicedMs(best);
        var allMs = new List<double> { bestMs };
        for (int attempt = 1; attempt <= ExtraTakes && IsTooShort(bestMs, text, pace); attempt++)
        {
            token.ThrowIfCancellationRequested();
            SpokenTake take = await generate();
            double takeMs = VoicedMs(take);
            allMs.Add(takeMs);
            if (!IsTooShort(takeMs, text, pace))
            {
                report($"silent voice output rescued by take {attempt + 1} (voice {voiceId}, {text.Length} chars, {Measured(allMs, text, pace)})");
                return new RescueResult(take, false);
            }

            if (takeMs > bestMs)
            {
                (best, bestMs) = (take, takeMs);
            }
        }

        if (IsTooShort(bestMs, text, pace))
        {
            return await TrySiblingAsync(text, voiceId, pace, (best, bestMs, allMs), fallback, report, token);
        }

        ReportNearMiss(text, voiceId, pace, allMs, debug);
        return new RescueResult(best, false);
    }

    // A first take that passed with under 1.25 times the floor (Slice 210; debug log only, so it cannot flood the activity log): the device run reads these to choose a floor.
    private static void ReportNearMiss(string text, string voiceId, double pace, List<double> takesMs, Action<string>? debug)
    {
        if (debug is not null && takesMs[0] < NeededMs(text, pace) * NearMissFactor)
        {
            debug($"voice output passed near the floor (voice {voiceId}, {text.Length} chars, {Measured(takesMs, text, pace)})");
        }
    }

    // The measure behind a rescue line (Slice 210): voiced ms of each take and the floor it was held to. No text.
    private static string Measured(List<double> takesMs, string text, double pace) =>
        $"voiced ms {string.Join("/", takesMs.Select(m => (int)m))}, needed {(int)NeededMs(text, pace)}, pace {pace:0.00}";

    /// <summary>
    /// As <see cref="RescueAsync"/> for a whole unit. With <paramref name="splitPhrases"/> (a drop-prone voice) the unit
    /// is spoken phrase by phrase (<see cref="PhraseSplitter"/>), each phrase guarded and rescued on its own, and the
    /// takes are joined with a short silence, so a sound word glued to a sentence cannot vanish unseen. Other voices
    /// speak the unit whole. <see cref="RescueResult.GaveUp"/> is true when any phrase gave up.
    /// </summary>
    public static async Task<RescueResult> RescueUnitAsync(
        string text, string voiceId, double pace, bool splitPhrases, Func<string, Task<SpokenTake>> generate,
        Action<string> report, CancellationToken token = default, Func<string, Task<SpokenTake?>>? fallback = null,
        Action<string>? debug = null)
    {
        IReadOnlyList<Phrase> phrases = splitPhrases ? PhraseSplitter.Split(text) : [new Phrase(text, 0)];
        var takes = new List<SpokenTake>();
        bool gaveUp = false;
        foreach (Phrase phrase in phrases)
        {
            token.ThrowIfCancellationRequested();
            RescueResult one = await RescueAsync(
                phrase.Text, voiceId, pace, () => generate(phrase.Text), report, token,
                fallback is null ? null : () => fallback(phrase.Text), debug);
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
        string text, string voiceId, double pace, (SpokenTake Take, double Ms, List<double> TakesMs) best, Func<Task<SpokenTake?>>? fallback,
        Action<string> report, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        SpokenTake? sibling = fallback is null ? null : await fallback();
        double siblingMs = sibling is null ? 0 : VoicedMs(sibling);
        string measured = Measured(best.TakesMs, text, pace) + (sibling is null ? string.Empty : $", sibling {(int)siblingMs}");
        if (sibling is not null && !IsTooShort(siblingMs, text, pace))
        {
            report($"silent voice output rescued by the sibling voice (voice {voiceId}, {text.Length} chars, {measured})");
            return new RescueResult(sibling, false);
        }

        report($"silent voice output after {ExtraTakes + 1} takes{(fallback is null ? string.Empty : " and the sibling voice")}; gave up (voice {voiceId}, {text.Length} chars, {measured})");
        return new RescueResult(sibling is not null && siblingMs > best.Ms ? sibling : best.Take, true);
    }

    private static bool IsTooShort(double voicedMs, string text, double pace) =>
        voicedMs < NeededMs(text, pace);

    /// <summary>The voiced milliseconds a take of <paramref name="text"/> must hold (0 when it has no letter or digit).</summary>
    internal static double NeededMs(string text, double pace = NormalPace)
    {
        int letters = text.Count(char.IsLetterOrDigit);
        if (letters == 0)
        {
            return 0;
        }

        // A clause fragment cut at a comma ("و،") is not a sound word: it gets the per-letter floor only.
        double floor = EndsAtComma(text) ? 0 : MinVoicedMs;
        return Math.Max(floor, letters * MinVoicedMsPerLetter) / Math.Max(pace, 0.1);
    }

    private static bool EndsAtComma(string text) => text.TrimEnd() is { Length: > 0 } t && ",،".Contains(t[^1]);

    // Span between the first and last voiced sample, in ms: a click and a gap in the middle count as speech
    // only as far as they stretch, but a whole-clip near-zero trace stays at ~0.
    private static double VoicedMs(SpokenTake take)
    {
        float[] s = take.Samples;
        int first = Array.FindIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        if (first < 0 || take.SampleRate <= 0)
        {
            return 0;
        }

        int last = Array.FindLastIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        return (last - first + 1) * 1000.0 / take.SampleRate;
    }
}
