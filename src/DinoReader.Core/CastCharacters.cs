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
/// cast to its animal character first (piglet for a pig, big bear for a grown bear); when its own animal's characters are all taken it reuses them, the least used
/// first, if the animal has more than one, else takes another young animal character. Only a voice a grown-up picked is never replaced.
/// </summary>
public static class CastCharacters
{
    private const int UsedAlready = 30;
    private const int VoiceUsedAlready = 3;
    private const int NotAnAnimalCharacter = 25;

    // An animal story character stays its own animal as long as there is a character for it: its own animal's characters (reused, the least used first, once all are
    // taken) always beat another young animal, which beats a plain character (Slice 135: the third pig is a piglet again, never a puppy). These are tiers compared before
    // any cost, so no number of reuses can make another animal cheaper. A reused character gets a flavour it has not sounded with yet, so two story characters of one kind
    // sound different.
    private const int AnimalGenderRelief = 10;
    private static readonly VoiceFlavour[] ReuseFlavours = [VoiceFlavour.Small, VoiceFlavour.Squeaky, VoiceFlavour.Bouncy, VoiceFlavour.Chirpy, VoiceFlavour.Perky, VoiceFlavour.Normal, VoiceFlavour.Whisper, VoiceFlavour.Sleepy, VoiceFlavour.Big];

    // A reused young animal (piglet, lamb, duckling) never gets a flavour that sounds grown: it would turn into a deep bear.
    private static readonly HashSet<VoiceFlavour> OldSounding = [VoiceFlavour.Big, VoiceFlavour.Sleepy, VoiceFlavour.Giant, VoiceFlavour.Grumpy, VoiceFlavour.Mellow];

