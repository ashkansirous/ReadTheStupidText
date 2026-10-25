using System.Text.RegularExpressions;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>
/// Guesses how a character should sound from the name (Slice 113, Decision 117), by the words of the language's
/// <see cref="FlavourWords"/>. Size words come first (a diminutive such as "little" gives Small, "big" or "giant" gives Big),
/// then the age words (Sleepy: "Grandpa Bear" is Sleepy, "Old Owl" too), then the animal words (Big, then Small); a name with none
/// of them is Normal. A grown-up's pick on the cast screen is pinned with the character, which is then Edited, and replaces
/// the guess (<see cref="Effective"/>).
/// </summary>
public static partial class FlavourGuess
{
    private const int MinPrefixLength = 3;

    public static VoiceFlavour Guess(string name, LanguageRules rules)
    {
        FlavourWords words = rules.FlavourWordLists;
        Func<IReadOnlySet<string>, bool> mentions = rules.Script == ScriptStyle.Unspaced
            ? list => MentionsUnspaced(name.Trim(), list, false)
            : TokenMatcher(Tokens(name));
        bool diminutive = rules.Script == ScriptStyle.Unspaced ? MentionsUnspaced(name.Trim(), words.Diminutives, true) : mentions(words.Diminutives);
        if (diminutive)
        {
            return VoiceFlavour.Small;
        }

        if (mentions(words.BigSize))
        {
            return VoiceFlavour.Big;
        }

        if (mentions(words.Sleepy))
        {
            return VoiceFlavour.Sleepy;
        }

        if (mentions(words.Big))
        {
            return VoiceFlavour.Big;
        }

        if (mentions(words.Small))
        {
            return VoiceFlavour.Small;
        }

        return Extra(mentions, words);
    }

    // Slice 116: the characters' own flavours come last, so the size and age words above keep winning ("Grumpy Troll" stays Big).
    private static VoiceFlavour Extra(Func<IReadOnlySet<string>, bool> mentions, FlavourWords words)
    {
        if (mentions(words.Grumpy))
        {
            return VoiceFlavour.Grumpy;
        }

        if (mentions(words.Whisper))
        {
            return VoiceFlavour.Whisper;
        }

        return mentions(words.Squeaky) ? VoiceFlavour.Squeaky : VoiceFlavour.Normal;
    }

    /// <summary>The flavour a member sounds with: the one stored with the character (a grown-up's pick, or the flavour of the storyteller character the cast gave it), else the guess from the name.
    /// The cast gives characters different flavours on purpose (Pip and Pop are both Normal by name, yet one is Small and one Squeaky), so a guess must never replace a stored one. The cast screen and the player use this one rule.</summary>
    public static VoiceFlavour Effective(CastMember member, LanguageRules rules) =>
        member.Flavour ?? Guess(member.Name, rules);

    /// <summary>The flavour of every character of a cast: the stored member's (<see cref="Effective"/>), or a guess for one with no member.</summary>
    public static IReadOnlyDictionary<string, VoiceFlavour> ForCast(
        IReadOnlyList<Character> characters, IReadOnlyList<CastMember> stored, LanguageRules rules)
    {
        var members = stored.GroupBy(m => m.CharacterId).ToDictionary(g => g.Key, g => g.First());
        return characters.GroupBy(c => c.Id).ToDictionary(
            g => g.Key, g => members.TryGetValue(g.Key, out CastMember? m) ? Effective(m, rules) : Guess(g.First().Name, rules));
    }

    // A word run matches a whole word of the list, or (for a list entry of several words) the words in a row.
    private static Func<IReadOnlySet<string>, bool> TokenMatcher(IReadOnlyList<string> tokens)
    {
        string joined = " " + string.Join(' ', tokens).ToLowerInvariant() + " ";
        return list => list.Any(w => w.Contains(' ') ? joined.Contains(" " + w + " ") : tokens.Contains(w, StringComparer.OrdinalIgnoreCase));
    }

    // A language without spaces never matches a part in the middle of a name: an entry is the whole name, or the end of it when
    // it is written in Han characters (a Chinese or Japanese head noun), or the start of it for a diminutive of three or more characters.
    private static bool MentionsUnspaced(string name, IReadOnlySet<string> words, bool diminutivePrefix) =>
        words.Any(w => name.Equals(w, StringComparison.OrdinalIgnoreCase)
            || (IsHan(w) && name.EndsWith(w, StringComparison.Ordinal))
            || (diminutivePrefix && w.Length >= MinPrefixLength && name.StartsWith(w, StringComparison.Ordinal)));

    private static bool IsHan(string word) => word.All(c => c is >= '一' and <= '鿿');

    private static IReadOnlyList<string> Tokens(string name) =>
        WordRun().Matches(name).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+")]
    private static partial Regex WordRun();
}
