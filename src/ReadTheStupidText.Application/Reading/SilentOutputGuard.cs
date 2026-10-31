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
        CancellationToken token = default, Func<Task<SpokenTake?>>? fallback = null, Action<string>? debug = null,
        Func<Task<SpokenTake>>? paddedGenerate = null)
    {
        SpokenTake best = await generate();
        double bestMs = VoicedMs(best);
        var allMs = new List<double> { bestMs };
        for (int attempt = 1; attempt <= ExtraTakes && IsTooShort(bestMs, text, pace); attempt++)
        {
            token.ThrowIfCancellationRequested();
            // Slice 220: the first retry stays plain (Slice 210 rescued most lines that way); only the later one is padded (see ShortLinePadding).
            bool padded = paddedGenerate is not null && attempt > 1;
            debug?.Invoke($"retry take {attempt + 1} ({(padded ? "padded" : "plain")}, voice {voiceId}, {text.Length} chars)");
            SpokenTake take = await (padded ? paddedGenerate! : generate)();
            double takeMs = VoicedMs(take);
            allMs.Add(takeMs);
            if (!IsTooShort(takeMs, text, pace))
            {
                report($"silent voice output rescued by {(padded ? "padded " : string.Empty)}take {attempt + 1} (voice {voiceId}, {text.Length} chars, {Measured(allMs, text, pace)})");
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
        Action<string>? debug = null, bool probe = false)
    {
        IReadOnlyList<Phrase> phrases = splitPhrases ? PhraseSplitter.Split(text) : [new Phrase(text, 0)];
        var takes = new List<SpokenTake>();
        bool gaveUp = false;
        foreach (Phrase phrase in phrases)
        {
            token.ThrowIfCancellationRequested();
            bool pad = ShortLinePadding.AppliesTo(voiceId, phrase.Text);
            if (pad && probe)
            {
                await ShortLinePadding.ProbeAsync(phrase.Text, voiceId, generate, debug, token);
            }

            RescueResult one = await RescueAsync(
                phrase.Text, voiceId, pace, () => generate(phrase.Text), report, token,
                fallback is null ? null : () => SiblingTakeAsync(phrase.Text, pad, fallback), debug,
                pad ? () => PaddedTakeAsync(phrase.Text, generate, debug) : null);
            takes.Add(one.Take);
            gaveUp |= one.GaveUp;
        }

        return new RescueResult(takes.Count == 1 ? takes[0] : Join(takes, phrases.Select(p => p.GapAfterMs).ToList()), gaveUp);
    }

    // The padded retry (Slice 220); its voiced density goes to the debug log (no text), so the phone run can choose a density threshold.
    private static async Task<SpokenTake> PaddedTakeAsync(string text, Func<string, Task<SpokenTake>> generate, Action<string>? debug)
    {
        SpokenTake take = await ShortLinePadding.GenerateAsync(text, ShortLinePadding.DefaultStyle, generate);
        debug?.Invoke($"padded take voiced density {ShortLinePadding.VoicedDensity(take):0.00} ({text.Length} chars)");
        return take;
    }

    // The sibling voice has the same collapse on a short line, so it speaks the padded text too (Slice 220 review).
    private static async Task<SpokenTake?> SiblingTakeAsync(string text, bool pad, Func<string, Task<SpokenTake?>> fallback)
    {
        if (!pad)
        {
            return await fallback(text);
        }

        SpokenTake? take = await fallback(ShortLinePadding.Pad(text, ShortLinePadding.DefaultStyle));
        return take is null ? null : ShortLinePadding.Trim(take);
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
    internal static double VoicedMs(SpokenTake take)
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

/// <summary>How a short line is padded before Kokoro speaks it (Slice 220).</summary>
public enum ShortLinePadStyle
{
    /// <summary>"... text ..." : pause marks either side.</summary>
    Dots,

    /// <summary>", text," : a comma either side.</summary>
    Commas,
}

/// <summary>
/// Slice 220. On arm64 Kokoro returns truly silent output (voiced ms 0 on every take) for 4-6 character lines
/// ("Ann.", "Help!"), about one read in five; the emulator never does (Slice 210). Hypothesis: a very short phoneme
/// sequence makes the model's output length collapse to nothing on arm64. Padding the text with pause marks makes
/// the sequence longer, and the sound is cut back to the voiced part afterwards. A retry of a silent short Kokoro line
/// is spoken padded; the voiced-length floor of <see cref="SilentOutputGuard"/> is unchanged.
/// </summary>
public static class ShortLinePadding
{
    /// <summary>A line with at most this many letters or digits counts as short (not for Chinese, Japanese or Korean text, where a few characters are a whole sentence).</summary>
    public const int MaxLetters = 8;

    /// <summary>The style the padded retry uses. Dots is a guess made before any phone run (tools/phone/checks/short-lines.ps1 decides; docs/plan/slices/220-...md says so).</summary>
    public const ShortLinePadStyle DefaultStyle = ShortLinePadStyle.Dots;

    /// <summary>Name of the marker file the phone check creates in the app's temp folder (Android: its cache folder) to switch the probe on.</summary>
    public const string ProbeMarkerFileName = "rtst-short-line-probe";

    /// <summary>
    /// True only while the marker file exists in the temp folder: the guard then logs the plain-versus-padded probe
    /// (3 extra syntheses) for each short Kokoro line. Normal runs, Debug or not, never probe; the probe also needs a debug log sink.
    /// </summary>
    public static bool ProbeInThisBuild => File.Exists(Path.Combine(Path.GetTempPath(), ProbeMarkerFileName));

    // Silence kept on each side of the voiced part when the padded take is cut back.
    private const int TrimMarginMs = 60;

    private const float VoicedAmplitude = 0.01f;

    /// <summary>True for a short line of a Kokoro voice: the only voices that go silent on a short line.</summary>
    public static bool AppliesTo(string voiceId, string text) =>
        voiceId.StartsWith(VoiceChoices.KokoroIdPrefix, StringComparison.Ordinal)
        && !text.Any(IsCjk)
        && text.Count(char.IsLetterOrDigit) is > 0 and <= MaxLetters;

    // Kana, CJK ideographs and Hangul: one character is a syllable or a word, so the letter count says nothing about length.
    private static bool IsCjk(char c) => c is >= '぀' and <= 'ヿ' or >= '㐀' and <= '鿿' or >= '가' and <= '힯';

    /// <summary>The text with pause marks either side. Empty or blank text comes back unchanged.</summary>
    public static string Pad(string text, ShortLinePadStyle style)
    {
        string core = text.Trim();
        if (core.Length == 0)
        {
            return text;
        }

        return style == ShortLinePadStyle.Commas ? $", {core}," : $"... {core} ...";
    }

    /// <summary>The take cut back to its voiced part plus a short margin; a take with no voiced sample is returned as it is. A lone click stays far below the guard's voiced floor; the share of voiced samples (<see cref="VoicedDensity"/>) is only logged, since no threshold is measured yet.</summary>
    public static SpokenTake Trim(SpokenTake take)
    {
        float[] s = take.Samples;
        int first = Array.FindIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        if (first < 0 || take.SampleRate <= 0)
        {
            return take;
        }

        int last = Array.FindLastIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        int margin = take.SampleRate * TrimMarginMs / 1000;
        int from = Math.Max(0, first - margin);
        int to = Math.Min(s.Length, last + 1 + margin);
        return new SpokenTake(s[from..to], take.SampleRate);
    }

    /// <summary>The share of samples above the voiced amplitude inside the take's voiced span (0 when there is none). Speech is dense; a click in silence is not.</summary>
    public static double VoicedDensity(SpokenTake take)
    {
        float[] s = take.Samples;
        int first = Array.FindIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        if (first < 0)
        {
            return 0;
        }

        int last = Array.FindLastIndex(s, v => Math.Abs(v) > VoicedAmplitude);
        int voiced = s.Skip(first).Take(last - first + 1).Count(v => Math.Abs(v) > VoicedAmplitude);
        return (double)voiced / (last - first + 1);
    }

    /// <summary>One padded take of <paramref name="text"/>, cut back to its voiced part.</summary>
    public static async Task<SpokenTake> GenerateAsync(string text, ShortLinePadStyle style, Func<string, Task<SpokenTake>> generate) =>
        Trim(await generate(Pad(text, style)));

    /// <summary>Sample count, peak amplitude and voiced span of a take: what the phone run records per line.</summary>
    public static string Describe(SpokenTake take)
    {
        float peak = take.Samples.Length == 0 ? 0 : take.Samples.Max(Math.Abs);
        double voiced = SilentOutputGuard.VoicedMs(take);
        return $"samples {take.Samples.Length}, peak {peak:0.000}, voiced ms {(int)voiced}, density {VoicedDensity(take):0.00}";
    }

    /// <summary>
    /// Debug builds only: one independent plain take and one take of each padded style, one log line, never the text.
    /// The phone run reads these to see what Kokoro returns for the same line, plain and padded.
    /// </summary>
    internal static async Task ProbeAsync(string text, string voiceId, Func<string, Task<SpokenTake>> generate, Action<string>? debug, CancellationToken token = default)
    {
        if (debug is null)
        {
            return;
        }

        try
        {
            SpokenTake plain = await generate(text);
            token.ThrowIfCancellationRequested();
            SpokenTake dots = await GenerateAsync(text, ShortLinePadStyle.Dots, generate);
            token.ThrowIfCancellationRequested();
            SpokenTake commas = await GenerateAsync(text, ShortLinePadStyle.Commas, generate);
            debug($"short line probe (voice {voiceId}, {text.Length} chars): plain [{Describe(plain)}]; dots [{Describe(dots)}]; commas [{Describe(commas)}]");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            debug($"short line probe failed (voice {voiceId}): {e.GetType().Name}");
        }
    }
}
