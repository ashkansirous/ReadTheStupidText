namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Splits a chunk into the units the sentence cache stores (Slice 47). A unit is a sentence,
/// except that a fragment shorter than <see cref="MinUnitLength"/> (an abbreviation such as
/// "Mr." that the sentence splitter cut at its full stop, or a one-word "Yes.") is joined to the
/// next unit, so the engine never speaks a stub on its own with an artificial pause after it.
/// </summary>
internal static class SentenceUnits
{
    internal const int MinUnitLength = 15;

    public static IReadOnlyList<string> Split(string chunk)
    {
        List<List<string>> groups = Group(chunk);
        return groups.Count > 0 ? groups.Select(g => string.Join(' ', g)).ToList() : [chunk.Trim()];
    }

    /// <summary>How many sentences each unit of <paramref name="chunk"/> holds, in order (the early start ends its head on a unit).</summary>
    public static IReadOnlyList<int> SentenceCounts(string chunk) => Group(chunk).Select(g => g.Count).ToList();

    private static List<List<string>> Group(string chunk)
    {
        var units = new List<List<string>>();
        var pending = new List<string>();
        foreach (string sentence in SpeechTextChunker.SplitSentences(chunk))
        {
            pending.Add(sentence);
            if (string.Join(' ', pending).Length >= MinUnitLength)
            {
                units.Add(pending);
                pending = [];
            }
        }

        AppendRemainder(units, pending);
        return units;
    }

    // A short tail has no next unit to join, so it joins the previous one; a chunk that is
    // short throughout stays one unit.
    private static void AppendRemainder(List<List<string>> units, List<string> pending)
    {
        if (pending.Count == 0)
        {
            return;
        }

        if (units.Count > 0)
        {
            units[^1].AddRange(pending);
        }
        else
        {
            units.Add(pending);
        }
    }
}
