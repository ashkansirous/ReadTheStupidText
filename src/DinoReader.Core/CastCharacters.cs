using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>
/// A voice and flavour already stored for a character, which a recast keeps while the voice is still there. A grown-up's pick (<paramref name="Edited"/>) is kept whatever else
/// sounds the same; an automatic one is dropped when it would sound like a pick already kept (an older voice-only cast gave several characters one voice).
/// </summary>
public sealed record PinnedCharacter(string VoiceId, VoiceFlavour? Flavour, bool Edited = true, string? SuggestedVoice = null, CharacterTraits? Traits = null);

/// <summary>
/// Casts a story's characters with storyteller characters (Slice 116, Decision 122), the successor of the voice-only <see cref="VoiceCaster"/>.
/// Each story character gets the roster character that fits its name best: the gender, then the flavour the name guesses ("Little Pig" is
/// Small, so Cheeky Piglet), and no two story characters share a roster character while the roster has another that fits. Fully deterministic:
/// stored picks first, then the main characters (most words) and the rest in the order given; ties break by roster order. The result is the
/// same <see cref="VoiceCast"/> the rest of Dino reads: a voice per character and its flavour. A story character that is an animal (<c>animalOf</c>, Slice 125) is
/// cast to its animal character first (piglet for a pig, big bear for a grown bear); when its own animal's characters are all taken it reuses them, the least used
/// first, if the animal has more than one, else takes another young animal character. Only a voice a grown-up picked is never replaced.
/// A character whose book chose its voice (Slice 156, <see cref="PinnedCharacter.SuggestedVoice"/>) gets that roster character while the language has it and its gender does not clash;
/// otherwise the roster character closest to the book's traits (<see cref="CharacterTraitMatch"/>); a grown-up's pick still wins over both.
/// </summary>
public static class CastCharacters
{
    private const string HomeLanguage = "en";
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
        var state = new State(choices, narratorVoiceAnimals, roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), animalOf, WantedBy(pinned, animalOf), narratorVoiceId);
        var picks = new List<(int Order, Character Character, string VoiceId, VoiceFlavour Flavour)>();
        var kept = Kept(characters, choices, narratorVoiceAnimals, roster, narratorVoiceId, guess, pinned, animalOf);
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

    private sealed record WantedTraits(CharacterTraits Traits, AnimalKind? Animal);

    private static Func<Character, WantedTraits?> WantedBy(IReadOnlyDictionary<string, PinnedCharacter>? pinned, Func<Character, AnimalHint?>? animalOf) =>
        c => pinned is not null && pinned.TryGetValue(c.Id, out PinnedCharacter? p) && p.Traits is { } traits ? new WantedTraits(traits, animalOf?.Invoke(c)?.Kind) : null;

    // The roster character a book suggests for a character (Slice 156), while the language's roster has it and its gender is not the opposite of the character's.
    private static StorytellerCharacter? BookCharacter(Character character, PinnedCharacter? stored, IReadOnlyList<StorytellerCharacter> speakers)
    {
        if (stored?.SuggestedVoice is not { Length: > 0 } id)
        {
            return null;
        }

        StorytellerCharacter? suggested = speakers.FirstOrDefault(s => s.Id == id);
        bool fits = suggested is not null && !CharacterRoster.OppositeGender(suggested.Gender, character.Gender) && !SwapsInWhisper(suggested, stored.Traits);
        return fits ? suggested : null;
    }

    /// <summary>Whether a book's suggestion is dropped for its whisper (Slice 156): the place is not whispery in English (the roster the books are written against, so the author never chose a whisper) but is
    /// in this language, and the book does not describe the character as whispery. Such a character falls back to its traits; a place that is whispery in English (a sly fox) stays in every language.</summary>
    public static bool SwapsInWhisper(StorytellerCharacter suggested, CharacterTraits? traits) =>
        SoundsWhispered(suggested) && !IsWhispery(traits) && !WhisperedAtHome(suggested.Id);

    private static bool WhisperedAtHome(string id) =>
        CharacterRoster.Speakers(HomeLanguage).FirstOrDefault(c => c.Id == id) is { } home && SoundsWhispered(home);

    // A whispered place (Whisper, Hushed) is only for a character the book describes as whispery.
    private static bool SoundsWhispered(StorytellerCharacter candidate) => candidate.Flavour is VoiceFlavour.Whisper or VoiceFlavour.Hushed;

    private static bool IsWhispery(CharacterTraits? traits) => traits?.Qualities?.Contains(VoiceQuality.Whispery) == true;

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
        IReadOnlyList<StorytellerCharacter> roster, string narratorVoiceId, Func<Character, VoiceFlavour> guess, IReadOnlyDictionary<string, PinnedCharacter>? pinned,
        Func<Character, AnimalHint?>? animalOf)
    {
        var result = new Dictionary<string, (string, VoiceFlavour, StorytellerCharacter?)>();
        if (pinned is null)
        {
            return result;
        }

        var voices = choices.Select(c => c.VoiceId).ToHashSet();
        var standingVoices = new StandingVoices(voices, voices.Union(narratorVoiceAnimals.Select(c => c.VoiceId)).ToHashSet());
        var allSpeakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        var bookable = BookableSpeakers(roster, choices, narratorVoiceAnimals, narratorVoiceId);
        var sounds = new HashSet<(string, VoiceFlavour)>();
        foreach (Character character in characters.OrderBy(c => KeepRank(c, pinned, bookable)))
        {
            pinned.TryGetValue(character.Id, out PinnedCharacter? stored);
            bool standing = stored is not null && (standingVoices.For(animalOf?.Invoke(character) is not null).Contains(stored.VoiceId) || IsEditedEnginePick(stored, voices));
            if (KeepBookPick(character, stored, standing, bookable, sounds) is { } book)
            {
                result[character.Id] = (book.VoiceId, book.Flavour, book);
            }
            else if (standing && KeepStoredPick(character, stored!, guess, allSpeakers, sounds) is { } kept)
            {
                result[character.Id] = kept;
            }
        }

        return result;
    }

    // The voice ids of each Kokoro language, taken from the voice table once.
    private static readonly IReadOnlyList<HashSet<string>> KokoroPools =
        KokoroVoiceTable.Languages.Select(l => KokoroVoiceTable.VoicesFor(l).Select(v => v.Id).ToHashSet()).ToList();

    // A grown-up's own pick stays while its voice is in the Kokoro language of the book's choices, even when no roster place uses it any more (bf_emma, Slice 153). It stands only in the
    // language it was made for: an edited pick from another Kokoro language (an English voice left on a Spanish book) is not kept and is re-picked.
    private static bool IsEditedEnginePick(PinnedCharacter stored, HashSet<string> choiceVoices) =>
        stored.Edited && KokoroPools.Any(pool => pool.Contains(stored.VoiceId) && pool.Overlaps(choiceVoices));

    // The voices a stored pick may stand on: the choices' voices, plus the narrator-voice animals' for an animal character.
    private sealed record StandingVoices(HashSet<string> Plain, HashSet<string> WithAnimals)
    {
        public HashSet<string> For(bool isAnimal) => isAnimal ? WithAnimals : Plain;
    }

    // The book's own pick is kept as long as its roster id exists in this language, unless a grown-up's pick still stands or another character already sounds like it.
    private static StorytellerCharacter? KeepBookPick(
        Character character, PinnedCharacter? stored, bool standing, IReadOnlyList<StorytellerCharacter> bookable, HashSet<(string, VoiceFlavour)> sounds)
    {
        if (stored is { Edited: true } && standing)
        {
            return null;
        }

        StorytellerCharacter? book = BookCharacter(character, stored, bookable);
        return book is not null && sounds.Add((book.VoiceId, book.Flavour)) ? book : null;
    }

    private static (string, VoiceFlavour, StorytellerCharacter?)? KeepStoredPick(
        Character character, PinnedCharacter stored, Func<Character, VoiceFlavour> guess, IReadOnlyList<StorytellerCharacter> allSpeakers, HashSet<(string, VoiceFlavour)> sounds)
    {
        VoiceFlavour flavour = stored.Flavour ?? guess(character);
        if (!sounds.Add((stored.VoiceId, flavour)) && !stored.Edited)
        {
            return null;
        }

        return (stored.VoiceId, flavour, CharacterRoster.Nearest(allSpeakers, stored.VoiceId, flavour, character.Gender));
    }

    /// <summary>The roster characters a book's suggestion may name in a language (Slice 156): not one on the narrator's own voice, so nobody sounds like the narrator. The cast screen offers "Back to the book's voice" only for these.</summary>
    public static IReadOnlyList<StorytellerCharacter> BookableSpeakers(IReadOnlyList<StorytellerCharacter> roster, string narratorVoiceId)
    {
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narratorVoiceId);
        return BookableSpeakers(roster, choices, NarratorVoiceAnimals(choices, roster, narratorVoiceId), narratorVoiceId);
    }

    // The roster characters a book's suggestion may name: not one on the narrator's own voice, unless it is offered anyway (a narrator-voice animal), so nobody sounds like the narrator.
    private static List<StorytellerCharacter> BookableSpeakers(
        IReadOnlyList<StorytellerCharacter> roster, IReadOnlyList<StorytellerCharacter> choices, IReadOnlyList<StorytellerCharacter> narratorVoiceAnimals, string narratorVoiceId) =>
        roster.Where(c => c.Kind == CharacterKind.Speaker)
            .Select(c => choices.FirstOrDefault(o => o.Id == c.Id) ?? c)
            .Where(c => c.VoiceId != narratorVoiceId || choices.Any(o => o.Id == c.Id) || narratorVoiceAnimals.Any(o => o.Id == c.Id))
            .ToList();

    // A grown-up's pick is kept first, then the book's, then the earlier automatic ones.
    private static int KeepRank(Character character, IReadOnlyDictionary<string, PinnedCharacter> pinned, IReadOnlyList<StorytellerCharacter> speakers)
    {
        pinned.TryGetValue(character.Id, out PinnedCharacter? stored);
        if (stored is { Edited: true })
        {
            return 0;
        }

        return BookCharacter(character, stored, speakers) is not null ? 1 : 2;
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
        Func<Character, AnimalHint?>? animalOf, Func<Character, WantedTraits?> wantedOf, string narratorVoiceId)
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
            if (wantedOf(character) is { } byTraits)
            {
                return PickByTraits(character, byTraits, animalOf?.Invoke(character));
            }

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
            return Reuse(NotWhispered(pick, hint, wanted is VoiceFlavour.Whisper or VoiceFlavour.Hushed), hint);
        }

        // A person who is not whispery never ends on a whispered place of the narrator's voice (a language with one woman's voice has only that): they take that voice in a Normal
        // flavour, else its nearest calm one, else the nearest calm place of another voice; a whisper is the very last resort. They may share a sound with an earlier character.
        private StorytellerCharacter? NotWhispered(StorytellerCharacter? pick, AnimalHint? hint, bool whispery)
        {
            if (pick is null || hint is not null || whispery || !SoundsWhispered(pick) || pick.VoiceId != narratorVoiceId)
            {
                return pick;
            }

            return choices
                .Where(c => !SoundsWhispered(c))
                .OrderBy(c => c.VoiceId == pick.VoiceId ? 0 : 1)
                .ThenBy(c => CharacterRoster.GenderMisfit(c.Gender, pick.Gender))
                .ThenBy(c => CharacterRoster.FlavourDistance(c.Flavour, VoiceFlavour.Normal))
                .FirstOrDefault() ?? pick;
        }

        // One rule for both pick paths: a pick that is the story animal's own character (an exact fit) is made to sound unlike an earlier use of the same sound.
        private StorytellerCharacter? Reuse(StorytellerCharacter? pick, AnimalHint? hint) =>
            pick is not null && hint is not null && Tier(pick, hint) == AnimalTier.Own ? Distinct(pick) : pick;

        // A character the book described but whose suggested voice the language lacks (Slice 156): the closest roster character by traits, avoiding repeats as the picker does.
        // A reused animal character gets a flavour its voice has not sounded with, as the picker does, so two of the same animal sound alike only when no fresh flavour is left.
        private StorytellerCharacter? PickByTraits(Character character, WantedTraits wanted, AnimalHint? hint)
        {
            StorytellerCharacter? pick = AvoidingWhispers(hint is not null ? choices.Concat(narratorVoiceAnimals) : choices, wanted.Traits)
                .Select((c, order) => (Character: c, Order: order, Cost: TraitPickCost(c, character, wanted)))
                .OrderBy(x => x.Cost)
                .ThenBy(x => x.Order)
                .Select(x => x.Character)
                .FirstOrDefault();
            return Reuse(NotWhispered(pick, hint, IsWhispery(wanted.Traits)), hint);
        }

        // A character that is not whispery keeps off the whispered places while any other place is left (the roster of a language with few voices has some).
        private static List<StorytellerCharacter> AvoidingWhispers(IEnumerable<StorytellerCharacter> candidates, CharacterTraits traits)
        {
            var all = candidates.ToList();
            var calm = all.Where(c => !SwapsInWhisper(c, traits)).ToList();
            return calm.Count == 0 ? all : calm;
        }

        private int TraitPickCost(StorytellerCharacter candidate, Character character, WantedTraits wanted) =>
            CharacterTraitMatch.Cost(candidate, character.Gender, wanted.Traits, wanted.Animal)
            + (_used.GetValueOrDefault(candidate.Id) * UsedAlready) + (_voiceUses.GetValueOrDefault(candidate.VoiceId) * VoiceUsedAlready);

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

        // An animal character whose (voice, flavour) sound is already taken, by any roster character, takes the first flavour its voice has not sounded with yet; none left, it stays as it is.
        // A flavour the cast screen would name another animal is skipped (Farsi, Slice 135: a third pig shown as the bear cub); a reuse with no such free flavour shares the
        // sound it has, which the cast screen flags.
        private StorytellerCharacter Distinct(StorytellerCharacter pick)
        {
            if (!_sounds.Contains((pick.VoiceId, pick.Flavour)))
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
