using System.Text.RegularExpressions;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>The animals whose name makes a character say its sound now and then (Slice 117). The order is the order a name is tried in:
/// a more specific word comes first (an owl before a cat, a rooster before a hen, a goat before a sheep).</summary>
public enum AnimalKind
{
    Owl,
    Rooster,
    Goat,
    Sheep,
    Cow,
    Cat,
    Dog,
    Duck,
    Pig,
    Hen,
    Horse,
    Frog,
    Mouse,
    Lion,
    Bee,
    Wolf,
    Rabbit,
    Bear,
    Bird,
}

/// <summary>
/// Animal characters say their sound (Slice 117, Decision 123). An animal is found from the character's name by the language's word list
/// (<see cref="AnimalSoundWords"/>), and says its sound, spoken only, after every 2nd or 3rd of its own lines, never on a line that already
/// has the sound word and never on narration. The plan is worked out over the whole story, so a read that starts later in the book still
/// sounds the same as a read from the start.
/// </summary>
public static partial class AnimalSounds
{
    private const int FirstGap = 2;
    private const int SecondGap = 3;
    private const string ArabicArticle = "ال";

    /// <summary>The animal a character's name says it is, or null (a name with no animal word in the story's language).</summary>
    public static AnimalKind? KindOf(string name, string? language)
    {
        ScriptStyle script = LanguageRules.For(language).Script;
        return AnimalSoundWords.For(language).Entries.FirstOrDefault(e => Mentions(name, e, script))?.Kind;
    }

