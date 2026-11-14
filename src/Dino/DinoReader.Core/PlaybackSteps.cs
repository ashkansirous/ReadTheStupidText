using Sirous.Reading.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// A read as the steps the reading session takes in order (Slice 278 part B). No speech engine exists: a step either plays a pre-made clip of one piece, plays an animal's recording
/// (Slice 255), or is one piece with no clip, which the session shows and highlights for a short beat before it goes on. Pieces with no letter or digit have nothing to say or show a beat for
/// and are dropped. Pure, so it is tested without the app.
/// </summary>
public static class PlaybackSteps
{
    /// <summary>How long a piece with no clip stays marked before the read goes on: a short fixed beat, whatever the reading speed.</summary>
    public const int BeatMilliseconds = 1200;

    /// <summary>
    /// The steps of <paramref name="segments"/>. A run of pieces with no clip becomes one step per piece (each is a <see cref="PremadeAudioPlan.Step"/> with a one-piece run, no animal and no clip), so
    /// the highlight moves a piece at a time. May be empty when nothing in the read has a letter or a clip.
    /// </summary>
    public static List<PremadeAudioPlan.Step> Plan(IReadOnlyList<VoicedSegment> segments)
    {
        var steps = new List<PremadeAudioPlan.Step>();
        foreach (PremadeAudioPlan.Step step in PremadeAudioPlan.Plan(segments))
        {
            if (step.Animal is not null || step.Premade is not null)
            {
                steps.Add(step);
                continue;
            }

            steps.AddRange(step.Run.Where(piece => PremadeAudio.IsSpoken(piece.Text)).Select(piece => new PremadeAudioPlan.Step([piece], null, null)));
        }

        return steps;
    }

    /// <summary>True for a step that is one piece with no clip: nothing plays for it, it is shown for a beat.</summary>
    public static bool IsSilentPiece(PremadeAudioPlan.Step step) => step.Animal is null && step.Premade is null && step.Run.Count > 0;

    /// <summary>The step as a silent piece: used when its clip is gone or will not play, so the piece is shown for a beat instead.</summary>
    public static PremadeAudioPlan.Step AsSilentPiece(PremadeAudioPlan.Step step) => PremadeAudioPlan.AsSpoken(step);
}
