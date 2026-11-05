using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// Marks a <see cref="VoicedSegment"/> as the place an animal's recording plays (Slice 255). The segment's text is the sound word, kept only as a label: the reading
/// session takes these segments out of what a speech engine is given (<see cref="AnimalClipRuns.Next"/>), so no engine ever voices it.
/// </summary>
public sealed record AnimalClipMarker(AnimalKind Kind) : IVoicedAudioTransform
{
    public string CacheTag => "clip-" + Kind;

    public float Pace => VoicedAudio.DefaultPace;

    public TimeSpan SentenceGap => VoicedAudio.DefaultSentenceGap;

    public float[] Apply(float[] samples, int sampleRate) => samples;
}

/// <summary>Splits a read at its animal-sound slots: the segments before the first one go to the speech engine, the recording plays, the rest follows.</summary>
public static class AnimalClipRuns
{
    /// <summary><paramref name="Run"/> is spoken; then <paramref name="Clip"/> plays (null: there is no slot, and <paramref name="Rest"/> is empty); then <paramref name="Rest"/> is split again.</summary>
    public readonly record struct Cut(IReadOnlyList<VoicedSegment> Run, AnimalKind? Clip, IReadOnlyList<VoicedSegment> Rest);

    public static AnimalKind? ClipOf(VoicedSegment segment) => (segment.Transform as AnimalClipMarker)?.Kind;

    public static Cut Next(IReadOnlyList<VoicedSegment> segments)
    {
        int slot = -1;
        for (int i = 0; i < segments.Count && slot < 0; i++)
        {
            slot = ClipOf(segments[i]) is null ? -1 : i;
        }

        if (slot < 0)
        {
            return new Cut(segments, null, []);
        }

        return new Cut(segments.Take(slot).ToList(), ClipOf(segments[slot]), segments.Skip(slot + 1).ToList());
    }

    /// <summary>The whole read as the steps the session takes in order: each step speaks its run, then plays its clip (the last step has no clip). Pure: the session's
    /// one-cut-at-a-time walk gives the same steps, including consecutive slots (an empty run between two clips) and a slot that ends the read.</summary>
    public static IReadOnlyList<(IReadOnlyList<VoicedSegment> Run, AnimalKind? Clip)> Steps(IReadOnlyList<VoicedSegment> segments)
    {
        var steps = new List<(IReadOnlyList<VoicedSegment>, AnimalKind?)>();
        IReadOnlyList<VoicedSegment> rest = segments;
        while (true)
        {
            Cut cut = Next(rest);
            if (cut.Clip is null)
            {
                if (cut.Run.Count > 0)
                {
                    steps.Add((cut.Run, null));
                }

                return steps;
            }

            steps.Add((cut.Run, cut.Clip));
            rest = cut.Rest;
        }
    }

    /// <summary>One step of a read: speak <paramref name="Run"/> (<paramref name="Clip"/> null) or play the animal's <paramref name="Clip"/> (<paramref name="Run"/> empty).</summary>
    public readonly record struct ReadStep(IReadOnlyList<VoicedSegment> Run, AnimalKind? Clip);

    /// <summary>What the session does when a step ends (or a clip file is missing).</summary>
    public enum Advance
    {
        /// <summary>The read was replaced, stopped or cancelled: the signal is dropped.</summary>
        Ignore,

        /// <summary>The read is paused: the next step waits for Resume.</summary>
        Defer,

        /// <summary>Start the next step now.</summary>
        Start,
    }

    /// <summary>The whole read as a flat list of speak / play steps in order. Never empty: a read with nothing to speak is one empty speak step.</summary>
    public static IReadOnlyList<ReadStep> Plan(IReadOnlyList<VoicedSegment> segments)
    {
        var plan = new List<ReadStep>();
        foreach ((IReadOnlyList<VoicedSegment> run, AnimalKind? clip) in Steps(segments))
        {
            if (run.Count > 0)
            {
                plan.Add(new ReadStep(run, null));
            }

            if (clip is { } kind)
            {
                plan.Add(new ReadStep([], kind));
            }
        }

        if (plan.Count == 0)
        {
            plan.Add(new ReadStep(segments, null));
        }

        return plan;
    }

    /// <summary>The one decision every end signal gets: a stale or cancelled read is ignored, a paused read waits, otherwise the next step starts.</summary>
    public static Advance Decide(bool stale, bool cancelled, bool paused) =>
        stale || cancelled ? Advance.Ignore : paused ? Advance.Defer : Advance.Start;

    /// <summary>
    /// Whether a speech engine's Completed ends the current speech step. The event carries no id, so it is accepted only when it was raised for the current run
    /// (<paramref name="stampedRun"/> equals <paramref name="currentRun"/>), that run was seen Playing (<paramref name="playingRun"/>), and the read is the active one.
    /// A late Completed of a stopped run, one raised before the run played, and a duplicate (the caller clears <paramref name="playingRun"/> after taking one) are refused.
    /// </summary>
    public static bool AcceptCompleted(int readId, int activeReadId, int stampedRun, int currentRun, int playingRun) =>
        readId != 0 && readId == activeReadId && stampedRun == currentRun && currentRun == playingRun;

    /// <summary>The kinds of the slots in <paramref name="segments"/>, each once.</summary>
    public static IReadOnlyList<AnimalKind> KindsIn(IReadOnlyList<VoicedSegment> segments) =>
        segments.Select(ClipOf).OfType<AnimalKind>().Distinct().ToList();
}

/// <summary>
/// The real recordings that stand in for an animal's voiced sound word (Slice 255): one short bundled clip per animal, played where the sound word used to
/// be spoken. Four animals (bear, lion, rabbit, wolf) have no clip and so make no sound at all, not even the sound word. Pure names only: the app finds
/// and plays the file.
/// </summary>
public static class AnimalSoundClips
{
    public const string Extension = ".m4a";

    /// <summary>The bundled asset name is this prefix plus <see cref="FileNameOf"/> (the Windows package drops folders, so the asset names are flat).</summary>
    public const string AssetPrefix = "animal-";

    private static readonly IReadOnlySet<AnimalKind> WithoutClip = new HashSet<AnimalKind> { AnimalKind.Bear, AnimalKind.Lion, AnimalKind.Rabbit, AnimalKind.Wolf };

    public static bool Has(AnimalKind kind) => !WithoutClip.Contains(kind);

    /// <summary>The animals of <paramref name="animals"/> that have a recording (the ones that get a slot, whatever language the story is read in).</summary>
    public static IReadOnlyDictionary<string, AnimalKind> WithClip(IReadOnlyDictionary<string, AnimalKind> animals) =>
        animals.Where(a => Has(a.Value)).ToDictionary(a => a.Key, a => a.Value);

    /// <summary>The animals that have a recording.</summary>
    public static IReadOnlyList<AnimalKind> Kinds => Enum.GetValues<AnimalKind>().Where(Has).ToList();

    /// <summary>The clip's file name inside <c>Resources/Raw/animal-sounds</c>, e.g. <c>pig.m4a</c>.</summary>
    public static string FileNameOf(AnimalKind kind) => kind.ToString().ToLowerInvariant() + Extension;

    /// <summary>The name the clip is bundled under in the app package, e.g. <c>animal-pig.m4a</c>.</summary>
    public static string AssetNameOf(AnimalKind kind) => AssetPrefix + FileNameOf(kind);
}