    private enum AnimalTier
    {
        Own,
        OtherYoungAnimal,
        NotTheAnimal,
    }

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
        IReadOnlyList<StorytellerCharacter> narratorVoiceAnimals = NarratorVoiceAnimals(choices, roster, narratorVoiceId);
        var state = new State(choices, narratorVoiceAnimals, roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), animalOf);
        var picks = new List<(int Order, Character Character, string VoiceId, VoiceFlavour Flavour)>();
        var kept = Kept(characters, choices, narratorVoiceAnimals, roster, guess, pinned, animalOf);
        foreach ((Character character, int order) in characters.Select((c, i) => (c, i)).Where(x => kept.ContainsKey(x.c.Id)))
        {
            (string voice, VoiceFlavour flavour, StorytellerCharacter? near) = kept[character.Id];
            state.Use(near, voice, flavour);
            picks.Add((order, character, voice, flavour));
        }

        HashSet<string> mains = VoiceCaster.MainIds(characters, mainCharacterCount);
        foreach ((Character character, int order) in MainsFirst(characters, mains).Where(x => !kept.ContainsKey(x.Character.Id)))
        {
            StorytellerCharacter? pick = state.Pick(character, guess(character));
            if (pick is not null)
            {
                state.Use(pick, pick.VoiceId, pick.Flavour);
                picks.Add((order, character, pick.VoiceId, pick.Flavour));
            }
        }

        var ordered = picks.OrderBy(p => p.Order).ToList();
        return new VoiceCast(narratorVoiceId, Flag(ordered), ordered.ToDictionary(p => p.Character.Id, p => p.Flavour));
    }

    // The narrator's voice is left out of the choices so no character sounds like the narrator, but an animal's own characters on it (Farsi, Slice 135: two of the three
    // piglets sit on the narrator's voice) are still offered to animal story characters when their flavour is not the narrator's, which the pitch and pace tell apart.
    private static IReadOnlyList<StorytellerCharacter> NarratorVoiceAnimals(
        IReadOnlyList<StorytellerCharacter> choices, IReadOnlyList<StorytellerCharacter> roster, string narratorVoiceId)
    {
        VoiceFlavour narratorFlavour = roster.FirstOrDefault(c => c.Kind == CharacterKind.Narrator && c.VoiceId == narratorVoiceId)?.Flavour ?? VoiceFlavour.Storyteller;
        return roster
            .Where(c => c.Kind == CharacterKind.Speaker && c.VoiceId == narratorVoiceId && c.Flavour != narratorFlavour)
            .Where(c => AnimalCharacters.Of(c.Id) is not null && choices.All(o => o.Id != c.Id))
            .ToList();
    }

    // A stored voice that is still among the choices' voices stays, with its stored flavour (else the guess): the pick of an earlier read or of a grown-up.
    private static Dictionary<string, (string Voice, VoiceFlavour Flavour, StorytellerCharacter? Near)> Kept(
        IReadOnlyList<Character> characters, IReadOnlyList<StorytellerCharacter> choices, IReadOnlyList<StorytellerCharacter> narratorVoiceAnimals,
        IReadOnlyList<StorytellerCharacter> roster, Func<Character, VoiceFlavour> guess, IReadOnlyDictionary<string, PinnedCharacter>? pinned,
        Func<Character, AnimalHint?>? animalOf)
    {
        var result = new Dictionary<string, (string, VoiceFlavour, StorytellerCharacter?)>();
        if (pinned is null)
        {
            return result;
        }

        var voices = choices.Select(c => c.VoiceId).ToHashSet();
        var animalVoices = voices.Union(narratorVoiceAnimals.Select(c => c.VoiceId)).ToHashSet();
        var speakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        var sounds = new HashSet<(string, VoiceFlavour)>();
        foreach (Character character in characters.OrderBy(c => pinned.TryGetValue(c.Id, out PinnedCharacter? p) && p.Edited ? 0 : 1))
        {
            if (!pinned.TryGetValue(character.Id, out PinnedCharacter? stored) || !(animalOf?.Invoke(character) is null ? voices : animalVoices).Contains(stored.VoiceId))
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
    private sealed class State(
        IReadOnlyList<StorytellerCharacter> choices, IReadOnlyList<StorytellerCharacter> narratorVoiceAnimals, IReadOnlyList<StorytellerCharacter> speakers,
        Func<Character, AnimalHint?>? animalOf)
    {
        private readonly Dictionary<(string CharacterId, string VoiceId, Gender Gender, VoiceFlavour Flavour), bool> _foreignSounds = [];
        private readonly Dictionary<string, int> _used = [];
        private readonly Dictionary<string, int> _voiceUses = [];
        private readonly HashSet<(string VoiceId, VoiceFlavour Flavour)> _sounds = [];

        public void Use(StorytellerCharacter? character, string voiceId, VoiceFlavour flavour)
        {
            _sounds.Add((voiceId, flavour));
            if (character is not null)
            {
                _used[character.Id] = _used.GetValueOrDefault(character.Id) + 1;
            }

            _voiceUses[voiceId] = _voiceUses.GetValueOrDefault(voiceId) + 1;
        }

        public StorytellerCharacter? Pick(Character character, VoiceFlavour wanted)
        {
            AnimalHint? hint = animalOf?.Invoke(character);
            // A story character that is no animal keeps off the animal characters only while a plain one is still free: on a small roster it takes what there is, as before Slice 125.
            bool plainFree = hint is null && choices.Any(c => AnimalCharacters.Of(c.Id) is null && !_used.ContainsKey(c.Id));
            IEnumerable<StorytellerCharacter> candidates = hint is null ? choices : choices.Concat(narratorVoiceAnimals);
            StorytellerCharacter? pick = candidates
                .Select((c, order) => ScoreOf(c, order, character, wanted, hint, plainFree))
                .OrderBy(x => x.Tier)
                .ThenBy(x => x.Cost)
                .ThenBy(x => x.Order)
                .Select(x => x.Character)
                .FirstOrDefault();
            return pick is not null && hint is not null && Tier(pick, hint) == AnimalTier.Own ? Distinct(pick) : pick;
        }

        private (StorytellerCharacter Character, int Order, AnimalTier Tier, int Cost) ScoreOf(
            StorytellerCharacter candidate, int order, Character character, VoiceFlavour wanted, AnimalHint? hint, bool plainFree)
        {
            AnimalTier tier = Tier(candidate, hint);
            return (candidate, order, tier, Cost(candidate, character, wanted, hint, plainFree, tier));
        }

        private static AnimalTier Tier(StorytellerCharacter candidate, AnimalHint? hint)
        {
            if (hint is null)
            {
                return AnimalTier.Own;
            }

            return AnimalCharacters.FitOf(candidate, hint) switch
            {
                AnimalFit.Exact => AnimalTier.Own,
                AnimalFit.Young => AnimalTier.OtherYoungAnimal,
                _ => AnimalTier.NotTheAnimal,
            };
        }

        // A reused animal character takes the first flavour that its voice has not sounded with yet, so the reuse sounds different; none left, it stays as it is.
        // A flavour the cast screen would name another animal is skipped (Farsi, Slice 135: a third pig shown as the bear cub); a reuse with no such free flavour shares the
        // sound it has, which the cast screen flags.
        private StorytellerCharacter Distinct(StorytellerCharacter pick)
        {
            if (!_used.ContainsKey(pick.Id) || !_sounds.Contains((pick.VoiceId, pick.Flavour)))
            {
                return pick;
            }

            bool young = AnimalCharacters.Of(pick.Id)?.Young ?? false;
            VoiceFlavour? fresh = ReuseFlavours.Cast<VoiceFlavour?>()
                .FirstOrDefault(f => !_sounds.Contains((pick.VoiceId, f!.Value)) && !(young && OldSounding.Contains(f.Value)) && !SoundsLikeAnotherAnimal(pick, f.Value));
            return fresh is { } flavour ? pick with { Flavour = flavour } : pick;
        }

        // Judged the way the cast screen names a sound: the nearest speaker of the whole roster, by voice, flavour family and distance (with the character's gender, or none).
        private bool SoundsLikeAnotherAnimal(StorytellerCharacter pick, VoiceFlavour flavour)
        {
            if (!_foreignSounds.TryGetValue((pick.Id, pick.VoiceId, pick.Gender, flavour), out bool foreign))
            {
                foreign = new[] { pick.Gender, Gender.Unknown }.Any(g => IsAnotherAnimal(pick, CharacterRoster.Nearest(speakers, pick.VoiceId, flavour, g)));
                _foreignSounds[(pick.Id, pick.VoiceId, pick.Gender, flavour)] = foreign;
            }

            return foreign;
        }

        private static bool IsAnotherAnimal(StorytellerCharacter pick, StorytellerCharacter? named)
        {
            if (named is null || named.Id == pick.Id)
            {
                return false;
            }

            var own = AnimalCharacters.Of(pick.Id)?.Kinds ?? [];
            return !own.Intersect(AnimalCharacters.Of(named.Id)?.Kinds ?? []).Any();
        }

        private int Cost(StorytellerCharacter candidate, Character character, VoiceFlavour wanted, AnimalHint? hint, bool plainFree, AnimalTier tier)
        {
            int gender = CharacterRoster.GenderMisfit(candidate.Gender, character.Gender);
            int animal = 0;
            if (hint is null)
            {
                // A story character that is no animal does not take an animal character either, which would leave a real animal without its own.
                animal = plainFree && AnimalCharacters.Of(candidate.Id) is not null ? NotAnAnimalCharacter : 0;
            }
            else if (tier == AnimalTier.Own)
            {
                // The voice's gender matters little for an animal.
                gender /= AnimalGenderRelief;
            }

            int cost = CharacterRoster.FlavourDistance(candidate.Flavour, wanted) + animal + gender;
            cost += _used.GetValueOrDefault(candidate.Id) * UsedAlready;
            return cost + (_voiceUses.GetValueOrDefault(candidate.VoiceId) * VoiceUsedAlready);
        }
    }
}
