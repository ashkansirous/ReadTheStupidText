using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Core;

/// <summary>A pack's manifest.json is not a version-3 manifest of this app's format.</summary>
public sealed class PremadeAudioException(string message) : Exception(message);

/// <summary>
/// The pre-made audio of one book language (Slice 246 C1): the generator's manifest v3 (<c>tools/dino/premade-audio/clip_manifest.py</c>) read as a lookup from the hash of a piece's text to the clip file
/// that says it. The hash is the one the generator writes: <c>sha256(UTF-8(NFC(text)))</c> in lower-case hex, of the plain translated piece text (the displayed translation, never the
/// <c>speak:</c> table). A text with no entry has no clip and is read by the device voice. Pure: finding the file on disk is the caller's.
/// </summary>
public sealed class PremadeAudio
{
    public const int ManifestVersion = 3;
    public const string ManifestFile = "manifest.json";

    private readonly Dictionary<int, (string Hash, string Clip)> _clipByIndex;

    private PremadeAudio(string language, Dictionary<int, (string Hash, string Clip)> clipByIndex)
    {
        Language = language;
        _clipByIndex = clipByIndex;
    }

    /// <summary>The language code the manifest was made for.</summary>
    public string Language { get; }

    /// <summary>How many pieces have a clip.</summary>
    public int Count => _clipByIndex.Count;

    /// <summary>True when the manifest has a clip entry for the <paramref name="index"/>-th piece (Slice 351: entries can have holes, so a position below <see cref="Count"/> may still have none).</summary>
    public bool Has(int index) => _clipByIndex.ContainsKey(index);

    /// <summary>Every clip file name the manifest names, each once.</summary>
    public IReadOnlyCollection<string> ClipNames => _clipByIndex.Values.Select(e => e.Clip).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>True for a piece the read speaks (and the generator renders): it holds a letter or digit. The one rule for the position a clip is keyed by; the app and <c>check-audio</c> both call it.</summary>
    public static bool IsSpoken(string text) => text.Any(char.IsLetterOrDigit);

    /// <summary>How many of the piece texts are spoken: a clip's position is the count of spoken pieces before it.</summary>
    public static int SpokenCount(IEnumerable<string> texts) => texts.Count(IsSpoken);

    /// <summary>Slice 316 item 3: where a read that starts mid-book begins in the pack: the whole book's spoken pieces minus the partial read's.</summary>
    public static int ClipOffset(IEnumerable<string> wholeBookTexts, IEnumerable<string> partialReadTexts) =>
        SpokenCount(wholeBookTexts) - SpokenCount(partialReadTexts);

    /// <summary>The generator's text hash: SHA-256 of the UTF-8 bytes of the NFC form, lower-case hex.</summary>
    public static string HashOf(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Normalize(NormalizationForm.FormC))));

    /// <summary>
    /// The clip file name for the <paramref name="index"/>-th spoken piece (counting only pieces with a letter or digit, as the generator's dump does), or null when the pack has none for it or when
    /// the manifest's hash for that index is not the hash of <paramref name="text"/> (the book changed since the pack was made: the device voice reads it). Matching by position keeps the same words
    /// said by two voices apart: each has its own entry and clip.
    /// </summary>
    public string? ClipAt(int index, string text) =>
        _clipByIndex.TryGetValue(index, out (string Hash, string Clip) entry) && entry.Hash == HashOf(text) ? entry.Clip : null;

    /// <summary>Reads a manifest. Throws <see cref="PremadeAudioException"/> for anything that is not a version-3 manifest with a language and entries, or that names a clip outside the pack's own folder.</summary>
    public static PremadeAudio Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || IntOf(root, "version") != ManifestVersion)
            {
                throw new PremadeAudioException($"The manifest is not version {ManifestVersion}.");
            }

            string language = StringOf(root, "language") ?? throw new PremadeAudioException("The manifest names no language.");
            if (!root.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
            {
                throw new PremadeAudioException("The manifest has no entries.");
            }

            return new PremadeAudio(language, ClipsOf(entries));
        }
        catch (JsonException ex)
        {
            throw new PremadeAudioException("The manifest is not valid JSON: " + ex.Message);
        }
    }

    private static Dictionary<int, (string Hash, string Clip)> ClipsOf(JsonElement entries)
    {
        var clips = new Dictionary<int, (string Hash, string Clip)>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string? hash = StringOf(entry, "hash")?.ToLowerInvariant();
            string? clip = StringOf(entry, "clip");
            int? index = IntOf(entry, "index");
            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(clip) || index is null)
            {
                continue; // an entry the generator could not render has no clip
            }

            if (!IsPlainFileName(clip))
            {
                throw new PremadeAudioException($"The manifest names a clip outside the pack: {clip}");
            }

            clips.TryAdd(index.Value, (hash, clip));
        }

        return clips;
    }

    /// <summary>True for a bare file name: no folder part, no parent step, no drive.</summary>
    public static bool IsPlainFileName(string name) =>
        name.Length > 0 && name != "." && name != ".." && name.IndexOfAny(['/', '\\', ':']) < 0 && name.All(c => !char.IsControl(c));

    private static int? IntOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;

    private static string? StringOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// Marks a <see cref="VoicedSegment"/> whose text has a pre-made clip (Slice 246 C2). Everything else delegates to the segment's own transform, so when the clip cannot play the
