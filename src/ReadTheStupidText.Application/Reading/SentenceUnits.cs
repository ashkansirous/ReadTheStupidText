namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Splits a chunk into the units the sentence cache stores (Slice 47). A unit is a sentence,
/// except that a fragment shorter than <see cref="MinUnitLength"/> (an abbreviation such as
/// "Mr." that the sentence splitter cut at its full stop, or a one-word "Yes.") is joined to the
/// next unit, so the engine never speaks a stub on its own with an artificial pause after it.
/// </summary>
internal static class SentenceUnits
{
    private const int MinUnitLength = 15;

    public static IReadOnlyList<string> Split(string chunk)
    {
        var units = new List<string>();
        string pending = string.Empty;
        foreach (string sentence in SpeechTextChunker.SplitSentences(chunk))
        {
            pending = pending.Length == 0 ? sentence : $"{pending} {sentence}";
            if (pending.Length >= MinUnitLength)
            {
                units.Add(pending);
                pending = string.Empty;
            }
        }

        return AppendRemainder(units, pending, chunk);
    }

    // A short tail has no next unit to join, so it joins the previous one; a chunk that is
    // short throughout stays one unit (and an empty chunk stays a single empty-safe unit).
    private static IReadOnlyList<string> AppendRemainder(List<string> units, string pending, string chunk)
    {
        if (pending.Length > 0 && units.Count > 0)
        {
            units[^1] = $"{units[^1]} {pending}";
        }
        else if (pending.Length > 0)
        {
            units.Add(pending);
        }

        return units.Count > 0 ? units : [chunk.Trim()];
    }
}
