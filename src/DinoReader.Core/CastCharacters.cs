using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>
/// A voice and flavour already stored for a character, which a recast keeps while the voice is still there. A grown-up's pick (<paramref name="Edited"/>) is kept whatever else
/// sounds the same; an automatic one is dropped when it would sound like a pick already kept (an older voice-only cast gave several characters one voice).
/// </summary>
public sealed record PinnedCharacter(string VoiceId, VoiceFlavour? Flavour, bool Edited = true);

/// <summary>
/// Casts a story's characters with storyteller characters (Slice 116, Decision 122), the successor of the voice-only <see cref="VoiceCaster"/>.
/// Each story character gets the roster character that fits its name best: the gender, then the flavour the name guesses ("Little Pig" is
/// Small, so Cheeky Piglet), and no two story characters share a roster character while the roster has another that fits. Fully deterministic:
/// stored picks first, then the main characters (most words) and the rest in the order given; ties break by roster order. The result is the
/// same <see cref="VoiceCast"/> the rest of Dino reads: a voice per character and its flavour. A story character that is an animal (<c>animalOf</c>, Slice 125) is
/// cast to its animal character first (piglet for a pig, big bear for a grown bear), or to another young animal character while its own is taken; only a voice a
/// grown-up picked is never replaced.
/// </summary>
public static class CastCharacters
{
    private const int UsedAlready = 30;
    private const int VoiceUsedAlready = 3;
    private const int NotAnAnimalCharacter = 25;
    private const int OtherYoungAnimal = 8;
    private const int AnimalGenderRelief = 10;

    public static VoiceCast Cast(
        IReadOnlyList<Character> characters,
        IReadOnlyList<StorytellerCharacter> choices,
        IReadOnlyList<StorytellerCharacter> roster,
        string narratorVoiceId,
        Func<Character, VoiceFlavour> guess,
        IReadOnlyDictionary<string, PinnedCharacter>? pinned = null,
        int mainCharacterCount = VoiceCaster.DefaultMainCount,
        Func<Character, AnimalHint?>? animalOf = null)
    {
        var state = new State(choices, animalOf);
        var picks = new List<(int Order, Character Character, string VoiceId, VoiceFlavour Flavour)>();
        var kept = Kept(characters, choices, roster, guess, pinned);
        foreach ((Character character, int order) in characters.Select((c, i) => (c, i)).Where(x => kept.ContainsKey(x.c.Id)))
        {
            (string voice, VoiceFlavour flavour, StorytellerCharacter? near) = kept[character.Id];
            state.Use(near, voice);
            picks.Add((order, character, voice, flavour));
        }

        HashSet<string> mains = VoiceCaster.MainIds(characters, mainCharacterCount);
        foreach ((Character character, int order) in MainsFirst(characters, mains).Where(x => !kept.ContainsKey(x.Character.Id)))
        {
            StorytellerCharacter? pick = state.Pick(character, guess(character));
            if (pick is not null)
            {
                state.Use(pick, pick.VoiceId);
                picks.Add((order, character, pick.VoiceId, pick.Flavour));
            }
        }

        var ordered = picks.OrderBy(p => p.Order).ToList();
        return new VoiceCast(narratorVoiceId, Flag(ordered), ordered.ToDictionary(p => p.Character.Id, p => p.Flavour));
    }

    // A stored voice that is still among the choices' voices stays, with its stored flavour (else the guess): the pick of an earlier read or of a grown-up.
    private static Dictionary<string, (string Voice, VoiceFlavour Flavour, StorytellerCharacter? Near)> Kept(
        IReadOnlyList<Character> characters, IReadOnlyList<StorytellerCharacter> choices, IReadOnlyList<StorytellerCharacter> roster,
        Func<Character, VoiceFlavour> guess, IReadOnlyDictionary<string, PinnedCharacter>? pinned)
    {
        var result = new Dictionary<string, (string, VoiceFlavour, StorytellerCharacter?)>();
        if (pinned is null)
        {
            return result;
        }

        var voices = choices.Select(c => c.VoiceId).ToHashSet();
        var speakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        var sounds = new HashSet<(string, VoiceFlavour)>();
        foreach (Character character in characters.OrderBy(c => pinned.TryGetValue(c.Id, out PinnedCharacter? p) && p.Edited ? 0 : 1))
        {
            if (!pinned.TryGetValue(character.Id, out PinnedCharacter? stored) || !voices.Contains(stored.VoiceId))
            {
                continue;
            }

            VoiceFlavour flavour = stored.Flavour ?? guess(character);
            if (sounds.Add((stored.VoiceId, flavour)) || stored.Edited)
            {
                result[character.Id] = (stored.VoiceId, flavour, CharacterRoster.Nearest(speakers, stored.VoiceId, flavour, character.Gender));
            }
        }

        return result;
    }

