using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>One line of the cast screen: the stored member and what the screen shows beside it.</summary>
public sealed record CastRow(
    CastMember Member, int Words, bool IsMain, bool SharesVoice, VoiceFlavour Flavour = VoiceFlavour.Normal, StorytellerCharacter? Character = null);

/// <summary>
/// What the cast screen draws (Slice 65): a row per character with words and flags, how many lines have
/// no known speaker, and whether the pack has too few voices for every character to have their own.
/// </summary>
public sealed record CastSheet(IReadOnlyList<CastRow> Rows, int UnknownLines, bool VoicesAreShort, int VoiceCount)
{
    /// <summary>
    /// Builds the sheet. <paramref name="voiceCount"/> is the number of voices a character may have
    /// (the pack without the narrator's). A voice's later holders carry the shared tag, but only when
    /// the voices are short: otherwise a share is the grown-up's own choice.
    /// </summary>
    public static CastSheet Build(
        AttributionResult resolved, IReadOnlyList<CastMember> cast, int voiceCount, LanguageRules rules,
        int mainCount = VoiceCaster.DefaultMainCount, IReadOnlyList<StorytellerCharacter>? roster = null)
    {
        HashSet<string> mains = VoiceCaster.MainIds(resolved.Characters, mainCount);
        var members = cast.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId);
        var present = resolved.Characters.Where(c => members.ContainsKey(c.Id)).ToList();
        bool voicesShort = present.Count > voiceCount;
        var seen = new HashSet<(string, VoiceFlavour)>();
        // A speaker row is named by a speaker, never by a narrator the picker does not list.
        var speakers = roster?.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        var rows = present.Select(c => RowOf(c, members[c.Id], mains, seen, voicesShort, rules, speakers)).ToList();
        return new CastSheet(rows, CountUnknownLines(resolved.Segments), voicesShort, voiceCount);
    }

    // Two characters share when they sound the same: the same voice and flavour (Slice 116), not the same voice alone.
    private static CastRow RowOf(
        Character c, CastMember member, HashSet<string> mains, HashSet<(string, VoiceFlavour)> seen, bool voicesShort, LanguageRules rules,
        IReadOnlyList<StorytellerCharacter>? roster)
    {
        VoiceFlavour flavour = FlavourGuess.Effective(member, rules);
        return new CastRow(
            member, c.WordCount, mains.Contains(c.Id), !seen.Add((member.VoiceId, flavour)) && voicesShort, flavour,
            roster is null ? null : CharacterRoster.Nearest(roster, member.VoiceId, flavour, member.Gender));
    }

    /// <summary>Quoted lines nobody could be attributed to: the narrator reads them.</summary>
    public static int CountUnknownLines(IReadOnlyList<Segment> segments) =>
        segments.Count(s => s.Kind == SegmentKind.Quote && s.CharacterId is null);
}
