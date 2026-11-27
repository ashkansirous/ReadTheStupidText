using Sirous.Reading.Application.Reading;

namespace DinoReader.Core;

/// <summary>A book as the Record screen sees it: its sentences by page and the characters who speak, the Storyteller first.</summary>
public sealed record RecordBook(IReadOnlyList<RecordSentence> Sentences, IReadOnlyList<RecordSpeaker> Speakers);

/// <summary>
/// Reads a <see cref="Story"/> into a <see cref="RecordBook"/> (Slice 279) the way the reading screen does: the same sentence split, the same speaker attribution, so a clip's
/// sentence numbers mean the same sentences when the book is read back. A sentence belongs to the character whose quote it holds (the first quote with a known speaker), else to the Storyteller.
/// </summary>
public static class RecordSplitBook
{
    private const char LineBreak = '\n';
    private const char Blank = ' ';

    public static RecordBook Of(Story story)
    {
        if (story.Pages.Count == 0)
        {
            return new RecordBook([], [new RecordSpeaker(RecordSplit.Storyteller, string.Empty)]);
        }

        string text = story.Text;
        LanguageRules rules = RulesOf(story);
        IReadOnlyList<Segment> segments = new StorySegmenter(rules).Split(text, story.BreakMap());
        AttributionResult attribution = StoryCast.Resolve(new SpeakerAttributor(rules).Attribute(text, segments), story.Cast);
        IReadOnlyList<SentenceSpan> spans = SentencesOf(story);
        List<RecordSentence> plain = Sentences(story, text, attribution, spans);
        IReadOnlyList<RecordSentence> sentences = PartTakes.Enabled ? WithParts(plain, PartListIn(story, attribution, spans)) : plain; // Slice 395: no parts (so only sentence cards and sentence keys) until Slice 443 plays a part take
        return new RecordBook(sentences, Speakers(attribution, sentences));
    }

    /// <summary>The one list of a story's parts (Slice 395, review round 2): every segment of every page, in order, one entry each, numbered from 0 on its page the way the reading screen counts them, with its
    /// speaker, its words, where it lies in the story's text and the sentences (counted on the page) it sits in. Every other view of the parts derives from it: the cards (<see cref="Of"/>), whether a clip fits a
    /// page (<see cref="PartCounts(IReadOnlyList{RecordPart}, int)"/>, <see cref="PartFits"/>) and the progress counts. It is cut from the story's own text, so it is the same in every story language; a segment in no sentence stays in it (with <see cref="RecordPart.InSentences"/> false), so no index has a gap.</summary>
    public static IReadOnlyList<RecordPart> PartListOf(Story story)
    {
        if (story.Pages.Count == 0)
        {
            return [];
        }

        LanguageRules rules = RulesOf(story);
        IReadOnlyList<Segment> segments = new StorySegmenter(rules).Split(story.Text, story.BreakMap());
        AttributionResult attribution = StoryCast.Resolve(new SpeakerAttributor(rules).Attribute(story.Text, segments), story.Cast);
        return PartListIn(story, attribution, SentencesOf(story));
    }

    /// <summary>How many parts each of <paramref name="pageCount"/> pages holds in <paramref name="list"/>.</summary>
    public static IReadOnlyList<int> PartCounts(IReadOnlyList<RecordPart> list, int pageCount)
    {
        var counts = new int[pageCount];
        foreach (RecordPart part in list.Where(p => p.Page < pageCount))
        {
            counts[part.Page]++;
        }

        return counts;
    }

    /// <summary>How many parts (segments) each page of <paramref name="story"/> holds: a part clip's range is checked against it as a sentence clip's is against <see cref="SentenceCounts"/>.</summary>
    public static IReadOnlyList<int> PartCounts(Story story) => PartCounts(PartListOf(story), story.Pages.Count);

    /// <summary>Whether every part the part clip <paramref name="key"/> names is on its page in <paramref name="list"/> (else it was recorded for other words).</summary>
    public static bool PartFits(IReadOnlyList<RecordPart> list, StoryClipKey key) =>
        key.FitsPage(list.Count(p => p.Page == key.Page));