    private static IEnumerable<(Character Character, int Order)> MainsFirst(IReadOnlyList<Character> characters, HashSet<string> mains) =>
        characters.Select((c, order) => (Character: c, Order: order))
            .OrderBy(x => mains.Contains(x.Character.Id) ? 0 : 1)
            .ThenBy(x => x.Order);

    // Two story characters count as sharing when they sound the same: the same voice and flavour.
    private static List<VoiceAssignment> Flag(List<(int Order, Character Character, string VoiceId, VoiceFlavour Flavour)> picks)
    {
        var uses = picks.GroupBy(p => (p.VoiceId, p.Flavour)).ToDictionary(g => g.Key, g => g.Count());
        return picks.Select(p => new VoiceAssignment(p.Character.Id, p.VoiceId, uses[(p.VoiceId, p.Flavour)] > 1)).ToList();
    }

    /// <summary>Running state of one casting pass: which roster characters and voices are taken.</summary>
    private sealed class State(IReadOnlyList<StorytellerCharacter> choices, Func<Character, AnimalHint?>? animalOf)
    {
        private readonly HashSet<string> _used = [];
        private readonly Dictionary<string, int> _voiceUses = [];

        public void Use(StorytellerCharacter? character, string voiceId)
        {
            if (character is not null)
            {
                _used.Add(character.Id);
            }

            _voiceUses[voiceId] = _voiceUses.GetValueOrDefault(voiceId) + 1;
        }

        public StorytellerCharacter? Pick(Character character, VoiceFlavour wanted)
        {
            AnimalHint? hint = animalOf?.Invoke(character);
            // A story character that is no animal keeps off the animal characters only while a plain one is still free: on a small roster it takes what there is, as before Slice 125.
            bool plainFree = hint is null && choices.Any(c => AnimalCharacters.Of(c.Id) is null && !_used.Contains(c.Id));
            return choices
                .Select((c, order) => (Character: c, Order: order, Cost: Cost(c, character, wanted, hint, plainFree)))
                .OrderBy(x => x.Cost)
                .ThenBy(x => x.Order)
                .Select(x => x.Character)
                .FirstOrDefault();
        }

        private int Cost(StorytellerCharacter candidate, Character character, VoiceFlavour wanted, AnimalHint? hint, bool plainFree)
        {
            int gender = CharacterRoster.GenderMisfit(candidate.Gender, character.Gender);
            int animal = AnimalCost(candidate, hint, plainFree, ref gender);
            int cost = CharacterRoster.FlavourDistance(candidate.Flavour, wanted) + animal + gender;
            cost += _used.Contains(candidate.Id) ? UsedAlready : 0;
            return cost + (_voiceUses.GetValueOrDefault(candidate.VoiceId) * VoiceUsedAlready);
        }

        // An animal story character prefers its own animal character (the voice's gender matters little for an animal), then another young one, over a plain character.
        private static int AnimalCost(StorytellerCharacter candidate, AnimalHint? hint, bool plainFree, ref int gender)
        {
            if (hint is null)
            {
                // A story character that is no animal does not take an animal character either, which would leave a real animal without its own.
                return plainFree && AnimalCharacters.Of(candidate.Id) is not null ? NotAnAnimalCharacter : 0;
            }

            switch (AnimalCharacters.FitOf(candidate, hint))
            {
                case AnimalFit.Exact:
                    gender /= AnimalGenderRelief;
                    return 0;
                case AnimalFit.Young:
                    return OtherYoungAnimal;
                default:
                    return NotAnAnimalCharacter;
            }
        }
    }
}
