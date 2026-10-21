using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// Finds character names as capitalised runs ("Cotton Tail", "Mrs. Rabbit"), dropping
/// leading filler words so "Then Peter" yields "Peter". A single capitalised word that
/// starts a sentence is only a name with corroboration (see <see cref="ForStory"/>).
/// </summary>
internal sealed class NameExtractor
{
    private readonly LanguageRules _rules;
    private readonly Regex _run;
    private readonly Regex _tagged;

    public NameExtractor(LanguageRules rules)
    {
        _rules = rules;
        Pattern = BuildPattern(rules);
        _run = new Regex(Pattern, RegexOptions.CultureInvariant);
        _tagged = BuildTaggedPattern(rules, Pattern);
    }

    /// <summary>Regex source for one raw name run, for embedding in larger patterns.</summary>
    public string Pattern { get; }

    private static string BuildPattern(LanguageRules rules) => rules.Script switch
    {
        ScriptStyle.Unspaced => UnspacedPattern(rules),
        ScriptStyle.CaselessSpaced => CaselessPattern(rules),
        _ => CasedPattern(rules),
    };

    private static string CasedPattern(LanguageRules rules)
    {
        const string run = @"\p{Lu}\p{L}*(?:[ \t]+\p{Lu}\p{L}*)*";
        if (rules.TitleAbbreviations.Count == 0) return $@"\b{run}";
        var abbreviations = string.Join("|", rules.TitleAbbreviations.Select(Regex.Escape));
        return $@"\b(?:(?i:{abbreviations})\.[ \t]+)?{run}";
    }

    // Farsi and Arabic have no capitals: any word can be a name, so a name only counts when the
    // name list or a speech tag vouches for it. A title may lead ("Mr Rabbit") unless a verb follows it.
    private static string CaselessPattern(LanguageRules rules)
    {
        const string word = @"[\p{L}‌]+";
        var titles = TagPatterns.Alternation(rules.MaleTitles.Concat(rules.FemaleTitles));
        if (titles.Length == 0) return $@"\b{word}";
        return $@"\b(?:(?:{titles})[ \t]+(?!{TagPatterns.Verb(rules)}))?{word}";
    }

    // Chinese and Japanese: the name and title lists, plus the language's own name shape.
    private static string UnspacedPattern(LanguageRules rules)
    {
        var listed = TagPatterns.Alternation(rules.MaleNames.Concat(rules.FemaleNames)
            .Concat(rules.MaleTitles).Concat(rules.FemaleTitles));
        var parts = new[] { listed, rules.ExtraNamePattern }.Where(p => p.Length > 0).ToList();
        return parts.Count == 0 ? "(?!)" : $"(?:{string.Join("|", parts)})";
    }

    private static Regex BuildTaggedPattern(LanguageRules rules, string name)
    {
        var verb = TagPatterns.Verb(rules);
        var gap = TagPatterns.SubjectVerbGap(rules);
        var named = $"(?<n>{name})";
        return new Regex(
            $"(?:{verb}{TagPatterns.VerbSubjectGap(rules)}{named}|{rules.NameLead}{named}{gap}{TagPatterns.AdverbBefore(rules)}{verb})",
            RegexOptions.CultureInvariant);
    }

    /// <summary>Names in a snippet of text, with no corroboration check.</summary>
    public IEnumerable<NameMatch> Find(string text)
    {
        foreach (Match m in _run.Matches(text))
        {
            var name = Trim(m.Value).Name;
            if (name is not null) yield return new NameMatch(name, m.Index, m.Index + m.Length);
        }
    }

    public string? Normalize(string run) => Trim(run).Name;
    /// <summary>
    /// Finds the names of a whole story. A single word is accepted when it is in the name or
    /// title tables, was seen capitalised mid-sentence somewhere, or is the subject or object
    /// of a speech-verb tag ("said Lucy", "Lucy said"). Other sentence-initial words are not.
    /// </summary>
    public StoryNames ForStory(string story)
    {
        var candidates = new List<NameMatch>();
        var known = TableWords();
        foreach (Match m in _run.Matches(story))
        {
            var (name, stripped) = Trim(m.Value);
            if (name is null) continue;
            bool initial = IsInitial(story, m.Index, stripped);
            if (!initial) known.Add(name.ToLowerInvariant());
            candidates.Add(new NameMatch(name, m.Index, m.Index + m.Length));
        }
        AddTagged(story, known);
        var accepted = candidates
            .Where(c => c.Name.Contains(' ') || known.Contains(c.Name.ToLowerInvariant()))
            .ToList();
        return new StoryNames(story, accepted, known, TagPatterns.Boundary(_rules));
    }

    // Without capitals every word looks like a name, so a caseless run only counts through the name tables or a speech tag.
    private bool IsInitial(string story, int index, int stripped) => _rules.Script switch
    {
        ScriptStyle.CaselessSpaced => true,
        ScriptStyle.Cased => stripped == 0 && StartsSentence(story, index),
        _ => false,
    };

    private HashSet<string> TableWords() =>
        _rules.MaleNames.Concat(_rules.FemaleNames).Concat(_rules.MaleTitles).Concat(_rules.FemaleTitles)
            .Select(w => w.ToLowerInvariant()).ToHashSet();

    private void AddTagged(string story, HashSet<string> known)
    {
        foreach (Match m in _tagged.Matches(story))
        {
            var name = Trim(m.Groups["n"].Value).Name;
            if (name is not null) known.Add(name.ToLowerInvariant());
        }
    }

    private bool StartsSentence(string story, int index)
    {
        int j = index - 1;
        while (j >= 0 && char.IsWhiteSpace(story[j])) j--;
        if (j < 0 || ParagraphBreak.Between(story, j + 1, index)) return true;
        if (_rules.SentenceTerminators.Contains(story[j])) return true;
        return _rules.IsQuote(story[j]) && QuoteStartsSentence(story, j);
    }

    private bool QuoteStartsSentence(string story, int quote)
    {
        if (story[quote] == _rules.CurlyOpenQuote) return true;
        if (quote == 0 || char.IsWhiteSpace(story[quote - 1])) return true;
        return _rules.SentenceTerminators.Contains(story[quote - 1]);
    }

    private (string? Name, int Stripped) Trim(string run)
    {
        var words = run.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int first = 0;
        while (first < words.Length && IsNonName(words[first])) first++;
        if (first == words.Length || IsOnlyTitle(words[first..])) return (null, first);
        return (string.Join(' ', words[first..]), first);
    }

    private bool IsNonName(string word)
    {
        var bare = word.TrimEnd('.');
        return _rules.NonNameWords.Contains(bare)
            || _rules.IndefinitePronouns.Contains(bare)
            || _rules.MalePronouns.Contains(bare)
            || _rules.FemalePronouns.Contains(bare);
    }

    private bool IsOnlyTitle(string[] words) =>
        words.Length == 1 && _rules.TitleAbbreviations.Contains(words[0].TrimEnd('.'));
}

internal readonly record struct NameMatch(string Name, int Start, int End);