    // Every segment as a part (Slice 395): its words without quote marks, its speaker (the Storyteller for narration and for a quote nobody is known to say) and the sentences it sits in.
    private static List<RecordPart> PartListIn(Story story, AttributionResult attribution, IReadOnlyList<SentenceSpan> spans)
    {
        IReadOnlyList<int> starts = StoryRecordings.PageStarts(story.Pages);
        (int Page, int Index)[] slots = SentenceSlots(starts, spans);
        var onPage = new Dictionary<int, int>();
        var parts = new List<RecordPart>();
        int from = 0; // segments and sentences both run in story order, so the first sentence a segment can overlap never moves back
        foreach (Segment segment in attribution.Segments)
        {
            int page = StoryRecordings.PageOfPosition(starts, segment.SourceStart);
            int index = onPage.GetValueOrDefault(page);
            onPage[page] = index + 1;
            (int first, int last) = SentenceRunOf(spans, slots, ref from, segment, page);
            string speaker = segment.Kind == SegmentKind.Quote ? segment.CharacterId ?? RecordSplit.Storyteller : RecordSplit.Storyteller;
            parts.Add(new RecordPart(page, index, speaker, segment.Text.Replace(LineBreak, Blank).Trim(), first, last, segment.SourceStart, segment.SourceEnd));
        }

        return parts;
    }

    // The first and last sentence (counted on the segment's page) the segment overlaps; (-1, -1) when it overlaps none on its own page.
    private static (int First, int Last) SentenceRunOf(IReadOnlyList<SentenceSpan> spans, (int Page, int Index)[] slots, ref int from, Segment segment, int page)
    {
        while (from < spans.Count && spans[from].End <= segment.SourceStart)
        {
            from++;
        }

        int last = from;
        while (last < spans.Count && spans[last].Start < segment.SourceEnd && slots[last].Page == page)
        {
            last++;
        }

        return from < spans.Count && slots[from].Page == page && last > from ? (slots[from].Index, slots[last - 1].Index) : (-1, -1);
    }

    // Each sentence's page and its number on that page.
    private static (int Page, int Index)[] SentenceSlots(IReadOnlyList<int> starts, IReadOnlyList<SentenceSpan> spans)
    {
        var onPage = new Dictionary<int, int>();
        var slots = new (int Page, int Index)[spans.Count];
        for (int i = 0; i < spans.Count; i++)
        {
            int page = StoryRecordings.PageOfPosition(starts, spans[i].Start);
            int index = onPage.GetValueOrDefault(page);
            onPage[page] = index + 1;
            slots[i] = (page, index);
        }

        return slots;
    }

    private static List<RecordSentence> WithParts(List<RecordSentence> sentences, List<RecordPart> parts)
    {
        var byPage = parts.Where(p => p.InSentences).GroupBy(p => p.Page).ToDictionary(g => g.Key, g => g.ToList());
        return sentences.Select(s => s with { Parts = byPage.GetValueOrDefault(s.Page)?.Where(p => p.FirstSentence <= s.Index && s.Index <= p.LastSentence).ToList() ?? [] }).ToList();
    }

    /// <summary>The book with every sentence's words replaced by the same sentence of <paramref name="translated"/> (Slice 286): the card of a recording in another story language shows the
    /// text the reader shows in that language. <see cref="TranslatedStory.Sentences"/> keeps the source's sentences one to one, so the sentence numbers a clip is keyed by do not change.</summary>
    public static RecordBook? WithTranslatedText(RecordBook book, TranslatedStory translated)
    {
        if (translated.Sentences.Count != book.Sentences.Count)
        {
            return null; // the sentences do not line up one to one: the caller reports the language as unavailable instead of showing other-language words
        }

        IReadOnlySet<int> lined = PagesThatLineUp(book, translated);
        IReadOnlyList<RecordSentence> sentences = book.Sentences
            .Select((s, i) => s with { Text = TranslatedShown(translated.Text, translated.Sentences[i]), Parts = lined.Contains(s.Page) && s.Parts is { } sourceParts ? TranslatedParts(sourceParts, translated) : null }).ToList();
        return book with { Sentences = sentences };
    }

    // Slice 395, Decision 6: a page keeps its parts in the other language when every part of the source that has words found its own translated words; any other page falls back to sentence cards in that language.
    private static HashSet<int> PagesThatLineUp(RecordBook book, TranslatedStory translated)
    {
        var lined = new HashSet<int>();
        foreach (IGrouping<int, RecordSentence> page in book.Sentences.GroupBy(s => s.Page))
        {
            IReadOnlyList<RecordPart> parts = RecordSplit.PartsIn(page);
            bool found = translated.Parts is not null && page.All(s => s.Parts is not null)
                && parts.Count > 0 && parts.All(p => !p.Text.Any(char.IsLetter) || TranslatedPartText(translated, p).Length > 0);
            if (found)
            {
                lined.Add(page.Key);
            }
        }

        return lined;
    }

