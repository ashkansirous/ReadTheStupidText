using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.BookBuilder;

/// <summary>One piece the app reads aloud: its position, the highlighted sentence it sits in, the text as spoken, who says it (Slice 246 A2). <c>Sound</c> is never set by the dump now (Slice 255).</summary>
internal sealed record SpokenSentence(int Index, int Sentence, string Text, string Source, string? Speaker, bool Narrator, bool Sound = false);

/// <summary>The sentences of one book in one reading language.</summary>
internal sealed record SentenceListing(string Book, string Language, IReadOnlyList<SpokenSentence> Sentences);

/// <summary>
/// <c>dump-sentences &lt;stem&gt; --lang &lt;code&gt; [--out &lt;file&gt;]</c> (Slice 246 A2): prints every piece the app reads aloud for a store book in a language, from the same code the app
/// reads with (<see cref="StoryPieces.Of"/>, <see cref="StorySentences"/>, <see cref="BundledTranslations.Lookup"/>), so clip N of the pre-made audio is piece N of the read and
/// the clip's <c>Sentence</c> is the highlight. The text is the displayed translation (plain: the <c>speak:&lt;lang&gt;</c> table is for the on-device voice only); a piece with no
/// letter or digit says nothing and is left out; a piece the language has no translation for is spoken as the source text, as the app does with no engine. The animal sounds the app adds after some lines of animal characters
/// are not listed: the app plays a bundled real recording for them (Slice 255), so no clip is rendered for them.
/// </summary>
internal static class SentenceDump
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static int Run(string? stem, string? language, string? outPath)
    {
        if (stem is null || language is null)
        {
            Console.Error.WriteLine("dump-sentences needs a book and --lang <code>, e.g. dump-sentences three-little-pigs --lang fa");
            return 1;
        }

        string json = JsonSerializer.Serialize(Build(stem, StoryOf(stem), language), Options);
        if (outPath is null)
        {
            Console.Out.WriteLine(json);
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        File.WriteAllText(outPath, json);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>Where a book's story comes from: its story.md, or the one picture book supported (Good Enough, pages from lines.json).</summary>
    internal enum DumpSource { StoryMd, GoodEnoughPictureBook }

    // The decision alone, so it is testable without a book folder: a story.md book is a text book; with no story.md only Good Enough is supported, anything else cannot be dumped.
    internal static DumpSource SourceOf(string stem, bool hasStoryMd)
    {
        if (hasStoryMd)
        {
            return DumpSource.StoryMd;
        }

        if (stem != StarterBuilder.GoodEnoughStem)
        {
            throw new InvalidOperationException($"dump-sentences: book '{stem}' has no story.md and is not the known picture book '{StarterBuilder.GoodEnoughStem}'; it cannot be dumped.");
        }

        return DumpSource.GoodEnoughPictureBook;
    }

    // A text book is built as the store builds it; Good Enough (no story.md, pages from lines.json, Slice 256) as the starter builder packs it, with its line boxes,
    // so the break map and the sentence index match what the app reads. Only Good Enough is supported as a picture book.
    internal static Story StoryOf(string stem)
    {
        if (SourceOf(stem, Books.Folder(stem).Text is not null) == DumpSource.StoryMd)
        {
            return StoreBuilder.Build(Books.SpecOf(stem));
        }

        Story? built = null;
        StarterBuilder.InScratchAsync(async library => built = StarterBuilder.WithTranslations(await StarterBuilder.BuildGoodEnoughAsync(library), stem)).GetAwaiter().GetResult();
        return built!;
    }

    public static SentenceListing Build(string book, Story story, string language)
    {
        LanguageRules rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        LineBreakMap breaks = story.BreakMap();
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, rules, breaks);
        AttributionResult resolved = StoryCast.Resolve(new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, breaks)), story.Cast);
        IReadOnlyList<SplitSegment> source = StoryPieces.Split(story, resolved, spans, 0);
        string key = BundledTranslations.Key(language);
        (IReadOnlyList<SplitSegment> spoken, IReadOnlyList<SplitSegment> sources) = SpeakerFirstOrder(story, source, spans, key);
        var sentences = new List<SpokenSentence>(); // no animal sounds: the app plays a bundled recording for them (Slice 255), so there is nothing to render
        for (int i = 0; i < spoken.Count; i++)
        {
            if (PremadeAudio.IsSpoken(spoken[i].Segment.Text))
            {
                sentences.Add(Spoken(story, spoken[i], sources[i].Segment.Text, spans, sentences.Count));
            }
        }

        return new SentenceListing(book, key, sentences);
    }

    // The pieces as the app speaks them: the displayed translation of each, joined speaker first in Persian and Arabic by the same code the app runs as it translates (SpeakerFirst.Join, Slice 280).
    // The text of a joined tag is the one it was translated as (its colon is display only), so its clip is the one it already has. The source texts follow the new order unchanged,
    // so each spoken piece still names the story piece it translates.
    private static (IReadOnlyList<SplitSegment> Spoken, IReadOnlyList<SplitSegment> Sources) SpeakerFirstOrder(
        Story story, IReadOnlyList<SplitSegment> source, IReadOnlyList<SentenceSpan> spans, string key)
    {
        IReadOnlyList<SplitSegment> translated = source.Select(p => p with { Segment = p.Segment with { Text = BundledTranslations.Lookup(story, key, p.Segment.Text) ?? p.Segment.Text } }).ToList();
        // The app's own decision (SpeakerFirst.Regroups): offline it translates through the book's table only when the table covers every piece (StoryTranslator.Applies).
        bool usesTable = BundledTranslations.Has(story, key) && BundledTranslations.IsComplete(story, key);
        if (!SpeakerFirst.Regroups(key, story.Language, usesTable))
        {
            return (translated, source);
        }

        return (SpeakerFirst.Join(translated, key, story.Language, story.Text, spans), SpeakerFirst.JoinedSourceOrder(source, key, story.Text, spans));
    }

    private static string? SpeakerName(Story story, string? id) =>
        id is null ? null : story.Cast.FirstOrDefault(m => m.CharacterId == id)?.Name ?? id;

    // The highlight is the sentence holding the segment's SourceStart: what the reading screen moves to (ReadingSession.ApplyChunk) and what TranslatedStory shows the piece under.
    private static SpokenSentence Spoken(Story story, SplitSegment piece, string source, IReadOnlyList<SentenceSpan> spans, int index) =>
        new(index, StorySentences.IndexAt(spans, piece.Segment.SourceStart), piece.Segment.Text, source, SpeakerName(story, piece.Segment.CharacterId), piece.Segment.CharacterId is null);
}
