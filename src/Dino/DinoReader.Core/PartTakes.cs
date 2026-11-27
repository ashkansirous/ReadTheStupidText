using Sirous.Reading.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The one switch for recording a character's own words (Slice 395, wave decision 2026-11-27). While <see cref="Enabled"/> is false the Record screen, the intro, the progress counts and saving behave as before the slice:
/// every card is a sentence card and every clip a sentence clip, so no part clip is ever created. Slice 443 turned it on together with playback of a part take (<see cref="Plan"/>), so a take that is saved always plays;
/// the switch stays as the one place to turn part takes off again.
/// </summary>
public static class PartTakes
{
    /// <summary>Whether the Record screen cuts a book into parts and the reading screen plays a part take. On since Slice 443, which plays a part take in place of only its own words; the app never sets it (tests do, in <c>PartTakesCollection</c>).</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>The take clips a read in <paramref name="reading"/> plays, each as the source text it stands in for (Slice 443): from the first segment of its first part to the end of its last part, cut from <paramref name="parts"/>
    /// (the story's one part list). A clip the parts do not name is left out (it was recorded for other words), and of two part clips that overlap the earlier one plays, so no words are heard twice.</summary>
    /// <remarks>Slice 443 review: <paramref name="stands"/> leaves out a take that must not play now (the screen shows other words than the reading language, or the take failed to play this session), as a sentence clip's <c>ClipStands</c> does.
    /// A take with no stored cover is hidden when a playing sentence clip of its page covers any sentence its parts sit in (the sentence clip wins, so no words are heard twice). Of two overlapping takes the earlier in the text plays
    /// (the Record screen's <c>ClipOf</c> picks by the same order); <paramref name="onHidden"/> gets the hidden take and the one that hides it, so the reading screen can log it.</remarks>
    public static IReadOnlyList<PartTakeSpan> SpansOf(Story story, string reading, IReadOnlyList<RecordPart> parts, Func<StoryClipKey, bool>? stands = null, Action<StoryClipKey, StoryClipKey>? onHidden = null)
    {
        IReadOnlyList<StoryClipKey> sentenceClips = StoryRecordings.PlayingClips(story, reading);
        var spans = new List<PartTakeSpan>();
        foreach (StoryClipKey key in StoryRecordings.PlayingParts(story, reading).Where(k => stands is null || stands(k)))
        {
            if (SpanOf(story, key, parts, sentenceClips) is { } span)
            {
                spans.Add(span);
            }
        }

        return WithoutOverlaps(spans, onHidden);
    }

    private static PartTakeSpan? SpanOf(Story story, StoryClipKey key, IReadOnlyList<RecordPart> parts, IReadOnlyList<StoryClipKey> sentenceClips)
    {
        RecordPart? first = parts.FirstOrDefault(p => p.Page == key.Page && p.Index == key.First);
        RecordPart? last = parts.FirstOrDefault(p => p.Page == key.Page && p.Index == key.Last);
        if (first is null || last is null || StoryRecordings.ClipOf(story, key) is not { } recording)
        {
            return null;
        }

        bool noCover = recording.Cover is not { IsValid: true };
        return noCover && SentenceClipCovers(parts, key, sentenceClips) ? null : new PartTakeSpan(key, first.SourceStart, last.SourceEnd, recording.Seconds);
    }

    // A take with no stored cover (an adopted file, an old take, a move that cleared it) is judged by the sentences its own parts sit in: a playing sentence clip that shares one of them wins.
    private static bool SentenceClipCovers(IReadOnlyList<RecordPart> parts, StoryClipKey take, IReadOnlyList<StoryClipKey> sentenceClips) =>
        parts.Where(p => p.Page == take.Page && p.Index >= take.First && p.Index <= take.Last && p.InSentences)
            .Any(p => sentenceClips.Any(s => s.Page == take.Page && s.Language == take.Language && p.FirstSentence <= s.Last && s.First <= p.LastSentence));

    private static List<PartTakeSpan> WithoutOverlaps(List<PartTakeSpan> spans, Action<StoryClipKey, StoryClipKey>? onHidden)
    {
        var kept = new List<PartTakeSpan>();
        foreach (PartTakeSpan span in spans.OrderBy(s => s.SourceStart))
        {
            if (kept.Count == 0 || span.SourceStart >= kept[^1].SourceEnd)
            {
                kept.Add(span);
                continue;
            }

            onHidden?.Invoke(span.Key, kept[^1].Key);
        }

        return kept;
    }

    /// <summary>The take whose source range holds <paramref name="sourcePosition"/>, or null.</summary>
    public static PartTakeSpan? TakeAt(IReadOnlyList<PartTakeSpan> takes, int sourcePosition) =>
        takes.Where(t => sourcePosition >= t.SourceStart && sourcePosition < t.SourceEnd).Select(t => (PartTakeSpan?)t).FirstOrDefault();