/// segment is still read by the device voice exactly as it would have been without the marker.
/// </summary>
public sealed record PremadeClipMarker(IVoicedAudioTransform? Inner, string ClipPath) : IVoicedAudioTransform
{
    public string CacheTag => Inner?.CacheTag ?? VoicedAudio.NoTag;

    public float Pace => VoicedAudio.PaceOf(Inner);

    public TimeSpan SentenceGap => Inner?.SentenceGap ?? VoicedAudio.DefaultSentenceGap;

    public float[] Apply(float[] samples, int sampleRate) => Inner is null ? samples : Inner.Apply(samples, sampleRate);

    /// <summary>The clip file of a segment that has one, else null.</summary>
    public static string? PathOf(VoicedSegment segment) => (segment.Transform as PremadeClipMarker)?.ClipPath;

    /// <summary>The segment with the marker added (a segment that already has one is returned as it is).</summary>
    public static VoicedSegment Mark(VoicedSegment segment, string clipPath) =>
        PathOf(segment) is not null ? segment : segment with { Transform = new PremadeClipMarker(segment.Transform, clipPath) };
}

/// <summary>
/// A read as the steps the session takes in order (Slice 246 C2): speak a run with the device voice, play an animal's recording (Slice 255), or play a pre-made clip. Pieces with a clip
/// are cut out of the runs the speech engine is given, so the engine never voices them; pieces without one (and the animal slots) are planned exactly as before. Pure.
/// </summary>
public static class PremadeAudioPlan
{
    /// <summary>One step: <paramref name="Run"/> is spoken, or <paramref name="Animal"/>'s recording plays, or the <paramref name="Premade"/> segment's clip plays (the other two empty).</summary>
    public readonly record struct Step(IReadOnlyList<VoicedSegment> Run, AnimalKind? Animal, VoicedSegment? Premade);

    /// <summary>Never empty: a read with nothing to speak is one empty speak step, as <see cref="AnimalClipRuns.Plan"/> does.</summary>
    public static List<Step> Plan(IReadOnlyList<VoicedSegment> segments)
    {
        var steps = new List<Step>();
        var run = new List<VoicedSegment>();
        foreach (VoicedSegment segment in segments)
        {
            if (PremadeClipMarker.PathOf(segment) is null)
            {
                run.Add(segment);
                continue;
            }

            Flush(run, steps);
            steps.Add(new Step([], null, segment));
        }

        Flush(run, steps);
        if (steps.Count == 0)
        {
            steps.Add(new Step(segments, null, null));
        }

        return steps;
    }

    private static void Flush(List<VoicedSegment> run, List<Step> steps)
    {
        if (run.Count == 0)
        {
            return;
        }

        steps.AddRange(AnimalClipRuns.Plan(run.ToList()).Select(s => new Step(s.Run, s.Clip, null)));
        run.Clear();
    }

    /// <summary>The step as a plain speak step: used when a clip is missing or will not play, so its piece is read by the device voice.</summary>
    public static Step AsSpoken(Step step) =>
        step.Premade is { } segment ? new Step([segment], null, null) : step;
}
