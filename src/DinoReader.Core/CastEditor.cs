using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>The cast a grown-up is editing: the stored members.</summary>
public sealed record CastEdit(IReadOnlyList<CastMember> Cast);

/// <summary>
/// The grown-up's edits on the cast screen (Slice 65), as pure functions. Every edit marks the
/// character <see cref="CastMember.Edited"/>, and a stored voice is a pinned one for the caster
/// (<see cref="StoryCast.Pinned"/>), so recasting never overrides an edit. The narrator's voice is
/// never given to a character.
/// </summary>
public static class CastEditor
{
    /// <summary>The edit state for one reading language (Slice 108): that language's cast.</summary>
    public static CastEdit Open(Story story, string language) => new(story.CastIn(language));

    /// <summary>The story with <paramref name="edit"/> saved into <paramref name="language"/>'s cast; other languages' casts keep their pins.</summary>
    public static Story Close(Story story, string language, CastEdit edit) =>
        story.WithCastIn(language, edit.Cast);

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

    /// <summary>"Back to the book's voice" (Slice 156): the character is no longer a grown-up's pick, so the caster gives it the voice its book chose again (the caller recasts it,
    /// <c>StoryVoicing.RecastCharacter</c>). A character whose book chose nothing is left as it is.</summary>
    public static CastEdit UseBookVoice(CastEdit state, string characterId) =>
        state with { Cast = Replace(state.Cast, characterId, m => m.HasAuthoredVoice ? m with { Edited = false } : m) };

    /// <summary>Whether the "Back to the book's voice" link is worth showing (Slice 156): a grown-up's pick on a character whose book chose a voice or traits, where the book's
    /// suggestion is usable in this language (in the roster, gender not opposite) and sounds different from the pick, or, with no usable suggestion, the closest traits may.</summary>
    public static bool OffersBookVoice(CastMember member, VoiceFlavour flavour, IReadOnlyList<StorytellerCharacter> speakers)
    {
        if (!member.Edited || !member.HasAuthoredVoice)
        {
            return false;
        }

        StorytellerCharacter? suggested = member.SuggestedVoice is { Length: > 0 } id ? speakers.FirstOrDefault(c => c.Id == id) : null;
        if (suggested is not null && !CharacterRoster.OppositeGender(suggested.Gender, member.Gender) && !CastCharacters.SwapsInWhisper(suggested, member.Traits))
        {
            return suggested.VoiceId != member.VoiceId || suggested.Flavour != flavour;
        }

        return member.Traits is not null;
    }

    /// <summary>Whether any character of <paramref name="cast"/> has <paramref name="voiceId"/>: a cast that must be recast once that voice is the narrator's.</summary>
    public static bool UsesVoice(IReadOnlyList<CastMember> cast, string voiceId) => cast.Any(m => m.VoiceId == voiceId);

    /// <summary>Whether a character of <paramref name="cast"/> would really sound like the narrator (Slice 144): it has <paramref name="narratorVoiceId"/> and not a flavour the language's roster
    /// deliberately plays on that voice (Farsi's mana is the narrator and also the mum, dragon and fairy, told apart by flavour), so a cast like that is not recast on every read.</summary>
    public static bool ClashesWithNarrator(IReadOnlyList<CastMember> cast, string narratorVoiceId, string language)
    {
        var shared = CharacterRoster.Speakers(language).Where(c => c.VoiceId == narratorVoiceId).Select(c => c.Flavour).ToHashSet();
        return cast.Any(m => m.VoiceId == narratorVoiceId && (m.Flavour is not { } flavour || !shared.Contains(flavour)));
    }

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

    /// <summary>Whether any character the grown-up did not pick has a voice its language's roster no longer uses (Slice 144: Farsi's literary voice left the cast), so the cast is recast.</summary>
    public static bool UsesRetiredVoice(IReadOnlyList<CastMember> cast, string language) => cast.Any(m => IsRetired(m, language));

    /// <summary>The cast without the characters of <see cref="UsesRetiredVoice"/>, so a recast gives them a voice the roster uses; picked characters stay.</summary>
    public static IReadOnlyList<CastMember> WithoutRetiredVoices(IReadOnlyList<CastMember> cast, string language) =>
        cast.Where(m => !IsRetired(m, language)).ToList();

    private static bool IsRetired(CastMember member, string language) =>
        !member.Edited && RuleSets.PrimaryCode(language) == VoiceChoices.PersianLanguageCode && member.VoiceId == VoiceChoices.PersianLiteraryVoiceId && CharacterRoster.For(language).All(c => c.VoiceId != member.VoiceId);

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
