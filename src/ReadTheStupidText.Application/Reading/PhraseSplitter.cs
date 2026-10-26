using System.Text;

namespace ReadTheStupidText.Application.Reading;

/// <summary>One part of a unit, and the silence that follows it when the parts are joined.</summary>
public sealed record Phrase(string Text, int GapAfterMs);

/// <summary>
/// Splits a unit at its sentence and clause marks (<c>. ! ? … , ،</c>) into phrases a drop-prone voice speaks one
/// at a time (Slice 149): a sound word glued to a sentence ("Pip ran away. تق!") then stands alone, where the
/// silent-output guard can see it drop. This is only for the synthesis path; chunking and the cache keep the whole unit.
/// </summary>
public static class PhraseSplitter
{
    /// <summary>The silence after a sentence-final mark (the app's inter-unit gap is about this long).</summary>
    public const int SentenceGapMs = 150;

    /// <summary>The shorter silence after a comma.</summary>
    public const int CommaGapMs = 90;

    /// <summary>A phrase of this many letters or fewer is short enough to be a sound word the voice may drop.</summary>
    public const int ShortPhraseLetters = 6;

    private const string Terminators = ".!?…,،؟";
    private const string Commas = ",،";

    /// <summary>The phrases of <paramref name="text"/> in order; a mark-only piece joins its neighbour. Never empty for non-blank text.</summary>
    public static IReadOnlyList<Phrase> Split(string text)
    {
        List<string> pieces = MergeLongClauses(Pieces(text));
        var phrases = new List<Phrase>();
        string carry = string.Empty;
        foreach (string piece in pieces)
        {
            string joined = carry.Length == 0 ? piece : carry + " " + piece;
            if (!joined.Any(char.IsLetterOrDigit))
            {
                carry = joined;
                continue;
            }

            carry = string.Empty;
            phrases.Add(new Phrase(joined, GapAfter(joined)));
        }

        if (carry.Length > 0 && phrases.Count > 0)
        {
            phrases[^1] = new Phrase(phrases[^1].Text + " " + carry, phrases[^1].GapAfterMs);
        }

        return phrases.Count > 0 ? phrases : [new Phrase(text, SentenceGapMs)];
    }

    // A comma splits only around a short phrase (a likely-dropped sound word); two normal clauses stay one utterance,
    // so their prosody and the time to first sound are as before.
    private static List<string> MergeLongClauses(List<string> pieces)
    {
        var merged = new List<string>();
        foreach (string piece in pieces)
        {
            if (merged.Count > 0 && Commas.Contains(merged[^1][^1]) && !IsShort(merged[^1]) && !IsShort(piece))
            {
                merged[^1] = merged[^1] + " " + piece;
            }
            else
            {
                merged.Add(piece);
            }
        }

        return merged;
    }

    private static bool IsShort(string phrase) => phrase.Count(char.IsLetterOrDigit) <= ShortPhraseLetters;

    private static int GapAfter(string phrase) => Commas.Contains(phrase[^1]) ? CommaGapMs : SentenceGapMs;

    private static List<string> Pieces(string text)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            current.Append(text[i]);
            if (EndsPiece(text, i))
            {
                pieces.Add(current.ToString().Trim());
                current.Clear();
            }
        }

        if (current.ToString().Trim() is { Length: > 0 } rest)
        {
            pieces.Add(rest);
        }

        return pieces;
    }

    // A mark ends a piece unless more marks follow it directly ("…!", "?!") or it is a decimal point ("3.5").
    private static bool EndsPiece(string text, int i)
    {
        if (!Terminators.Contains(text[i]))
        {
            return false;
        }

        if (i + 1 < text.Length && Terminators.Contains(text[i + 1]))
        {
            return false;
        }

        return text[i] != '.' || !IsDecimalPoint(text, i);
    }

    // A "." between two digits ("3.5"); "go.»" and "رفت.تق" are sentence ends.
    private static bool IsDecimalPoint(string text, int i) =>
        i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]);
}
