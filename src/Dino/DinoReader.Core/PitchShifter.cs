namespace DinoReader.Core;

/// <summary>
/// Shifts the pitch of mono speech samples by a number of semitones and keeps its length (Slice 113). Pure C#, so
/// a character's flavour sounds the same on Android, iOS and Windows without an audio library (SoundTouch is LGPL).
/// The samples are resampled (pitch and length both change), then stretched back to the original length with
/// WSOLA (waveform-similarity overlap-add), which keeps speech from sounding like a warbling tape.
/// </summary>
internal static class PitchShifter
{
    private const double MinSemitones = 0.01;
    private const double FrameSeconds = 0.032;
    private const double SearchFraction = 0.25;
    private const int CorrelationStride = 4;
    private const int CandidateStep = 2;
    private const int MinFrame = 64;

    private const double WobbleHertz = 7.0;
    private const double TremoloShare = 4.0;

    /// <summary>A flavour's pitch shift and, for a bleat, its faint wobble (Slice 117); <see cref="FlavourTransform"/> calls this one step.</summary>
    public static float[] Apply(float[] samples, int sampleRate, FlavourShape shape)
    {
        float[] shifted = Shift(samples, sampleRate, shape.Semitones);
        return shape.WobbleDepth > 0 ? Wobble(shifted, sampleRate, shape.WobbleDepth) : shifted;
    }

    /// <summary>
    /// A bleat's wobble: a vibrato of about 7 Hz (the read position swings so the pitch moves by <paramref name="depth"/>, a fraction, either way)
    /// with a faint matching tremolo. Same length as the input; pure C#.
    /// </summary>
    public static float[] Wobble(float[] samples, int sampleRate, double depth)
    {
        if (samples.Length < 2 || depth <= 0)
        {
            return samples;
        }

        double angular = 2 * Math.PI * WobbleHertz / sampleRate;
        double swing = depth / angular; // samples of read-position swing that give +-depth of pitch
        var output = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double phase = Math.Sin(angular * i);
            double position = Math.Clamp(i + swing * phase, 0, samples.Length - 1);
            int index = (int)position;
            float next = index + 1 < samples.Length ? samples[index + 1] : samples[index];
            float value = samples[index] + (next - samples[index]) * (float)(position - index);
            output[i] = value * (float)(1 - depth * TremoloShare * (0.5 + 0.5 * Math.Cos(angular * i)));
        }

        return output;
    }

    /// <summary>A shifted copy of <paramref name="samples"/> of the same length (the input itself for a shift of about zero).</summary>
    public static float[] Shift(float[] samples, int sampleRate, double semitones)
    {
        if (samples.Length < MinFrame * 2 || Math.Abs(semitones) < MinSemitones)
        {
            return samples;
        }

        double ratio = Math.Pow(2, semitones / 12);
        return Stretch(Resample(samples, ratio), sampleRate, samples.Length);
    }

    // Reads the input ratio times faster: pitch goes up by the ratio and the clip gets shorter by it.
    private static float[] Resample(float[] input, double ratio)
    {
        int length = Math.Max(1, (int)(input.Length / ratio));
        var output = new float[length];
        for (int i = 0; i < length; i++)
        {
            double position = i * ratio;
            int index = (int)position;
            float fraction = (float)(position - index);
            float next = index + 1 < input.Length ? input[index + 1] : input[index];
            output[i] = input[index] + (next - input[index]) * fraction;
        }

        return output;
    }

    // WSOLA: each Hann frame is taken from near its nominal input position, nudged to where it best
    // continues the previous frame, and overlap-added at a fixed hop.
    private static float[] Stretch(float[] input, int sampleRate, int targetLength)
    {
        int frame = Math.Max(MinFrame, (int)(sampleRate * FrameSeconds)) & ~1;
        int hop = frame / 2;
        int search = (int)(frame * SearchFraction);
        double stretch = (double)targetLength / input.Length;
        float[] window = HannWindow(frame);
        var sum = new float[targetLength + frame];
        var weight = new float[targetLength + frame];
        int previous = 0;
        for (int k = 0; k * hop < targetLength; k++)
        {
            int nominal = (int)(k * hop / stretch);
            int position = k == 0 ? 0 : BestPosition(input, previous + hop, nominal, search, frame);
            AddFrame(input, position, window, sum, weight, k * hop);
            previous = position;
        }

        return Normalize(sum, weight, targetLength);
    }

    private static float[] HannWindow(int frame)
    {
        var window = new float[frame];
        for (int i = 0; i < frame; i++)
        {
            window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / frame));
        }

        return window;
    }

    // The candidate near the nominal position whose frame is most like the natural continuation of the last one.
    private static int BestPosition(float[] input, int continuation, int nominal, int search, int frame)
    {
        int best = Math.Clamp(nominal, 0, Math.Max(0, input.Length - 1));
        double bestScore = double.NegativeInfinity;
        for (int candidate = nominal - search; candidate <= nominal + search; candidate += CandidateStep)
        {
            if (candidate < 0 || candidate >= input.Length)
            {
                continue;
            }

            double score = Correlation(input, continuation, candidate, frame);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private const double NormalisationFloor = 1e-9;
    private const float WeightFloor = 1e-6f;

    // Normalised cross-correlation: the dot product divided by both frames' energy, so a loud candidate
    // does not beat a quiet one that matches the continuation's waveform better.
    private static double Correlation(float[] input, int a, int b, int frame)
    {
        if (a + frame > input.Length || b + frame > input.Length)
        {
            return double.NegativeInfinity; // a candidate that does not fully fit is never compared with full ones
        }

        double cross = 0;
        double energyA = 0;
        double energyB = 0;
        for (int i = 0; i < frame; i += CorrelationStride)
        {
            double x = input[a + i];
            double y = input[b + i];
            cross += x * y;
            energyA += x * x;
            energyB += y * y;
        }

        return cross / Math.Sqrt(energyA * energyB + NormalisationFloor);
    }

    private static void AddFrame(float[] input, int from, float[] window, float[] sum, float[] weight, int to)
    {
        for (int i = 0; i < window.Length && to + i < sum.Length; i++)
        {
            float sample = from + i < input.Length ? input[from + i] : 0f;
            sum[to + i] += sample * window[i];
            weight[to + i] += window[i];
        }
    }

    private static float[] Normalize(float[] sum, float[] weight, int length)
    {
        var output = new float[length];
        for (int i = 0; i < length; i++)
        {
            output[i] = weight[i] > WeightFloor ? sum[i] / weight[i] : 0f;
        }

        return output;
    }
}