    private static List<RecordPart> TranslatedParts(IReadOnlyList<RecordPart> parts, TranslatedStory translated) =>
        parts.Select(p => p with { Text = TranslatedPartText(translated, p) }).ToList();

    // The translated pieces cut from this part's source range, side by side.
    private static string TranslatedPartText(TranslatedStory translated, RecordPart part) =>
        string.Join(Blank, (translated.Parts ?? []).Where(t => t.SourceStart >= part.SourceStart && t.SourceStart < part.SourceEnd).Select(t => t.Text.Replace(LineBreak, Blank).Trim()).Where(t => t.Length > 0));

    private static string TranslatedShown(string text, SentenceSpan span) =>
        span.Start >= 0 && span.End <= text.Length && span.End > span.Start ? text[span.Start..span.End].Replace(LineBreak, Blank).Trim() : string.Empty;

    /// <summary>How many sentences each page of <paramref name="story"/> holds under the reading screen's split (its language's rules and its line breaks), so a clip's sentence numbers can be checked against a page.</summary>
    public static IReadOnlyList<int> SentenceCounts(Story story)
    {
        if (story.Pages.Count == 0)
        {
            return [];
        }

        return StoryRecordings.SentencesPerPage(story.Pages, SentencesOf(story));
    }

    /// <summary>The language rules the story is cut with: recording a clip, reading it back, counting a page's sentences and moving a clip on a starter update all use these (Slice 279), so a clip's sentence
    /// numbers always mean the same sentences.</summary>
    public static LanguageRules RulesOf(Story story) => LanguageRules.For(story.Language);

    /// <summary>The sentences of the whole story, cut by <see cref="RulesOf"/> and the story's line breaks: the one function the Record screen and the reading screen both call.</summary>
    public static IReadOnlyList<SentenceSpan> SentencesOf(Story story) => StorySentences.Build(story.Text, RulesOf(story), story.BreakMap());

    private static List<RecordSentence> Sentences(Story story, string text, AttributionResult attribution, IReadOnlyList<SentenceSpan> spans)
    {
        IReadOnlyList<int> starts = StoryRecordings.PageStarts(story.Pages);
        var sentences = new List<RecordSentence>();
        var onPage = new Dictionary<int, int>();
        for (int i = 0; i < spans.Count; i++)
        {
            int page = StoryRecordings.PageOfPosition(starts, spans[i].Start);
            int index = onPage.GetValueOrDefault(page);
            onPage[page] = index + 1;
            bool last = i == spans.Count - 1 || StoryRecordings.PageOfPosition(starts, spans[i + 1].Start) != page;
            bool ends = last || text[spans[i].End..spans[i + 1].Start].Contains(LineBreak);
            sentences.Add(new RecordSentence(page, index, ShownText(text, spans[i]), SpeakerOf(attribution.Segments, spans[i]), ends));
        }

        return sentences;
    }

    // The sentence as the book shows it: a soft line wrap inside it reads as a blank.
    private static string ShownText(string text, SentenceSpan span) =>
        text[span.Start..span.End].Replace(LineBreak, Blank).Trim();

    private static string SpeakerOf(IReadOnlyList<Segment> segments, SentenceSpan span)
    {
        Segment? quote = segments.FirstOrDefault(s => s.Kind == SegmentKind.Quote && s.CharacterId is not null && s.SourceStart < span.End && s.SourceEnd > span.Start);
        return quote?.CharacterId ?? RecordSplit.Storyteller;
    }

    private static List<RecordSpeaker> Speakers(AttributionResult attribution, IReadOnlyList<RecordSentence> sentences)
    {
        var speakers = new List<RecordSpeaker> { new(RecordSplit.Storyteller, string.Empty) };
        IEnumerable<string> ids = RecordSplit.PartsIn(sentences).Select(p => p.Speaker).Concat(sentences.Select(s => s.Speaker)); // Slice 395: a character who only speaks inside another's sentence still has a list
        foreach (string id in ids.Where(id => id != RecordSplit.Storyteller).Distinct())
        {
            string name = attribution.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
            speakers.Add(new RecordSpeaker(id, name));
        }

        return speakers;
    }
}
