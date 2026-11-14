namespace DinoReader.Core;

/// <summary>The result of an answer to the grown-up gate's sum.</summary>
public enum GateOutcome
{
    Passed,
    WrongRerolled,
}

/// <summary>
/// The sum behind the grown-up gate (Decision 77): two numbers from 6 to 19, added. A wrong
/// answer rolls a new sum, so a child cannot grind through guesses on one question.
/// </summary>
public sealed class GateChallenge
{
    public const int MinOperand = 6;
    public const int MaxOperand = 19;

    private readonly Random _random;

    public GateChallenge(Random? random = null)
    {
        _random = random ?? Random.Shared;
        Roll();
    }

    public int Left { get; private set; }

    public int Right { get; private set; }

    public int Answer => Left + Right;

    public GateOutcome Submit(int answer)
    {
        if (answer == Answer)
        {
            return GateOutcome.Passed;
        }

        Roll();
        return GateOutcome.WrongRerolled;
    }

    private void Roll()
    {
        Left = _random.Next(MinOperand, MaxOperand + 1);
        Right = _random.Next(MinOperand, MaxOperand + 1);
    }
}
