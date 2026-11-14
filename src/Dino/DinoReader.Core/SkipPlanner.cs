namespace DinoReader.Core;

/// <summary>Where a skip lands and whether it starts audio there.</summary>
public readonly record struct SkipPlan(int Target, bool StartsAudio);

/// <summary>
/// Skip on the reading screen moves by one sentence, the highlight unit. Only a read that is
/// playing or still loading is restarted at the target; when nothing plays (idle, or paused) the
/// skip just moves the mark and starts no audio.
/// </summary>
public static class SkipPlanner
{
    /// <summary>The plan for a skip; the target is -1 (and nothing starts) when the story has no sentences.</summary>
    public static SkipPlan Plan(int currentSentence, int delta, int sentenceCount, bool isPlayingOrLoading)
    {
        if (sentenceCount <= 0)
        {
            return new SkipPlan(-1, false);
        }

        int target = Math.Clamp(Math.Max(0, currentSentence) + delta, 0, sentenceCount - 1);
        return new SkipPlan(target, isPlayingOrLoading);
    }
}
