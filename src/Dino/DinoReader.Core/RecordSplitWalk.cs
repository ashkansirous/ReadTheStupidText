using System.Text.Json;

namespace DinoReader.Core;

/// <summary>What a count is of: spoken lines (recording per character) or pages (recording whole pages).</summary>
public enum RecordUnit
{
    Lines,
    Pages,
}

/// <summary>How much of a book is recorded: <see cref="Done"/> of <see cref="Total"/> <see cref="Unit"/>s.</summary>
public readonly record struct RecordProgress(int Done, int Total, RecordUnit Unit);

/// <summary>What a grown-up chose on the intro for one book (Slice 279): the mode and the ticked speakers, remembered per book.</summary>
public sealed record RecordChoice(RecordMode Mode, IReadOnlySet<string> Ticked);

/// <summary>
/// The pure rules for walking a book's recording pieces (Slice 279): which piece is recorded, where "Record the rest" starts, the pieces "Listen and change" visits, the counts the book sheet and the
/// intro show, and how the remembered choice is written to a preference.
/// </summary>
public static class RecordSplitWalk
{
    /// <summary>The clip, in the piece's own language (its key's, Slice 286), that covers every sentence of <paramref name="piece"/>, or null when the piece is not (fully) recorded.</summary>
    /// <remarks>Slice 395: a part piece is covered by a part clip that holds all its parts, else (Decision 5, an older recording) by a sentence clip that holds every sentence the piece lies in.
    /// A sentence piece is covered by a sentence clip, else (a page cut into sentence cards in a language whose parts do not line up with the story's, Decision 6, or a whole-page card) by the part clips
    /// that hold every part lying in its sentences: the story's one part list (<see cref="RecordSplitBook.PartListOf"/>) says which parts those are, whatever the card cut is.</remarks>
    public static StoryClipKey? ClipOf(Story story, RecordPiece piece) => ClipOfIn(story, piece, PartsOf(story));

    private static Lazy<IReadOnlyList<RecordPart>> PartsOf(Story story) => new(() => RecordSplitBook.PartListOf(story));

    private static StoryClipKey? ClipOfIn(Story story, RecordPiece piece, Lazy<IReadOnlyList<RecordPart>> parts)
    {
        List<StoryClipKey> inPage = StoryRecordings.Clips(story).Select(c => c.Key).Where(k => k.Language == piece.Key.Language && k.Page == piece.Key.Page).ToList();
        if (piece.Key.IsPart && FirstOf(FittingParts(inPage, parts).Where(k => k.First <= piece.Key.First && piece.Key.Last <= k.Last)) is { } part)
        {
            return part;
        }

        if (piece.Cover is not { } cover)
        {
            return null;
        }

        StoryClipKey? sentences = FirstOf(inPage.Where(k => !k.IsPart && k.First <= cover.First && cover.Last <= k.Last));
        return sentences ?? (piece.Key.IsPart ? null : PartClipCovering(inPage, parts, cover));
    }

    private static StoryClipKey? FirstOf(IEnumerable<StoryClipKey> keys) => keys.Select(k => (StoryClipKey?)k).FirstOrDefault();

    // The part clips of the page whose parts all exist on it; the list is cut only when the page has a part clip.
    private static List<StoryClipKey> FittingParts(List<StoryClipKey> inPage, Lazy<IReadOnlyList<RecordPart>> parts) =>
        inPage.Any(k => k.IsPart) ? inPage.Where(k => k.IsPart && RecordSplitBook.PartFits(parts.Value, k)).ToList() : [];

    // A part clip that holds every part lying in the sentences first to last, when there is at least one such part and no other clip is needed for the rest.
    private static StoryClipKey? PartClipCovering(List<StoryClipKey> inPage, Lazy<IReadOnlyList<RecordPart>> parts, StoryRecordings.SentenceCover cover)
    {
        List<StoryClipKey> clips = FittingParts(inPage, parts);
        if (clips.Count == 0)
        {
            return null;
        }

        List<RecordPart> lying = parts.Value.Where(p => p.Page == clips[0].Page && p.InSentences && p.FirstSentence <= cover.Last && cover.First <= p.LastSentence).ToList();
        return lying.Count > 0 && lying.All(p => clips.Any(k => k.First <= p.Index && p.Index <= k.Last)) ? FirstOf(clips.Where(k => lying.Any(p => k.First <= p.Index && p.Index <= k.Last))) : null;
    }

