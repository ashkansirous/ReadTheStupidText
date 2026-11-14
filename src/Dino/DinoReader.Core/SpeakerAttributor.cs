namespace DinoReader.Core;

/// <summary>Attributed segments plus the characters found, in order of first appearance.</summary>
public sealed record AttributionResult(
    IReadOnlyList<Segment> Segments,
    IReadOnlyList<Character> Characters);

/// <summary>
/// Gives each quote a speaker by, in order: an explicit tag ("said Peter", "Peter said"),
/// a pronoun tag resolved to the most recent character of that gender, a quote that
/// continues the previous unclosed quote, and finally alternation in untagged
/// back-and-forth. A quote none of these settle keeps a null speaker.
/// </summary>
public sealed class SpeakerAttributor
{
    private readonly LanguageRules _rules;
    private readonly NameExtractor _names;
    private readonly SpeakerTagParser _tags;
    private readonly GenderGuesser _genders;

    public SpeakerAttributor(LanguageRules rules)
    {
        _rules = rules;
        _names = new NameExtractor(rules);
        _tags = new SpeakerTagParser(rules, _names);
        _genders = new GenderGuesser(rules, _names);
    }

    public AttributionResult Attribute(string text, IReadOnlyList<Segment> segments)
    {
        var run = new AttributionRun(_rules, _tags, _genders, _names.ForStory(text), segments);
        return run.Execute();
    }
}
