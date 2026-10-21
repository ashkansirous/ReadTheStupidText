namespace DinoReader.Core;

/// <summary>One sentence (or dialogue line) of a story: the unit the reading screen highlights.
/// <see cref="Start"/> is inclusive and <see cref="End"/> exclusive, both indexes into the story text.</summary>
public readonly record struct SentenceSpan(int Start, int End)
{
    public int Length => End - Start;
}
