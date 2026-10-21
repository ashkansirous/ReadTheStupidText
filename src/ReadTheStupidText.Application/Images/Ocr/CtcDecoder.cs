using System.Collections.Concurrent;
using System.Text;

namespace ReadTheStupidText.Application.Images;

/// <summary>A decoded line and the mean probability of its characters.</summary>
public readonly record struct CtcResult(string Text, float Confidence);

/// <summary>
/// Greedy CTC decoding for PaddleOCR recognizers. The character table is index 0 = blank, then one
/// entry per dictionary line, then (when the model has one more class) a space. A model whose class
/// count fits neither layout was paired with the wrong dictionary and is refused.
/// </summary>
public sealed class CtcDecoder
{
    private const int BlankIndex = 0;
    private const string Space = " ";
    private readonly IReadOnlyList<string> _dictionary;
    private readonly ConcurrentDictionary<int, string[]> _tables = new();

    public CtcDecoder(IReadOnlyList<string> dictionary) => _dictionary = dictionary;

    /// <summary>Parses a dictionary file: one character per line, UTF-8, a trailing newline allowed.</summary>
    public static IReadOnlyList<string> ParseDictionary(string text)
    {
        List<string> lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    public CtcResult Decode(float[] scores, int steps, int classes)
    {
        string[] table = _tables.GetOrAdd(classes, BuildCharacterTable);
        var text = new StringBuilder();
        float confidenceSum = 0;
        int kept = 0;
        int previous = BlankIndex;
        for (int step = 0; step < steps; step++)
        {
            (int best, float score) = ArgMax(scores, step * classes, classes);
            if (best != BlankIndex && best != previous)
            {
                text.Append(table[best]);
                confidenceSum += score;
                kept++;
            }

            previous = best;
        }

        return new CtcResult(text.ToString(), kept == 0 ? 0 : confidenceSum / kept);
    }

    private string[] BuildCharacterTable(int classes)
    {
        bool withSpace = classes == _dictionary.Count + 2;
        if (!withSpace && classes != _dictionary.Count + 1)
        {
            throw new InvalidDataException($"The recognizer has {classes} classes but the dictionary has {_dictionary.Count} entries.");
        }

        var table = new List<string> { string.Empty };
        table.AddRange(_dictionary);
        if (withSpace)
        {
            table.Add(Space);
        }

        return table.ToArray();
    }

    private static (int Index, float Score) ArgMax(float[] scores, int offset, int count)
    {
        int best = 0;
        for (int i = 1; i < count; i++)
        {
            if (scores[offset + i] > scores[offset + best])
            {
                best = i;
            }
        }

        return (best, scores[offset + best]);
    }
}