    public static bool IsRecorded(Story story, RecordPiece piece) => ClipOf(story, piece) is not null;

    /// <summary>How many of <paramref name="pieces"/> are recorded.</summary>
    public static int RecordedCount(Story story, IReadOnlyList<RecordPiece> pieces)
    {
        Lazy<IReadOnlyList<RecordPart>> parts = PartsOf(story);
        return pieces.Count(p => ClipOfIn(story, p, parts) is not null);
    }

    /// <summary>Where "Record the rest" starts: the first piece with no recording; -1 when every piece has one ("Record again from the start" starts at 0).</summary>
    public static int FirstUnrecorded(Story story, IReadOnlyList<RecordPiece> pieces)
    {
        Lazy<IReadOnlyList<RecordPart>> parts = PartsOf(story);
        for (int i = 0; i < pieces.Count; i++)
        {
            if (ClipOfIn(story, pieces[i], parts) is null)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The mode to open a book in when none is remembered: a book whose recordings are all whole pages (made before Slice 279) opens as whole pages, any other as per character.</summary>
    public static RecordMode DefaultMode(Story story, string language)
    {
        var clips = StoryRecordings.Clips(story, language).ToList();
        return clips.Count > 0 && clips.All(c => c.Key.IsWholePage) ? RecordMode.WholePage : RecordMode.PerCharacter;
    }

    /// <summary>The count the book sheet shows (mock frame 188): lines recorded of all lines for a per-character book, pages recorded of all pages for a whole-page book. A page counts as recorded when every sentence of it is covered.</summary>
    public static RecordProgress Progress(Story story, RecordBook book, RecordMode mode, string language)
    {
        if (mode == RecordMode.WholePage)
        {
            return PageProgress(story, book, language);
        }

        IReadOnlyList<RecordPiece> lines = InLanguage(RecordSplit.ByCharacter(book.Sentences, book.Speakers.Select(s => s.Id).ToHashSet()), language);
        return new RecordProgress(RecordedCount(story, lines), lines.Count, RecordUnit.Lines);
    }

    /// <summary>How many lines of one speaker are recorded in <paramref name="language"/>, of that speaker's lines.</summary>
    public static RecordProgress SpeakerProgress(Story story, RecordBook book, string speaker, string language)
    {
        IReadOnlyList<RecordPiece> lines = InLanguage(RecordSplit.ByCharacter(book.Sentences, new HashSet<string> { speaker }), language);
        return new RecordProgress(RecordedCount(story, lines), lines.Count, RecordUnit.Lines);
    }

    /// <summary><paramref name="pieces"/> with every key filed under <paramref name="language"/> (the split itself knows no language, Slice 286).</summary>
    public static IReadOnlyList<RecordPiece> InLanguage(IReadOnlyList<RecordPiece> pieces, string language) =>
        pieces.Select(p => p with { Key = p.Key.In(language) }).ToList();

    private static RecordProgress PageProgress(Story story, RecordBook book, string language)
    {
        var pages = book.Sentences.GroupBy(s => s.Page).ToList();
        Lazy<IReadOnlyList<RecordPart>> parts = PartsOf(story);
        int done = pages.Count(page => page.All(s => ClipOfIn(story, SentencePiece(s, language), parts) is not null)); // a page's sentence is covered by a sentence clip or by the part clips that hold its parts
        return new RecordProgress(done, pages.Count, RecordUnit.Pages);
    }

    private static RecordPiece SentencePiece(RecordSentence sentence, string language) =>
        new(new StoryClipKey(sentence.Page, sentence.Index, sentence.Index, StoryClipKey.NoLanguage).In(language), sentence.Speaker, sentence.Text);

    /// <summary>The recordings "Listen and change" walks, in story order, each clip once (mock frame 190): in per-character mode the clips of ticked speakers, in whole-page mode all of them.
    /// A piece is the clip's own sentences, its speaker the one who says the first of them.</summary>
    public static IReadOnlyList<RecordPiece> Recorded(Story story, RecordBook book, RecordMode mode, IReadOnlySet<string> ticked, string language)
    {
        var pieces = new List<RecordPiece>();
        IReadOnlyList<RecordPart> parts = RecordSplit.PartsIn(book.Sentences); // only which parts the book shows, and their words in its language
        Lazy<IReadOnlyList<RecordPart>> storyParts = PartsOf(story);
        foreach (StoryClipKey clip in StoryRecordings.Clips(story, language).Select(c => c.Key))
        {
            if (clip.IsPart)
            {
                AddPartPiece(pieces, story, book, parts, storyParts, clip, mode, ticked);
                continue;
            }

            List<RecordSentence> covered = book.Sentences.Where(s => s.Page == clip.Page && s.Index >= clip.First && s.Index <= clip.Last).ToList();
            if (covered.Count == 0 || (mode == RecordMode.PerCharacter && !ticked.Contains(covered[0].Speaker)))
            {
                continue;
            }

            var key = new StoryClipKey(clip.Page, covered[0].Index, covered[^1].Index, clip.Language);
            pieces.Add(new RecordPiece(key, covered[0].Speaker, RecordSplit.TextOf(covered)));
        }

        return pieces;
    }

    // A part clip is the parts it names, as the book shows them (Slice 395); one whose parts the book no longer has is skipped, as a sentence clip whose sentences are gone is.
    private static void AddPartPiece(List<RecordPiece> pieces, Story story, RecordBook book, IReadOnlyList<RecordPart> parts, Lazy<IReadOnlyList<RecordPart>> storyParts, StoryClipKey clip, RecordMode mode, IReadOnlySet<string> ticked)
    {
        // The range is read from the story's one part list (a part in no sentence counts, as PartFits counts it); the words are the book's own where it shows the part (a translated page), else the list's.
        Dictionary<int, RecordPart> shown = parts.Where(p => p.Page == clip.Page).ToDictionary(p => p.Index);
        List<RecordPart> covered = shown.Count == 0 ? [] : storyParts.Value.Where(p => p.Page == clip.Page && p.Index >= clip.First && p.Index <= clip.Last).Select(p => shown.GetValueOrDefault(p.Index) ?? p).ToList();
        if (covered.Count == 0)
        {
            AddSentencesOfPartClip(pieces, story, book, storyParts.Value, clip, mode, ticked);
            return;
        }

        if (mode == RecordMode.PerCharacter && !ticked.Contains(covered[0].Speaker))
        {
            return;
        }

        pieces.Add(RecordSplit.PartPiece(clip, covered[0].Speaker, RecordSplit.TextOfParts(covered), covered));
    }

    // Slice 395 review: a page cut into sentence cards in this language (its parts did not line up) still has its part clip; "Listen and change" shows it as the sentences it sits in, under its own key, so it plays, keeps and re-records as the clip it is.
    private static void AddSentencesOfPartClip(List<RecordPiece> pieces, Story story, RecordBook book, IReadOnlyList<RecordPart> storyParts, StoryClipKey clip, RecordMode mode, IReadOnlySet<string> ticked)
    {
        if (StoryRecordings.StoredCoverOf(story, clip) is not { } cover) // the cover stored with the clip, never re-derived from the text
        {
            return;
        }

        List<RecordSentence> sentences = book.Sentences.Where(s => s.Page == clip.Page && s.Index >= cover.First && s.Index <= cover.Last).ToList();
        RecordPart? first = storyParts.FirstOrDefault(p => p.Page == clip.Page && p.Index == clip.First); // a story update may have dropped the part the clip names: the clip stays, it is just not listed
        if (first is null)
        {
            return;
        }

        string speaker = first.Speaker;
        if (sentences.Count > 0 && (mode == RecordMode.WholePage || ticked.Contains(speaker)))
        {
            pieces.Add(new RecordPiece(clip, speaker, RecordSplit.TextOf(sentences), cover.First, cover.Last));
        }
    }

    /// <summary>The remembered choice written as text for a preference.</summary>
    public static string Write(RecordChoice choice) =>
        JsonSerializer.Serialize(new ChoiceFile { Mode = choice.Mode.ToString(), Ticked = choice.Ticked.Order(StringComparer.Ordinal).ToList() });

    /// <summary>The remembered choice read back; null for no text, a damaged one or a mode this build does not know. Ticks the book no longer has are dropped by the caller.</summary>
    public static RecordChoice? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            ChoiceFile? file = JsonSerializer.Deserialize<ChoiceFile>(text);
            return file is not null && Enum.TryParse(file.Mode, out RecordMode mode) && Enum.IsDefined(mode)
                ? new RecordChoice(mode, (file.Ticked ?? []).ToHashSet())
                : null;
        }
        catch (JsonException)
        {
            // no-fallback: a damaged preference reads as none; the intro opens on its defaults
            return null;
        }
    }

    private sealed class ChoiceFile
    {
        public string Mode { get; set; } = string.Empty;
        public List<string>? Ticked { get; set; }
    }
}