    /// <summary>The first and last sentence of the take that holds <paramref name="sentence"/> (Slice 443 review: Next and Back leave a take as one unit, as they leave a recorded page), or null when no take does.
    /// <paramref name="sentenceAt"/> maps a source position to its sentence (-1 when none).</summary>
    public static (int First, int Last)? SentenceRangeOf(IReadOnlyList<PartTakeSpan> takes, int sentence, Func<int, int> sentenceAt) =>
        takes.Select(t => (First: sentenceAt(t.SourceStart), Last: sentenceAt(t.SourceEnd - 1)))
            .Where(r => r.First >= 0 && r.Last >= r.First && r.First <= sentence && sentence <= r.Last)
            .Select(r => ((int, int)?)r).FirstOrDefault();

    /// <summary>The segments of a read that no take stands in for: the words the book's own voice really plays, which is what the read's voice count is taken on.</summary>
    public static IReadOnlyList<VoicedSegment> WithoutTakeWords(IReadOnlyList<VoicedSegment> segments, IReadOnlyList<PartTakeSpan> takes) =>
        takes.Count == 0 ? segments : segments.Where(s => TakeAt(takes, s.SourceStart) is null).ToList();

    /// <summary>Whether <paramref name="story"/> holds any part clip a read could play: the cheap test before the story's parts are cut (the segmenter and attribution over the whole book).</summary>
    public static bool HasPlayableTakes(Story story, string reading) => Enabled && StoryRecordings.PlayingParts(story, reading).Count > 0;

    /// <summary>The steps of a read of <paramref name="segments"/> with the take clips in place (Slice 443): every segment whose start lies inside a take's source range is covered by one step that plays the take,
    /// every other segment is planned as before (<see cref="PlaybackSteps.Plan"/>: its premade clip, an animal's recording, or a silent beat). A read that starts inside a take plays the take from its beginning, so a take is
    /// planned as soon as one of its segments is in the read; a take with no segment in the read plays nothing.</summary>
    public static PartTakePlan Plan(IReadOnlyList<VoicedSegment> segments, IReadOnlyList<PartTakeSpan> takes)
    {
        var steps = new List<PremadeAudioPlan.Step>();
        var placed = new Dictionary<int, PartTakeSpan>();
        var plain = new List<VoicedSegment>();
        var covered = new List<VoicedSegment>();
        PartTakeSpan? current = null;
        foreach (VoicedSegment segment in segments)
        {
            PartTakeSpan? take = takes.Where(t => segment.SourceStart >= t.SourceStart && segment.SourceStart < t.SourceEnd).Select(t => (PartTakeSpan?)t).FirstOrDefault();
            if (take != current)
            {
                FlushStretch(steps, placed, plain, covered, current);
                current = take;
            }

            (take is null ? plain : covered).Add(segment);
        }

        FlushStretch(steps, placed, plain, covered, current);
        return new PartTakePlan(steps, placed);
    }

    private static void FlushStretch(List<PremadeAudioPlan.Step> steps, Dictionary<int, PartTakeSpan> placed, List<VoicedSegment> plain, List<VoicedSegment> covered, PartTakeSpan? take)
    {
        if (plain.Count > 0)
        {
            steps.AddRange(PlaybackSteps.Plan(plain));
            plain.Clear();
        }

        if (covered.Count > 0 && take is { } span)
        {
            placed[steps.Count] = span;
            steps.Add(new PremadeAudioPlan.Step(covered.ToList(), null, null));
            covered.Clear();
        }
    }

    /// <summary>The take at <paramref name="index"/> cannot play (its file is gone or will not open): its step becomes the book's own steps for the same segments (their premade clips, an animal's recording, or a silent beat),
    /// and the takes after it move on by the steps added. Returns how many steps the take became.</summary>
    public static int ExpandTake(List<PremadeAudioPlan.Step> steps, Dictionary<int, PartTakeSpan> takes, int index)
    {
        List<PremadeAudioPlan.Step> own = PlaybackSteps.Plan(steps[index].Run);
        steps.RemoveAt(index);
        steps.InsertRange(index, own);
        int added = own.Count - 1;
        List<KeyValuePair<int, PartTakeSpan>> after = takes.Where(t => t.Key != index).ToList();
        takes.Clear();
        foreach (KeyValuePair<int, PartTakeSpan> take in after)
        {
            takes[take.Key > index ? take.Key + added : take.Key] = take.Value;
        }

        return own.Count;
    }
}

/// <summary>Slice 443: a part clip as the stretch of the story's text it stands in for (<see cref="SourceStart"/> to <see cref="SourceEnd"/>) and its length.</summary>
public readonly record struct PartTakeSpan(StoryClipKey Key, int SourceStart, int SourceEnd, int Seconds);

/// <summary>Slice 443: a read's steps and which of them (by index) play a part take instead of the book's own audio.</summary>
public sealed record PartTakePlan(List<PremadeAudioPlan.Step> Steps, Dictionary<int, PartTakeSpan> Takes);
