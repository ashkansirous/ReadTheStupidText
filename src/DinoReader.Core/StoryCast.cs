namespace DinoReader.Core;

/// <summary>Turns a caster result into stored cast members and keeps stored voices stable when a story is recast.</summary>
public static class StoryCast
{
    /// <summary>
    /// One member per character that has a voice in <paramref name="cast"/>. A character a grown-up
    /// edited stays edited, whatever voice the caster ended up with.
    /// </summary>
    public static IReadOnlyList<CastMember> ToMembers(
        IReadOnlyList<Character> characters, VoiceCast cast, IReadOnlyList<CastMember>? stored = null) =>
        characters
            .Select(c => (Character: c, Voice: cast.VoiceFor(c.Id)))
            .Where(x => x.Voice is not null)
            .Select(x => new CastMember(
                x.Character.Id, x.Character.Name, x.Character.Gender, x.Voice!, WasEdited(stored, x.Character.Id)))
            .ToList();

    private static bool WasEdited(IReadOnlyList<CastMember>? stored, string characterId) =>
        stored?.Any(m => m.CharacterId == characterId && m.Edited) == true;

    /// <summary>
    /// The attribution as the story reads it: the grown-up's merges applied, and an edited character's
    /// stored gender in place of the guessed one, so a recast keeps it.
    /// </summary>
    public static AttributionResult Resolve(
        AttributionResult raw, IReadOnlyDictionary<string, string> merges, IReadOnlyList<CastMember> stored)
    {
        AttributionResult merged = CastMerges.Apply(raw, merges);
        var edited = stored.Where(m => m.Edited).DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => m.Gender);
        return merged with
        {
            Characters = merged.Characters.Select(c => edited.TryGetValue(c.Id, out Gender g) ? c with { Gender = g } : c).ToList(),
        };
    }

    /// <summary>
    /// The stored voice of every character, for <see cref="VoiceCaster.Cast"/> to keep. A stored
    /// voice that is no longer in the pool (for example the new narrator's) is ignored there.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Pinned(IReadOnlyList<CastMember> stored) =>
        stored.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => m.VoiceId);
}
