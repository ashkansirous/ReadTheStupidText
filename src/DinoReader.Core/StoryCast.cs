namespace DinoReader.Core;

/// <summary>Turns a caster result into stored cast members and keeps stored voices stable when a story is recast.</summary>
public static class StoryCast
{
    /// <summary>One member per character that has a voice in <paramref name="cast"/>.</summary>
    public static IReadOnlyList<CastMember> ToMembers(IReadOnlyList<Character> characters, VoiceCast cast) =>
        characters
            .Select(c => (Character: c, Voice: cast.VoiceFor(c.Id)))
            .Where(x => x.Voice is not null)
            .Select(x => new CastMember(x.Character.Id, x.Character.Name, x.Character.Gender, x.Voice!))
            .ToList();

    /// <summary>
    /// The stored voice of every character, for <see cref="VoiceCaster.Cast"/> to keep. A stored
    /// voice that is no longer in the pool (for example the new narrator's) is ignored there.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Pinned(IReadOnlyList<CastMember> stored) =>
        stored.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => m.VoiceId);
}
