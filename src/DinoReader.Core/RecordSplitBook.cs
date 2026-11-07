using ReadTheStupidText.Application.Reading;

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
        IReadOnlyList<RecordSentence> sentences = Sentences(story, text, attribution, spans);
        return new RecordBook(sentences, Speakers(attribution, sentences));
    }

    /// <summary>The book with every sentence's words replaced by the same sentence of <paramref name="translated"/> (Slice 286): the card of a recording in another story language shows the
    /// text the reader shows in that language. <see cref="TranslatedStory.Sentences"/> keeps the source's sentences one to one, so the sentence numbers a clip is keyed by do not change.</summary>
    public static RecordBook? WithTranslatedText(RecordBook book, TranslatedStory translated)
    {
        if (translated.Sentences.Count != book.Sentences.Count)
        {
            return null; // the sentences do not line up one to one: the caller reports the language as unavailable instead of showing other-language words
        }

        IReadOnlyList<RecordSentence> sentences = book.Sentences
            .Select((s, i) => s with { Text = TranslatedShown(translated.Text, translated.Sentences[i]) }).ToList();
        return book with { Sentences = sentences };
    }

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
        foreach (string id in sentences.Select(s => s.Speaker).Where(id => id != RecordSplit.Storyteller).Distinct())
        {
            string name = attribution.Characters.FirstOrDefault(c => c.Id == id)?.Name ?? id;
            speakers.Add(new RecordSpeaker(id, name));
        }

        return speakers;
    }
}
