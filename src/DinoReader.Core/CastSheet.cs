namespace DinoReader.Core;

/// <summary>One line of the cast screen: the stored member and what the screen shows beside it.</summary>
public sealed record CastRow(CastMember Member, int Words, bool IsMain, bool SharesVoice);

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
        AttributionResult resolved, IReadOnlyList<CastMember> cast, int voiceCount, int mainCount = VoiceCaster.DefaultMainCount)
    {
        HashSet<string> mains = VoiceCaster.MainIds(resolved.Characters, mainCount);
        var members = cast.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId);
        var present = resolved.Characters.Where(c => members.ContainsKey(c.Id)).ToList();
        bool voicesShort = present.Count > voiceCount;
        var seen = new HashSet<string>();
        var rows = present
            .Select(c => new CastRow(
                members[c.Id], c.WordCount, mains.Contains(c.Id), !seen.Add(members[c.Id].VoiceId) && voicesShort))
            .ToList();
        return new CastSheet(rows, CountUnknownLines(resolved.Segments), voicesShort, voiceCount);
    }

    /// <summary>Quoted lines nobody could be attributed to: the narrator reads them.</summary>
    public static int CountUnknownLines(IReadOnlyList<Segment> segments) =>
        segments.Count(s => s.Kind == SegmentKind.Quote && s.CharacterId is null);
}
