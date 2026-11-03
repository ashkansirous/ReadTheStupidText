using System.Text.Encodings.Web;
using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.BookBuilder;

/// <summary>One piece the app reads aloud, or one animal sound it inserts after a line (<c>Sound</c>): its position, the highlighted sentence it sits in, the text as spoken, who says it (Slice 246 A2).</summary>
internal sealed record SpokenSentence(int Index, int Sentence, string Text, string Source, string? Speaker, bool Narrator, bool Sound = false);

/// <summary>The sentences of one book in one reading language.</summary>
internal sealed record SentenceListing(string Book, string Language, IReadOnlyList<SpokenSentence> Sentences);

/// <summary>
/// <c>dump-sentences &lt;stem&gt; --lang &lt;code&gt; [--out &lt;file&gt;]</c> (Slice 246 A2): prints every piece the app reads aloud for a store book in a language, from the same code the app
/// reads with (<see cref="StoryPieces.Of"/>, <see cref="StorySentences"/>, <see cref="BundledTranslations.Lookup"/>), so clip N of the pre-made audio is piece N of the read and
/// the clip's <c>Sentence</c> is the highlight. The text is the displayed translation (plain: the <c>speak:&lt;lang&gt;</c> table is for the on-device voice only); a piece with no
/// letter or digit says nothing and is left out; a piece the language has no translation for is spoken as the source text, as the app does with no engine. The animal sounds the app adds after some lines of animal characters
/// (pigs, wolf; <c>StoryVoicing.WithAnimalSounds</c>, planned by <see cref="AnimalSounds.Plan"/>) are listed in the app's order with <c>Sound</c> true, so every segment the app speaks has an index and a clip.
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

        string json = JsonSerializer.Serialize(Build(stem, StoreBuilder.Build(Books.SpecOf(stem)), language), Options);
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

    public static SentenceListing Build(string book, Story story, string language)
    {
        LanguageRules rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        LineBreakMap breaks = story.BreakMap();
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, rules, breaks);
        AttributionResult resolved = StoryCast.Resolve(new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, breaks)), story.Cast);
        IReadOnlyList<SplitSegment> source = StoryPieces.Split(story, resolved, spans, 0);
        string key = BundledTranslations.Key(language);
        (IReadOnlyList<SplitSegment> spoken, IReadOnlyList<SplitSegment> sources) = SpeakerFirstOrder(story, source, spans, key);
        IReadOnlyDictionary<int, AnimalSounds.Placement> sounds = SoundPlacements(story, resolved, sources, spoken, UiLanguage.Resolve(language));
        var sentences = new List<SpokenSentence>();
        for (int i = 0; i < spoken.Count; i++)
        {
            if (spoken[i].Segment.Text.Any(char.IsLetterOrDigit))
            {
                sentences.Add(Spoken(story, spoken[i], sources[i].Segment.Text, spans, sentences.Count));
            }

            if (sounds.TryGetValue(i, out AnimalSounds.Placement? placement) && SoundAfter(story, spoken, placement, spans, UiLanguage.Resolve(language), sentences.Count) is { } sound)
            {
                sentences.Add(sound);
            }
        }

        return new SentenceListing(book, key, sentences);
    }

    // The pieces as the app speaks them: the displayed translation of each, regrouped speaker first in Persian and Arabic by the same code the app runs after translating (Slice 254).
    // The source texts follow the new order unchanged, so each spoken piece still names the story piece it translates.
    private static (IReadOnlyList<SplitSegment> Spoken, IReadOnlyList<SplitSegment> Sources) SpeakerFirstOrder(
        Story story, IReadOnlyList<SplitSegment> source, IReadOnlyList<SentenceSpan> spans, string key)
    {
        IReadOnlyList<SplitSegment> translated = source.Select(p => p with { Segment = p.Segment with { Text = BundledTranslations.Lookup(story, key, p.Segment.Text) ?? p.Segment.Text } }).ToList();
        // The app's own decision (SpeakerFirst.Regroups): offline it translates through the book's table only when the table covers every piece (StoryTranslator.Applies).
        bool usesTable = BundledTranslations.Has(story, key) && BundledTranslations.IsComplete(story, key);
        IReadOnlyList<SpeakerFirst.Move> order = SpeakerFirst.Plan(source, SpeakerFirst.Regroups(key, story.Language, usesTable) ? key : UiLanguage.English, story.Text, spans);
        return (SpeakerFirst.Apply(translated, order, withColon: true, story.Text), SpeakerFirst.Apply(source, order, withColon: false));
    }

    // The app's animal sounds (StoryVoicing.WithAnimalSounds, Slice 117): the plan counts the story's own lines, the placements go over the pieces as spoken. The cast's animals
    // come from AnimalSounds.Resolve (the book's stored settings, then the name, then the text), which is what AnimalSounds.OfCast gives for a cast nobody has recast on a device;
    // the grown-up's "Animal sounds" setting is taken as on (its default).
    private static IReadOnlyDictionary<int, AnimalSounds.Placement> SoundPlacements(
        Story story, AttributionResult resolved, IReadOnlyList<SplitSegment> source, IReadOnlyList<SplitSegment> spoken, string spokenLanguage)
    {
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.WithSound(AnimalSounds.Resolve(resolved.Characters, story.Language, story.CastIn(spokenLanguage), story.Text), story.Language);
        IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Plan(source, animals, story.Language);
        return AnimalSounds.Placements(spoken, plan).ToDictionary(p => p.After);
    }

    // The sound segment the app inserts after the line: the character's voice, the sound word of the spoken language, on the sentence of the line's last character;
    // none when the spoken line already says it.
    private static SpokenSentence? SoundAfter(Story story, IReadOnlyList<SplitSegment> spoken, AnimalSounds.Placement placement, IReadOnlyList<SentenceSpan> spans, string spokenLanguage, int index)
    {
        Segment quote = spoken[placement.Quote].Segment;
        string? sound = AnimalSounds.SoundOf(placement.Kind, spokenLanguage);
        if (sound is null || AnimalSounds.SaysIt(LineText(spoken, placement.Quote), placement.Kind, spokenLanguage))
        {
            return null;
        }

        int at = Math.Max(quote.SourceStart, spoken[placement.After].Segment.SourceEnd - 1);
        return new SpokenSentence(index, StorySentences.IndexAt(spans, at), sound, sound, SpeakerName(story, quote.CharacterId), Narrator: false, Sound: true);
    }

    // The spoken text of the line the piece at <last> ends: the consecutive quote pieces of the same character before it (StoryVoicing.LineText).
    private static string LineText(IReadOnlyList<SplitSegment> pieces, int last)
    {
        int first = last;
        while (first > 0 && pieces[first - 1].Segment.Kind == SegmentKind.Quote && pieces[first - 1].Segment.CharacterId == pieces[last].Segment.CharacterId)
        {
            first--;
        }

        return string.Join(' ', pieces.Skip(first).Take(last - first + 1).Select(p => p.Segment.Text));
    }

    private static string? SpeakerName(Story story, string? id) =>
        id is null ? null : story.Cast.FirstOrDefault(m => m.CharacterId == id)?.Name ?? id;

    // The highlight is the sentence holding the segment's SourceStart: what the reading screen moves to (ReadingSession.ApplyChunk) and what TranslatedStory shows the piece under.
    private static SpokenSentence Spoken(Story story, SplitSegment piece, string source, IReadOnlyList<SentenceSpan> spans, int index) =>
        new(index, StorySentences.IndexAt(spans, piece.Segment.SourceStart), piece.Segment.Text, source, SpeakerName(story, piece.Segment.CharacterId), piece.Segment.CharacterId is null);
}
