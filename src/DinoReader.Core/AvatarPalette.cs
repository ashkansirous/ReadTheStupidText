namespace DinoReader.Core;

/// <summary>The avatar colours of the cast screen (Dino Reader v2 mock, screens 10 and 11).</summary>
public static class AvatarPalette
{
    public static IReadOnlyList<string> Colours { get; } = ["#4FC060", "#FF7A9C", "#FFA94D", "#9AA5B8"];

    /// <summary>The colour for the nth character, wrapping round the palette.</summary>
    public static string ColourAt(int index) => Colours[((index % Colours.Count) + Colours.Count) % Colours.Count];

    /// <summary>The letter in a character's avatar: the first letter or digit of the name, upper-cased.</summary>
    public static string InitialOf(string name)
    {
        string trimmed = name.Trim();
        int at = trimmed.ToList().FindIndex(char.IsLetterOrDigit);
        return at < 0 ? "?" : char.ToUpperInvariant(trimmed[at]).ToString();
    }
}
