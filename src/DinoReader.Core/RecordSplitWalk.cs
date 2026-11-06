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
    /// <summary>The clip that covers every sentence of <paramref name="piece"/>, or null when the piece is not (fully) recorded.</summary>
    public static StoryClipKey? ClipOf(Story story, RecordPiece piece) =>
        StoryRecordings.Clips(story).Select(c => (StoryClipKey?)c.Key)
            .FirstOrDefault(k => k!.Value.Page == piece.Key.Page && k.Value.First <= piece.Key.First && piece.Key.Last <= k.Value.Last);

    public static bool IsRecorded(Story story, RecordPiece piece) => ClipOf(story, piece) is not null;

    /// <summary>How many of <paramref name="pieces"/> are recorded.</summary>
    public static int RecordedCount(Story story, IReadOnlyList<RecordPiece> pieces) => pieces.Count(p => IsRecorded(story, p));

    /// <summary>Where "Record the rest" starts: the first piece with no recording; -1 when every piece has one ("Record again from the start" starts at 0).</summary>
    public static int FirstUnrecorded(Story story, IReadOnlyList<RecordPiece> pieces)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!IsRecorded(story, pieces[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The mode to open a book in when none is remembered: a book whose recordings are all whole pages (made before Slice 279) opens as whole pages, any other as per character.</summary>
    public static RecordMode DefaultMode(Story story)
    {
        var clips = StoryRecordings.Clips(story).ToList();
        return clips.Count > 0 && clips.All(c => c.Key.IsWholePage) ? RecordMode.WholePage : RecordMode.PerCharacter;
    }

    /// <summary>The count the book sheet shows (mock frame 188): lines recorded of all lines for a per-character book, pages recorded of all pages for a whole-page book. A page counts as recorded when every sentence of it is covered.</summary>
    public static RecordProgress Progress(Story story, RecordBook book, RecordMode mode)
    {
        if (mode == RecordMode.WholePage)
        {
            return PageProgress(story, book);
        }

        IReadOnlyList<RecordPiece> lines = RecordSplit.ByCharacter(book.Sentences, book.Speakers.Select(s => s.Id).ToHashSet());
        return new RecordProgress(RecordedCount(story, lines), lines.Count, RecordUnit.Lines);
    }

    /// <summary>How many lines of one speaker are recorded, of that speaker's lines.</summary>
    public static RecordProgress SpeakerProgress(Story story, RecordBook book, string speaker)
    {
        IReadOnlyList<RecordPiece> lines = RecordSplit.ByCharacter(book.Sentences, new HashSet<string> { speaker });
        return new RecordProgress(RecordedCount(story, lines), lines.Count, RecordUnit.Lines);
    }

    private static RecordProgress PageProgress(Story story, RecordBook book)
    {
        var pages = book.Sentences.GroupBy(s => s.Page).ToList();
        int done = pages.Count(page => page.All(s => StoryRecordings.ClipCovering(story, s.Page, s.Index) is not null));
        return new RecordProgress(done, pages.Count, RecordUnit.Pages);
    }

    /// <summary>The recordings "Listen and change" walks, in story order, each clip once (mock frame 190): in per-character mode the clips of ticked speakers, in whole-page mode all of them.
    /// A piece is the clip's own sentences, its speaker the one who says the first of them.</summary>
    public static IReadOnlyList<RecordPiece> Recorded(Story story, RecordBook book, RecordMode mode, IReadOnlySet<string> ticked)
    {
        var pieces = new List<RecordPiece>();
        foreach (StoryClipKey clip in StoryRecordings.Clips(story).Select(c => c.Key))
        {
            List<RecordSentence> covered = book.Sentences.Where(s => s.Page == clip.Page && s.Index >= clip.First && s.Index <= clip.Last).ToList();
            if (covered.Count == 0 || (mode == RecordMode.PerCharacter && !ticked.Contains(covered[0].Speaker)))
            {
                continue;
            }

            var key = new StoryClipKey(clip.Page, covered[0].Index, covered[^1].Index);
            pieces.Add(new RecordPiece(key, covered[0].Speaker, RecordSplit.TextOf(covered)));
        }

        return pieces;
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
