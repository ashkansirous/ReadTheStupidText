namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Orders pack versions (<see cref="PackNaming.IsValidVersion"/>: letters, digits and dots). Dotted
/// segments compare numerically ("10" is newer than "9"); a missing segment counts as 0, so "1" equals
/// "1.0"; a segment that is not a number compares as text, and sorts before a number.
/// </summary>
public static class PackVersions
{
    private const char Separator = '.';

    /// <summary>Negative when <paramref name="a"/> is older than <paramref name="b"/>, zero when equal, positive when newer.</summary>
    public static int Compare(string a, string b)
    {
        string[] left = a.Split(Separator);
        string[] right = b.Split(Separator);
        for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            int order = CompareSegment(i < left.Length ? left[i] : "0", i < right.Length ? right[i] : "0");
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    public static bool IsNewer(string candidate, string installed) => Compare(candidate, installed) > 0;

    private static int CompareSegment(string a, string b)
    {
        bool aNumeric = System.Numerics.BigInteger.TryParse(a, out System.Numerics.BigInteger x);
        bool bNumeric = System.Numerics.BigInteger.TryParse(b, out System.Numerics.BigInteger y);
        if (aNumeric && bNumeric)
        {
            return x.CompareTo(y);
        }

        if (aNumeric != bNumeric)
        {
            return aNumeric ? 1 : -1;
        }

        return Math.Sign(string.CompareOrdinal(a, b));
    }
}