    /// <summary>The word for an animal in a language, capitalised for a list ("Pig", "Cochon"): the first name of its table entry.</summary>
    public static string NameOf(AnimalKind kind, string? language)
    {
        string name = AnimalSoundWords.For(language).Entries.First(e => e.Kind == kind).Names[0];
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>The sound word of an animal in a language ("Baa!"), written for that language; null for an animal with no sound (a rabbit).</summary>
    public static string? SoundOf(AnimalKind kind, string? language) =>
        AnimalSoundWords.For(language).Entries.FirstOrDefault(e => e.Kind == kind)?.Sound is { Length: > 0 } sound ? sound : null;

    /// <summary>
    /// The animal of every story character that is one (Slice 125, Decision 132), by character id: the stored animal first (<see cref="CastMember.NotAnimal"/> says it is none),
    /// then the animal word in the name, then a best guess from the story <paramref name="text"/> (<see cref="AnimalGuess"/>). Animals with no sound are included.
    /// </summary>
    public static IReadOnlyDictionary<string, AnimalKind> Resolve(
        IReadOnlyList<Character> characters, string? language, IReadOnlyList<CastMember>? cast = null, string? text = null)
    {
        var animals = new Dictionary<string, AnimalKind>();
        var undecided = new List<Character>();
        foreach (Character character in characters)
        {
            CastMember? member = cast?.FirstOrDefault(m => m.CharacterId == character.Id);
            if (member is { NotAnimal: true })
            {
                continue;
            }

            if ((member?.Animal ?? KindOf(character.Name, language)) is { } kind)
            {
                animals[character.Id] = kind;
            }
            else
            {
                undecided.Add(character);
            }
        }

        if (text is not null && undecided.Count > 0)
        {
            foreach (var (id, kind) in AnimalGuess.Guess(undecided, text, language))
            {
                animals[id] = kind;
            }
        }

        return animals;
    }

    /// <summary>The characters of a story that are animals with a sound (the ones <see cref="Plan"/> works on), by character id.</summary>
    public static IReadOnlyDictionary<string, AnimalKind> AnimalsIn(
        IReadOnlyList<Character> characters, string? language, IReadOnlyList<CastMember>? cast = null, string? text = null) =>
        Resolve(characters, language, cast, text).Where(a => SoundOf(a.Value, language) is not null).ToDictionary(a => a.Key, a => a.Value);

    /// <summary>Whether <paramref name="text"/> already says the animal's sound (any stretched spelling of it: "Baaaa", "moooo"), so no sound is added after it.</summary>
    public static bool SaysIt(string text, AnimalKind kind, string? language)
    {
        AnimalSoundWords.Entry? entry = AnimalSoundWords.For(language).Entries.FirstOrDefault(e => e.Kind == kind);
        if (entry is null)
        {
            return false;
        }

        if (LanguageRules.For(language).Script == ScriptStyle.Unspaced)
        {
            return entry.Stems.Any(stem => SaysUnspaced(text, stem));
        }

        return Words(text).Any(w => entry.Stems.Any(stem => IsSpelling(w, stem)));
    }

    // No spaces to tell words apart: the sound stands alone (marks or the text's edge around it) or is said twice running, so it is not
    // found inside an ordinary word (ガーデン, ブーツ, ワンピース).
    private static bool SaysUnspaced(string text, string stem)
    {
        int at = text.IndexOf(stem, StringComparison.OrdinalIgnoreCase);
        while (at >= 0)
        {
            int end = at + stem.Length;
            bool alone = (at == 0 || !char.IsLetter(text[at - 1])) && (end >= text.Length || !char.IsLetter(text[end]));
            bool twice = string.CompareOrdinal(text, end, stem, 0, stem.Length) == 0;
            if (alone || twice)
            {
                return true;
            }

            at = text.IndexOf(stem, at + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    // The word is the sound itself, or the sound stretched ("Baaaa" for "baa"); a short everyday word that merely shrinks to the same letters is not.
    private static bool IsSpelling(string word, string stem)
    {
        string lower = word.ToLowerInvariant();
        if (lower == stem.ToLowerInvariant())
        {
            return true;
        }

        string collapsed = Collapse(lower);
        return collapsed.Length < lower.Length && collapsed == Collapse(stem);
    }

    /// <summary>
    /// Where the sounds go: the story end of the last piece of each line (a run of one animal's quote pieces) that gets a sound after it, with the animal.
    /// The end is the key because a read that starts mid-sentence only moves the start of the first piece, and translated pieces share it.
    /// It is every 2nd, then every 3rd, then every 2nd of an animal's own lines; a line that already says the sound counts as that sound.
    /// </summary>
    public static IReadOnlyDictionary<int, AnimalKind> Plan(
        IReadOnlyList<SplitSegment> pieces, IReadOnlyDictionary<string, AnimalKind> animals, string? language)
    {
        var plan = new Dictionary<int, AnimalKind>();
        if (animals.Count == 0)
        {
            return plan;
        }

        var seen = new Dictionary<string, int>();
        var gaps = new Dictionary<string, int>();
        foreach (Line line in LinesOf(pieces, animals))
        {
            int count = seen.GetValueOrDefault(line.CharacterId) + 1;
            int gap = gaps.GetValueOrDefault(line.CharacterId, FirstGap);
            seen[line.CharacterId] = count;
            if (count < gap)
            {
                continue;
            }

            seen[line.CharacterId] = 0;
            gaps[line.CharacterId] = gap == FirstGap ? SecondGap : FirstGap;
            AnimalKind kind = animals[line.CharacterId];
            if (!SaysIt(line.Text, kind, language))
            {
                plan[line.LastEnd] = kind;
            }
        }

        return plan;
    }

    /// <summary>Where one sound goes in the spoken pieces: after piece <see cref="After"/>, in the voice of the quote piece <see cref="Quote"/> that ends the line.</summary>
    public sealed record Placement(int After, int Quote, AnimalKind Kind);

    /// <summary>
    /// The sounds of <paramref name="plan"/> placed in the pieces actually spoken (the story's own, or its translation, which cuts one piece into several that
    /// share their story positions): the sound comes after the whole line, so after the last of those pieces and after the dialogue tag
    /// that finishes the quote's sentence ("Hello!" said Daddy Sheep. Baa!), never inside a translated sentence.
    /// </summary>
    public static IReadOnlyList<Placement> Placements(
        IReadOnlyList<SplitSegment> pieces, IReadOnlyDictionary<int, AnimalKind> plan, IReadOnlyList<SentenceSpan> sentences)
    {
        var placements = new List<Placement>();
        for (int i = 0; plan.Count > 0 && i < pieces.Count; i++)
        {
            Segment piece = pieces[i].Segment;
            bool lastOfLine = piece.Kind == SegmentKind.Quote && (i + 1 == pieces.Count || !SharesEnd(pieces[i + 1].Segment, piece));
            if (lastOfLine && plan.TryGetValue(piece.SourceEnd, out AnimalKind kind))
            {
                placements.Add(new Placement(AfterTag(pieces, i, sentences), i, kind));
            }
        }

        return placements;
    }

    private static bool SharesEnd(Segment next, Segment piece) =>
        next.Kind == SegmentKind.Quote && next.CharacterId == piece.CharacterId && next.SourceEnd == piece.SourceEnd;

    // The narration pieces that finish the quote's own sentence are its dialogue tag; the sound waits for them.
    private static int AfterTag(IReadOnlyList<SplitSegment> pieces, int quote, IReadOnlyList<SentenceSpan> sentences)
    {
        int sentence = StorySentences.IndexAt(sentences, pieces[quote].Segment.SourceEnd - 1);
        if (sentence < 0)
        {
            return quote;
        }

        int last = quote;
        while (last + 1 < pieces.Count && pieces[last + 1].Segment.Kind == SegmentKind.Narration && pieces[last + 1].Segment.SourceStart < sentences[sentence].End)
        {
            last++;
        }

        return last;
    }

    /// <summary>How the sound is voiced: the character's voice, a faint wobble (bleat) for a sheep or goat, higher for a lamb or kid, deeper and slower for the big animals (a grown-up deeper than a baby).</summary>
    public static VoiceFlavour FlavourFor(AnimalKind kind, VoiceFlavour lineFlavour)
    {
        bool baby = lineFlavour is VoiceFlavour.Small or VoiceFlavour.Squeaky;
        if (kind is AnimalKind.Sheep or AnimalKind.Goat)
        {
            return baby ? VoiceFlavour.BleatLamb : VoiceFlavour.Bleat;
        }

        if (baby)
        {
            return VoiceFlavour.Squeaky;
        }

        bool deepVoice = lineFlavour is VoiceFlavour.Big or VoiceFlavour.Giant or VoiceFlavour.Grumpy;
        return kind is AnimalKind.Cow or AnimalKind.Lion or AnimalKind.Wolf or AnimalKind.Bear ? (deepVoice ? VoiceFlavour.Giant : VoiceFlavour.Big) : lineFlavour;
    }

    private sealed record Line(string CharacterId, int LastEnd, string Text);

    // One line is a run of consecutive quote pieces of the same animal: a quote of several sentences is one line.
    private static IEnumerable<Line> LinesOf(IReadOnlyList<SplitSegment> pieces, IReadOnlyDictionary<string, AnimalKind> animals)
    {
        int i = 0;
        while (i < pieces.Count)
        {
            Segment first = pieces[i].Segment;
            if (first.Kind != SegmentKind.Quote || first.CharacterId is null || !animals.ContainsKey(first.CharacterId))
            {
                i++;
                continue;
            }

            int end = i;
            while (end + 1 < pieces.Count && IsSameLine(pieces[end + 1].Segment, first.CharacterId))
            {
                end++;
            }

            string text = string.Join(' ', pieces.Skip(i).Take(end - i + 1).Select(p => p.Segment.Text));
            yield return new Line(first.CharacterId, pieces[end].Segment.SourceEnd, text);
            i = end + 1;
        }
    }

    private static bool IsSameLine(Segment next, string characterId) =>
        next.Kind == SegmentKind.Quote && next.CharacterId == characterId;

    private static bool Mentions(string name, AnimalSoundWords.Entry entry, ScriptStyle script)
    {
        if (script == ScriptStyle.Unspaced)
        {
            return entry.Names.Any(w => MentionsUnspaced(name.Trim(), w));
        }

        IReadOnlyList<string> tokens = Words(name).SelectMany(WithoutArticle).ToList();
        string joined = " " + string.Join(' ', tokens).ToLowerInvariant() + " ";
        return entry.Names.Any(w => w.Contains(' ')
            ? joined.Contains(" " + w.ToLowerInvariant() + " ")
            : tokens.Contains(w, StringComparer.OrdinalIgnoreCase));
    }

    // Arabic writes "the" as a prefix: the sheep is a single word.
    private static IEnumerable<string> WithoutArticle(string word)
    {
        yield return word;
        if (word.Length > ArabicArticle.Length + 1 && word.StartsWith(ArabicArticle, StringComparison.Ordinal))
        {
            yield return word[ArabicArticle.Length..];
        }
    }

    // Without spaces a short animal word sits inside ordinary names (とり in a person's name), so the name is the word whole, or the word with a
    // curated title or diminutive around it (猫ちゃん, 小猫, 猫先生); never the word found anywhere inside the name.
    private static readonly string[] UnspacedPrefixes = ["小", "老", "大"];
    private static readonly string[] UnspacedSuffixes = ["さん", "ちゃん", "くん", "君", "様", "先生", "妈妈", "爸爸", "宝宝", "儿"];

    private static bool MentionsUnspaced(string name, string word)
    {
        if (name.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return UnspacedPrefixes.Any(p => name.Equals(p + word, StringComparison.OrdinalIgnoreCase))
            || UnspacedSuffixes.Any(x => name.Equals(word + x, StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<string> Words(string text) => LetterRun().Matches(text).Select(m => m.Value).ToList();

    // Lower-case, a repeated letter written once: "Baaaa" and "baa" are the same sound.
    internal static string Collapse(string word)
    {
        var result = new System.Text.StringBuilder(word.Length);
        foreach (char c in word.ToLowerInvariant())
        {
            if (result.Length == 0 || result[^1] != c)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+")]
    private static partial Regex LetterRun();
}
