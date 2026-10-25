using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

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

    /// <summary>Gives a character a storyteller character (Slice 116): its voice and flavour, pinned together so recasting keeps them. A narrator is refused (nothing changes);
    /// the picker only lists characters whose voice is not the narrator's (<see cref="CharacterRoster.Choices"/>), except in a language whose only voice
    /// is the narrator's (Chinese), where the flavours tell the characters apart; so there is no narrator-voice guard here.</summary>
    public static CastEdit SetCharacter(CastEdit state, string characterId, StorytellerCharacter character)
    {
        if (character.Kind != CharacterKind.Speaker)
        {
            return state;
        }

        return state with { Cast = Replace(state.Cast, characterId, m => m with { VoiceId = character.VoiceId, Flavour = character.Flavour, Edited = true }) };
    }

    /// <summary>Sets what a character is (Slice 125): an animal, or <c>null</c> for "not an animal". It is the animal alone: the voice and the edited mark are not touched
    /// (the cast screen picks the voice again when it was not chosen by hand).</summary>
    public static CastEdit SetAnimal(CastEdit state, string characterId, AnimalKind? animal) =>
        state with { Cast = Replace(state.Cast, characterId, m => m with { Animal = animal, NotAnimal = animal is null }) };

    /// <summary>The story with a character's animal set in every language's cast: what a character is does not depend on the language it is read in.</summary>
    public static Story WithAnimal(Story story, string characterId, AnimalKind? animal)
    {
        Story main = story with { Cast = SetAnimal(new CastEdit(story.Cast, story.Merges), characterId, animal).Cast };
        if (story.LanguageCasts is null)
        {
            return main;
        }

        var casts = story.LanguageCasts.ToDictionary(c => c.Key, c => (IReadOnlyList<CastMember>)SetAnimal(new CastEdit(c.Value, story.Merges), characterId, animal).Cast);
        return main with { LanguageCasts = casts };
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

    /// <summary>Whether any character of <paramref name="cast"/> has a voice that is not in <paramref name="pool"/> and is not an explicit pick that stays (Slice 114):
    /// the language's voices changed under it (a storyteller pack was installed or removed), so the cast is recast from the pool it has now.
    /// Only that switch counts: the pool and the cast voice must both be Supertonic or Kokoro ones, so a renamed or momentarily different pack voice list of any
    /// other language never recasts a cast. An empty pool (no voices yet) never counts.</summary>
    public static bool HasVoiceOutside(IReadOnlyList<CastMember> cast, IReadOnlyList<VoiceOption> pool) =>
        pool.Count > 0 && pool.All(v => IsStorytellerSwitchVoice(v.Id))
        && cast.Any(m => IsStorytellerSwitchVoice(m.VoiceId) && IsOutside(m, pool) && !StaysPicked(m, pool));

    // The engine families a storyteller pack swaps between: Supertonic and Kokoro, and (Slice 118) the Chinese Melo voice, which Kokoro replaces for Chinese.
    private static bool IsStorytellerSwitchVoice(string voiceId) =>
        voiceId.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal)
        || voiceId.StartsWith(KokoroVoiceTable.IdPrefix, StringComparison.Ordinal)
        || voiceId.StartsWith(VoiceChoices.MeloIdPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The recast of a cast whose pool changed (Slice 114) with the grown-up's explicit picks put back: a character marked <see cref="CastMember.Edited"/>
    /// keeps its stored member while its voice is still there. A voice is there while it is in the pool, or is a Supertonic one while the pool is not the storyteller's (Slice 115, Decision 121: once the pool is Kokoro a Supertonic pick is not exempt,
    /// so it takes the recast's voice, the closest one of the character's gender). A pick that is the narrator's voice never stays.
    /// </summary>
    public static IReadOnlyList<CastMember> KeepExplicitPicks(
        IReadOnlyList<CastMember> before, IReadOnlyList<CastMember> recast, IReadOnlyList<VoiceOption> pool, string narratorVoiceId) =>
        recast
            .Select(m => before.FirstOrDefault(b => b.CharacterId == m.CharacterId) is { } old && old.VoiceId != narratorVoiceId && StaysPicked(old, pool) ? old : m)
            .ToList();

    private static bool IsOutside(CastMember member, IReadOnlyList<VoiceOption> pool) => pool.All(v => v.Id != member.VoiceId);

    // Slice 115, Decision 121: only a pool that has become the storyteller pack's (Kokoro) takes a Supertonic pick over, re-casting it to a Kokoro voice (gender and
    // flavour kept by the caster and the stored member). Anywhere else a Supertonic pick stays, as before.
    private static bool StaysPicked(CastMember member, IReadOnlyList<VoiceOption> pool) =>
        member.Edited && (!IsOutside(member, pool) || (IsSupertonic(member.VoiceId) && !HasStorytellerVoice(pool)));

    /// <summary>Whether a pool has become the storyteller pack's (Kokoro) voices (Slice 115).</summary>
    public static bool HasStorytellerVoice(IReadOnlyList<VoiceOption> pool) =>
        pool.Any(v => v.Id.StartsWith(KokoroVoiceTable.IdPrefix, StringComparison.Ordinal));

    private static bool IsSupertonic(string voiceId) => voiceId.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal);

    /// <summary>The recast with those characters unpinned: their pin was overridden by the new narrator.</summary>
    public static IReadOnlyList<CastMember> UnpinOverridden(
        IReadOnlyList<CastMember> before, IReadOnlyList<CastMember> recast, string narratorVoiceId) =>
        recast.Select(m => LosesVoice(before, m, narratorVoiceId) ? m with { Edited = false } : m).ToList();

    private static bool LosesVoice(IReadOnlyList<CastMember> before, CastMember now, string narratorVoiceId) =>
        now.VoiceId != narratorVoiceId
        && before.Any(b => b.CharacterId == now.CharacterId && b.Edited && b.VoiceId == narratorVoiceId);

    private static List<CastMember> Replace(IReadOnlyList<CastMember> cast, string characterId, Func<CastMember, CastMember> change) =>
        cast.Select(m => m.CharacterId == characterId ? change(m) : m).ToList();
}
