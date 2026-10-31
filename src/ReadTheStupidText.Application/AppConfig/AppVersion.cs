using System.Globalization;

namespace ReadTheStupidText.Application.AppConfig;

/// <summary>The platforms the remote config keeps versions for (Slice 217; the same keys Dino Reader's Slice 132 reads).</summary>
public enum AppPlatform
{
    Android,
    Ios,
    Windows,
}

/// <summary>Compares dotted app versions ("1.42.0"); the comparison behind the forced update (Slice 217, port of Dino Reader's Slice 132).</summary>
public static class AppVersion
{
    private const int MinParts = 2;
    private const int MaxParts = 4;

    /// <summary>The numbers of a dotted version of 2 to 4 non-negative integer parts, or null when the text is anything else.</summary>
    public static int[]? Parse(string? text)
    {
        string[] parts = (text ?? string.Empty).Trim().Split('.');
        if (parts.Length < MinParts || parts.Length > MaxParts)
        {
            return null;
        }

        var numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }

    /// <summary>Negative when <paramref name="a"/> is older than <paramref name="b"/>; a missing part counts as 0. Null when either does not parse.</summary>
    public static int? Compare(string? a, string? b)
    {
        int[]? left = Parse(a);
        int[]? right = Parse(b);
        if (left is null || right is null)
        {
            return null;
        }

        for (int i = 0; i < MaxParts; i++)
        {
            int byPart = PartAt(left, i).CompareTo(PartAt(right, i));
            if (byPart != 0)
            {
                return byPart;
            }
        }

        return 0;
    }

    private static int PartAt(int[] parts, int index) => index < parts.Length ? parts[index] : 0;
}

/// <summary>One platform's versions: below <see cref="MinVersion"/> the app must update, below <see cref="LatestVersion"/> it only says so quietly.</summary>
public sealed record PlatformVersions(string MinVersion, string LatestVersion)
{
    /// <summary>The version that forces and announces nothing.</summary>
    public const string NoVersion = "0.0.0";

    public static PlatformVersions None { get; } = new(NoVersion, NoVersion);
}
