using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The sentence pieces a story is read in (one per sentence, speaker worked out in the story's own language). The reading
/// screen and the starter builder both get their pieces from here, so the keys of <see cref="Story.Translations"/> are
/// exactly the texts <see cref="StoryTranslation"/> receives at read time (Slice 95).
/// </summary>
public static class StoryPieces
{
    /// <summary>The pieces of the story read from <paramref name="fromPosition"/>, given its attribution and sentence spans.</summary>
    public static IReadOnlyList<SplitSegment> Split(
        Story story, AttributionResult resolved, IReadOnlyList<SentenceSpan> sentences, int fromPosition, bool wholePieces = false)
    {
        string text = story.Text;
        if (wholePieces)
        {
            return FromWholePiece(SegmentSentenceSplitter.Split(resolved.Segments, text, sentences), fromPosition);
        }

        IReadOnlyList<Segment> segments = SegmentSlicer.From(resolved.Segments, text, fromPosition);
        return SegmentSentenceSplitter.Split(segments, text, sentences);
    }

    /// <summary>The pieces from the one that holds <paramref name="fromPosition"/> on, that piece whole: a bundled translation is keyed
    /// by whole sentences, and a translated sentence cannot be cut at a source offset (Slice 95).</summary>
    public static IReadOnlyList<SplitSegment> FromWholePiece(IReadOnlyList<SplitSegment> all, int fromPosition)
    {
        int first = all.ToList().FindIndex(p => p.Segment.SourceEnd > fromPosition);
        return first < 0 ? [] : all.Skip(first).ToList();
    }

    /// <summary>All pieces of the story from the start, analysed from scratch (what a first read produces).</summary>
    public static IReadOnlyList<SplitSegment> Of(Story story)
    {
        LanguageRules rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        LineBreakMap breaks = story.BreakMap();
        AttributionResult attribution = new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, breaks));
        AttributionResult resolved = StoryCast.Resolve(attribution, story.Cast);
        return Split(story, resolved, StorySentences.Build(story.Text, rules, breaks), 0);
    }

    /// <summary>The distinct texts, in story order, of the pieces that have a letter (the ones that get translated).</summary>
    public static IReadOnlyList<string> TranslatablePieceTexts(Story story) =>
        Of(story).Select(p => p.Segment.Text).Where(t => t.Any(char.IsLetter)).Distinct(StringComparer.Ordinal).ToList();
}
