namespace DinoReader.Core;

/// <summary>The cast a grown-up is editing: the stored members and the merges made so far.</summary>
public sealed record CastEdit(IReadOnlyList<CastMember> Cast, IReadOnlyDictionary<string, string> Merges);

/// <summary>
/// The grown-up's edits on the cast screen (Slice 65), as pure functions. Every edit marks the
/// character <see cref="CastMember.Edited"/>, and a stored voice is a pinned one for the caster
/// (<see cref="StoryCast.Pinned"/>), so recasting never overrides an edit. The narrator's voice is
/// never given to a character.
/// </summary>
public static class CastEditor
{
    /// <summary>The edit state for one reading language (Slice 108): that language's cast and the story's merges, which every language shares.</summary>
    public static CastEdit Open(Story story, string language) => new(story.CastIn(language), story.Merges);

    /// <summary>The story with <paramref name="edit"/> saved into <paramref name="language"/>'s cast; other languages' casts keep their pins.</summary>
    public static Story Close(Story story, string language, CastEdit edit) =>
        story.WithCastIn(language, edit.Cast) with { CharacterMerges = edit.Merges };

    /// <summary>Gives a character a voice by hand. The narrator's voice is refused (nothing changes).</summary>
    public static CastEdit SetVoice(CastEdit state, string characterId, string voiceId, string narratorVoiceId)
    {
        if (voiceId == narratorVoiceId)
        {
            return state;
        }

        return state with { Cast = Replace(state.Cast, characterId, m => m with { VoiceId = voiceId, Edited = true }) };
    }

    /// <summary>
    /// Sets a character's gender. Their voice stays if it already fits the new gender ("?" fits any);
    /// otherwise a free voice of the new gender is taken, else the least-spoken one is shared. A character
    /// is never handed a voice of the opposite gender: with none of that gender in the pack the voice stays
    /// (see <see cref="HasVoiceOfGender"/>). Nobody else's voice moves.
    /// </summary>
    public static CastEdit SetGender(
        CastEdit state, IReadOnlyList<Character> characters, IReadOnlyList<VoiceOption> voices,
        string narratorVoiceId, string characterId, Gender gender)
    {
        CastMember? member = state.Cast.FirstOrDefault(m => m.CharacterId == characterId);
        if (member is null)
        {
            return state;
        }

        string voice = Fits(voices, member.VoiceId, gender)
            ? member.VoiceId
            : Recast(state, characters, voices, narratorVoiceId, characterId, gender) ?? member.VoiceId;
        return state with { Cast = Replace(state.Cast, characterId, m => m with { Gender = gender, VoiceId = voice, Edited = true }) };
    }

    /// <summary>
    /// Merges <paramref name="dropId"/> into <paramref name="keepId"/>: one entry is left, with the kept
    /// character's name and voice; the other's lines are re-attributed by <see cref="CastMerges"/>. The kept
    /// character's gender is the better-known one. A merge is stored in the merges, so it does not pin the
    /// kept character (<see cref="CastMember.Edited"/> is left as it was).
    /// </summary>
    public static CastEdit Merge(CastEdit state, string keepId, string dropId)
    {
        bool known = state.Cast.Any(m => m.CharacterId == keepId) && state.Cast.Any(m => m.CharacterId == dropId);
        if (keepId == dropId || !known)
        {
            return state;
        }

        CastMember dropped = state.Cast.First(m => m.CharacterId == dropId);
        var cast = state.Cast
            .Where(m => m.CharacterId != dropId)
            .Select(m => m.CharacterId == keepId ? m with { Gender = CastMerges.BetterGender(m.Gender, dropped.Gender) } : m)
            .ToList();
        return new CastEdit(cast, CastMerges.Add(state.Merges, keepId, dropId));
    }

    /// <summary>Whether any character of <paramref name="cast"/> has <paramref name="voiceId"/>: a cast that must be recast once that voice is the narrator's.</summary>
    public static bool UsesVoice(IReadOnlyList<CastMember> cast, string voiceId) => cast.Any(m => m.VoiceId == voiceId);

    /// <summary>The recast with those characters unpinned: their pin was overridden by the new narrator.</summary>
    public static IReadOnlyList<CastMember> UnpinOverridden(
        IReadOnlyList<CastMember> before, IReadOnlyList<CastMember> recast, string narratorVoiceId) =>
        recast.Select(m => LosesVoice(before, m, narratorVoiceId) ? m with { Edited = false } : m).ToList();

    private static bool LosesVoice(IReadOnlyList<CastMember> before, CastMember now, string narratorVoiceId) =>
        now.VoiceId != narratorVoiceId
        && before.Any(b => b.CharacterId == now.CharacterId && b.Edited && b.VoiceId == narratorVoiceId);

    /// <summary>The voices a character can be given, the narrator's left out, ones of their gender first.</summary>
    public static IReadOnlyList<VoiceOption> VoiceChoices(
        IReadOnlyList<VoiceOption> voices, string narratorVoiceId, Gender gender) =>
        voices
            .Where(v => v.Id != narratorVoiceId)
            .OrderBy(v => gender == Gender.Unknown || v.Gender == gender ? 0 : 1)
            .ToList();

    /// <summary>Whether the pack has any voice of <paramref name="gender"/> a character can take (the narrator's left out).</summary>
    public static bool HasVoiceOfGender(IReadOnlyList<VoiceOption> voices, string narratorVoiceId, Gender gender) =>
        gender == Gender.Unknown || voices.Any(v => v.Gender == gender && v.Id != narratorVoiceId);

    // A free voice of the gender first; otherwise the one whose holders speak the fewest words.
    // Null when the pack has none of that gender: the caller keeps the current voice.
    private static string? Recast(
        CastEdit state, IReadOnlyList<Character> characters, IReadOnlyList<VoiceOption> voices,
        string narratorVoiceId, string characterId, Gender gender)
    {
        var candidates = voices.Where(v => v.Gender == gender && v.Id != narratorVoiceId).ToList();
        var others = state.Cast.Where(m => m.CharacterId != characterId).ToList();
        VoiceOption? free = candidates.FirstOrDefault(v => others.All(m => m.VoiceId != v.Id));
        if (free is not null || candidates.Count == 0)
        {
            return free?.Id;
        }

        var words = characters.ToDictionary(c => c.Id, c => c.WordCount);
        return candidates
            .OrderBy(v => others.Where(m => m.VoiceId == v.Id).Sum(m => words.GetValueOrDefault(m.CharacterId)))
            .First().Id;
    }

    private static bool Fits(IReadOnlyList<VoiceOption> voices, string voiceId, Gender gender) =>
        gender == Gender.Unknown || voices.FirstOrDefault(v => v.Id == voiceId)?.Gender == gender;

    private static List<CastMember> Replace(IReadOnlyList<CastMember> cast, string characterId, Func<CastMember, CastMember> change) =>
        cast.Select(m => m.CharacterId == characterId ? change(m) : m).ToList();
}
