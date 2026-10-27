using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>What a book's author decided for a character (Slice 156): the suggested roster id and the traits.</summary>
public sealed record AuthoredVoice(string? SuggestedVoice, CharacterTraits? Traits);

/// <summary>Turns a caster result into stored cast members and keeps stored voices stable when a story is recast.</summary>
public static class StoryCast
{
    /// <summary>
    /// One member per character that has a voice in <paramref name="cast"/>. A character a grown-up
    /// edited stays edited, whatever voice the caster ended up with. Each member's flavour is the grown-up's pick when
    /// they made one, else the guess from the name in <paramref name="rules"/> (Slice 113; the rules are required, so a path that forgets them cannot guess in the wrong language).
    /// The animal setting (Slice 125) is kept from the stored member; <paramref name="animals"/> names the animals a book's builder decided for characters that have none stored.
    /// </summary>
    public static IReadOnlyList<CastMember> ToMembers(
        IReadOnlyList<Character> characters, VoiceCast cast, LanguageRules rules, IReadOnlyList<CastMember>? stored = null,
        IReadOnlyDictionary<string, AnimalKind>? animals = null,
        IReadOnlyDictionary<string, AuthoredVoice>? authored = null) =>
        characters
            .Select(c => (Character: c, Voice: cast.VoiceFor(c.Id)))
            .Where(x => x.Voice is not null)
            .Select(x => (new CastMember(
                x.Character.Id, x.Character.Name, x.Character.Gender, x.Voice!, WasEdited(stored, x.Character.Id, x.Voice!),
                FlavourOf(cast, stored, x.Character, rules)) with { Animal = AnimalOf(stored, animals, x.Character.Id), NotAnimal = NotAnimalIn(stored, x.Character.Id) })
                .WithAuthored(StoredAuthored(stored, x.Character.Id) ?? (authored is not null && authored.TryGetValue(x.Character.Id, out AuthoredVoice? a) ? a : null)))
            .ToList();

    // The book's suggestion and traits stay with the member whatever voice the caster gave it (Slice 156): they are book data, like the animal.
    private static AuthoredVoice? StoredAuthored(IReadOnlyList<CastMember>? stored, string characterId) =>
        stored?.FirstOrDefault(m => m.CharacterId == characterId) is { HasAuthoredVoice: true } m ? new AuthoredVoice(m.SuggestedVoice, m.Traits) : null;

    private static CastMember WithAuthored(this CastMember member, AuthoredVoice? authored) =>
        authored is null ? member : member with { SuggestedVoice = authored.SuggestedVoice, Traits = authored.Traits };

    private static AnimalKind? AnimalOf(IReadOnlyList<CastMember>? stored, IReadOnlyDictionary<string, AnimalKind>? animals, string characterId)
    {
        return stored?.FirstOrDefault(m => m.CharacterId == characterId)?.Animal
            ?? (animals is not null && animals.TryGetValue(characterId, out AnimalKind kind) ? kind : null);
    }

    private static bool NotAnimalIn(IReadOnlyList<CastMember>? stored, string characterId)
    {
        return stored?.Any(m => m.CharacterId == characterId && m.NotAnimal) == true;
    }

    // The grown-up's pick first, but with a roster cast (Slice 116) only while the character still has the voice they picked it with: a voice that was dropped and recast
    // takes the flavour of the character the caster gave, not the old pick's. A voice-only cast has no flavours of its own, so the pick stays. Else a guess from the name.
    private static VoiceFlavour FlavourOf(VoiceCast cast, IReadOnlyList<CastMember>? stored, Character character, LanguageRules rules)
    {
        VoiceFlavour? castFlavour = cast.Flavours?.GetValueOrDefault(character.Id);
        CastMember? pick = stored?.FirstOrDefault(m => m.CharacterId == character.Id && m.Edited);
        bool pickStands = pick is not null && (castFlavour is null || pick.VoiceId == cast.VoiceFor(character.Id));
        return (pickStands ? pick!.Flavour : null) ?? castFlavour ?? FlavourGuess.Guess(character.Name, rules);
    }

    // A pick that no longer stands was replaced by the book's voice (Slice 156), so the member is no longer the grown-up's.
    private static bool WasEdited(IReadOnlyList<CastMember>? stored, string characterId, string voiceId) =>
        stored?.Any(m => m.CharacterId == characterId && m.Edited && !(m.HasAuthoredVoice && m.VoiceId != voiceId)) == true;

    /// <summary>
    /// The attribution as the story reads it: an edited character's stored gender in place of the guessed one, so a recast keeps it. So does a character whose book described it (traits, Slice 156) with a known gender: that
    /// is the author's decision. A bare voice suggestion decides nothing about gender.
    /// Merges a book saved before Slice 151 are ignored: those names have their own voices again.
    /// </summary>
    public static AttributionResult Resolve(AttributionResult raw, IReadOnlyList<CastMember> stored)
    {
        var edited = stored.Where(m => m.Edited || (m.Traits is not null && m.Gender != Gender.Unknown)).DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => m.Gender);
        return raw with
        {
            Characters = raw.Characters.Select(c => edited.TryGetValue(c.Id, out Gender g) ? c with { Gender = g } : c).ToList(),
        };
    }

    /// <summary>
    /// The stored voice of every character, for <see cref="VoiceCaster.Cast"/> to keep. A stored
    /// voice that is no longer in the pool (for example the new narrator's) is ignored there.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Pinned(IReadOnlyList<CastMember> stored) =>
        stored.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => m.VoiceId);

    /// <summary>The stored voice and flavour of every character, for <see cref="CastCharacters.Cast"/> to keep (Slice 116).</summary>
    public static IReadOnlyDictionary<string, PinnedCharacter> PinnedCharacters(IReadOnlyList<CastMember> stored) =>
        stored.DistinctBy(m => m.CharacterId).ToDictionary(m => m.CharacterId, m => new PinnedCharacter(m.VoiceId, m.Flavour, m.Edited, m.SuggestedVoice, m.Traits));
}
