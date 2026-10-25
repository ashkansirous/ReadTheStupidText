using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>A voice the caster may hand out: an opaque id and the voice's gender.</summary>
public sealed record VoiceOption(string Id, Gender Gender);

/// <summary>A character's voice; <see cref="IsShared"/> is set when another character has it too.</summary>
public sealed record VoiceAssignment(string CharacterId, string VoiceId, bool IsShared);

/// <summary>The finished cast: the narrator's voice and one assignment per character.</summary>
public sealed record VoiceCast(
    string NarratorVoiceId, IReadOnlyList<VoiceAssignment> Assignments,
    IReadOnlyDictionary<string, VoiceFlavour>? Flavours = null)
{
    /// <summary>The flavour that shapes a line's voice (Slice 113): the narrator's storyteller pace for narration and for a quote nobody
    /// could be attributed to, else the speaker's, Normal when none is known.</summary>
    public VoiceFlavour FlavourForLine(string? characterId) =>
        characterId is not null && VoiceFor(characterId) is not null
            ? Flavours?.GetValueOrDefault(characterId) ?? VoiceFlavour.Normal
            : VoiceFlavour.Storyteller;

    /// <summary>The cast with each character's flavour set.</summary>
    public VoiceCast WithFlavours(IReadOnlyDictionary<string, VoiceFlavour> flavours) => this with { Flavours = flavours };

    public string? VoiceFor(string characterId) =>
        Assignments.FirstOrDefault(a => a.CharacterId == characterId)?.VoiceId;

    /// <summary>The voice that reads a line: the speaker's, and the narrator's for narration, for a quote
    /// nobody could be attributed to (the unknown-speaker rule, Slice 65) and for a character with no voice.</summary>
    public string VoiceForLine(string? characterId) =>
        (characterId is null ? null : VoiceFor(characterId)) ?? NarratorVoiceId;
}

/// <summary>
/// Casts voices to characters (Decision 75). The narrator's voice is never used. While free
/// voices remain no two characters share one (gender-matched first); after that a character
/// gets the gender-matched voice that has spoken the fewest words so far, never a main
/// character's voice. If the pack has too few voices for that, mains may share. Fully
/// deterministic: main characters are cast first (in the given order) so they get their
/// own voices whatever the order of appearance, then the rest in the given order; ties
/// break by voice order. Duplicate voice ids are dropped, the first one winning.
/// </summary>
public sealed class VoiceCaster(int mainCharacterCount = VoiceCaster.DefaultMainCount)
{
    /// <summary>How many characters, by words spoken, count as main characters.</summary>
    public const int DefaultMainCount = 2;

    public VoiceCast Cast(
        IReadOnlyList<Character> characters,
        IReadOnlyList<VoiceOption> voices,
        string narratorVoiceId,
        IReadOnlyDictionary<string, string>? pinned = null)
    {
        var pool = voices.Where(v => v.Id != narratorVoiceId).DistinctBy(v => v.Id).ToList();
        var state = new CastState(pool, MainIds(characters, mainCharacterCount));
        var picks = new List<(int Order, Character Character, string VoiceId)>();
        var pins = PinnedVoices(characters, pool, pinned);
        foreach (var (character, order) in characters.Select((c, i) => (c, i)).Where(x => pins.ContainsKey(x.c.Id)))
        {
            picks.Add((order, character, state.Take(character, pins[character.Id]).Id));
        }
        foreach (var (character, order) in MainsFirst(characters, state.IsMain).Where(x => !pins.ContainsKey(x.Character.Id)))
        {
            var voice = state.Pick(character);
            if (voice is not null) picks.Add((order, character, voice.Id));
        }
        return new VoiceCast(narratorVoiceId, Flag(picks.OrderBy(p => p.Order).ToList()));
    }

    // Pinned (already stored) voices that are still in the pool. They are taken up front so the
    // rest of the cast never lands on one while another voice is free.
    private static Dictionary<string, VoiceOption> PinnedVoices(
        IReadOnlyList<Character> characters, List<VoiceOption> pool, IReadOnlyDictionary<string, string>? pinned)
    {
        var result = new Dictionary<string, VoiceOption>();
        if (pinned is null) return result;
        foreach (var character in characters)
        {
            var voice = pinned.TryGetValue(character.Id, out var id) ? pool.FirstOrDefault(v => v.Id == id) : null;
            if (voice is not null) result[character.Id] = voice;
        }
        return result;
    }

    private static IEnumerable<(Character Character, int Order)> MainsFirst(
        IReadOnlyList<Character> characters, Func<Character, bool> isMain) =>
        characters.Select((c, order) => (Character: c, Order: order))
            .OrderBy(x => isMain(x.Character) ? 0 : 1)
            .ThenBy(x => x.Order);

    /// <summary>The ids of the <paramref name="count"/> characters with the most words (ties: first appearance).</summary>
    public static HashSet<string> MainIds(IReadOnlyList<Character> characters, int count) =>
        characters
            .Select((c, order) => (Character: c, Order: order))
            .OrderByDescending(x => x.Character.WordCount)
            .ThenBy(x => x.Order)
            .Take(count)
            .Select(x => x.Character.Id)
            .ToHashSet();

    private static List<VoiceAssignment> Flag(List<(int Order, Character Character, string VoiceId)> picks)
    {
        var uses = picks.GroupBy(p => p.VoiceId).ToDictionary(g => g.Key, g => g.Count());
        return picks
            .Select(p => new VoiceAssignment(p.Character.Id, p.VoiceId, uses[p.VoiceId] > 1))
            .ToList();
    }

    /// <summary>Running state of one casting pass: who owns each voice and its word count.</summary>
    private sealed class CastState(List<VoiceOption> pool, HashSet<string> mains)
    {
        private readonly Dictionary<string, int> _words = pool.ToDictionary(v => v.Id, _ => 0);
        private readonly HashSet<string> _owned = [];
        private readonly HashSet<string> _mainOwned = [];

        public bool IsMain(Character character) => mains.Contains(character.Id);

        public VoiceOption? Pick(Character character)
        {
            var voice = PickFree(character.Gender) ?? PickFewestWords(character.Gender);
            return voice is null ? null : Take(character, voice);
        }

        public VoiceOption Take(Character character, VoiceOption voice)
        {
            _owned.Add(voice.Id);
            _words[voice.Id] += character.WordCount;
            if (mains.Contains(character.Id)) _mainOwned.Add(voice.Id);
            return voice;
        }

        private VoiceOption? PickFree(Gender gender)
        {
            var free = pool.Where(v => !_owned.Contains(v.Id)).ToList();
            return free.FirstOrDefault(v => Matches(v, gender)) ?? free.FirstOrDefault();
        }

        private VoiceOption? PickFewestWords(Gender gender)
        {
            var shareable = pool.Where(v => !_mainOwned.Contains(v.Id)).ToList();
            if (shareable.Count == 0) shareable = pool;
            var matching = shareable.Where(v => Matches(v, gender)).ToList();
            return Fewest(matching.Count > 0 ? matching : shareable);
        }

        private VoiceOption? Fewest(List<VoiceOption> candidates) =>
            candidates.OrderBy(v => _words[v.Id]).FirstOrDefault();

        private static bool Matches(VoiceOption voice, Gender gender) =>
            gender == Gender.Unknown || voice.Gender == gender;
    }
}
