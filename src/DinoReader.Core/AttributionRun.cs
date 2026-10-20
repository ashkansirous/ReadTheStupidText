namespace DinoReader.Core;

/// <summary>One pass of speaker attribution over a story; holds the running state.</summary>
internal sealed class AttributionRun(
    LanguageRules rules,
    SpeakerTagParser tags,
    GenderGuesser genders,
    StoryNames story,
    IReadOnlyList<Segment> segments)
{
    private readonly string text = story.Text;
    private readonly List<string> _recent = [];                       // most recent first
    private readonly List<string> _order = [];                        // first appearance
    private readonly Dictionary<string, Gender> _genderCache = [];
    private readonly List<(int SegmentIndex, string? Speaker)> _quotes = [];
    private readonly Dictionary<string, int> _words = [];

    public AttributionResult Execute()
    {
        var output = new List<Segment>(segments.Count);
        for (int i = 0; i < segments.Count; i++)
            output.Add(segments[i].Kind == SegmentKind.Quote ? AttributeQuote(i) : NoteNarration(i));
        var aliases = CharacterAliases.Canonicalize(_order);
        return new AttributionResult(output.Select(s => Canonical(s, aliases)).ToList(), BuildCharacters(aliases));
    }

    private static Segment Canonical(Segment segment, Dictionary<string, string> aliases) =>
        segment.CharacterId is { } id ? segment with { CharacterId = aliases[id] } : segment;

    private Segment NoteNarration(int index)
    {
        var segment = segments[index];
        foreach (var name in story.InRange(segment.SourceStart, segment.SourceEnd)) Touch(name.Name);
        return segments[index];
    }

    private Segment AttributeQuote(int index)
    {
        var speaker = ResolveSpeaker(index);
        _quotes.Add((index, speaker));
        if (speaker is null) return segments[index];
        Register(speaker, segments[index].Text);
        return segments[index] with { CharacterId = speaker };
    }

    private string? ResolveSpeaker(int index) =>
        FromTag(index) ?? FromContinuation(index) ?? FromAlternation(index);

    private string? FromTag(int index)
    {
        var tag = TagBefore(index) ?? TagAfter(index);
        if (tag is null) return null;
        return tag.Value.Name ?? MostRecentOf(tag.Value.PronounGender);
    }

    private SpeakerTag? TagAfter(int index) =>
        index + 1 < segments.Count && segments[index + 1].Kind == SegmentKind.Narration && !IsLeadInToNextQuote(index)
            ? tags.ParseAfter(segments[index + 1].Text, story)
            : null;

    /// <summary>
    /// True for `"Hello." Cotton Tail said, "Hi."`: the narration after a finished quote is an
    /// unfinished lead-in to the next quote, so it names that quote's speaker, not this one's.
    /// Narration that continues the quote's own sentence (lowercase start: `"Hello!" cried Peter, "come"`)
    /// is that quote's tag and keeps it.
    /// </summary>
    private bool IsLeadInToNextQuote(int index)
    {
        if (index + 2 >= segments.Count || segments[index + 2].Kind != SegmentKind.Quote) return false;
        var quote = segments[index].Text.TrimEnd();
        if (quote.Length == 0 || !rules.SentenceTerminators.Contains(quote[^1])) return false;
        var narration = segments[index + 1].Text;
        if (!StartsNewSentence(narration)) return false;
        return SentenceBoundaries.First(narration, rules).Length == narration.Length;
    }

    private static bool StartsNewSentence(string narration)
    {
        var trimmed = narration.TrimStart();
        return trimmed.Length > 0 && char.IsUpper(trimmed[0]);
    }

    private SpeakerTag? TagBefore(int index) =>
        index > 0 && segments[index - 1].Kind == SegmentKind.Narration
            ? tags.ParseBefore(segments[index - 1].Text, story)
            : null;

    private string? MostRecentOf(Gender gender) =>
        _recent.FirstOrDefault(name => GenderOf(name) == gender);

    private string? FromContinuation(int index)
    {
        if (index == 0 || segments[index - 1].Kind != SegmentKind.Quote) return null;
        return segments[index - 1].IsClosed ? null : _quotes[^1].Speaker;
    }

    private string? FromAlternation(int index)
    {
        if (_quotes.Count == 0 || _quotes[^1].Speaker is not { } previous) return null;
        if (GapBefore(index, _quotes[^1].SegmentIndex) > rules.DialogueGapChars) return null;
        if (!ParagraphBreak.Between(text, segments[_quotes[^1].SegmentIndex].SourceEnd, segments[index].SourceStart))
            return NewSubjectBetween(_quotes[^1].SegmentIndex, index, previous) ?? previous;
        for (int i = _quotes.Count - 2; i >= 0; i--)
        {
            if (GapBefore(_quotes[i + 1].SegmentIndex, _quotes[i].SegmentIndex) > rules.DialogueGapChars) return null;
            if (_quotes[i].Speaker is { } other && other != previous) return other;
        }
        return null;
    }

    /// <summary>A different character who is the subject of the narration between two quotes.</summary>
    private string? NewSubjectBetween(int earlierIndex, int laterIndex, string previous)
    {
        var subject = Enumerable.Range(earlierIndex + 1, laterIndex - earlierIndex - 1)
            .Where(i => segments[i].Kind == SegmentKind.Narration)
            .Select(i => NarrationSubjects.Last(story, segments[i], rules))
            .LastOrDefault(name => name is not null);
        return subject is not null && !CharacterAliases.Same(subject, previous) ? subject : null;
    }

    private int GapBefore(int laterIndex, int earlierIndex) =>
        segments[laterIndex].SourceStart - segments[earlierIndex].SourceEnd;

    private void Register(string speaker, string quoteText)
    {
        Touch(speaker);
        if (!_order.Contains(speaker)) _order.Add(speaker);
        _words[speaker] = _words.GetValueOrDefault(speaker) + WordCounter.Count(quoteText);
    }

    private void Touch(string name)
    {
        _recent.Remove(name);
        _recent.Insert(0, name);
    }

    private Gender GenderOf(string name)
    {
        if (!_genderCache.TryGetValue(name, out var gender))
            _genderCache[name] = gender = genders.Guess(name, story);
        return gender;
    }

    private List<Character> BuildCharacters(Dictionary<string, string> aliases) =>
        _order.GroupBy(n => aliases[n])
            .Select(g => new Character(g.Key, g.Key, GenderOf(g.Key), g.Sum(n => _words.GetValueOrDefault(n))))
            .ToList();
}
