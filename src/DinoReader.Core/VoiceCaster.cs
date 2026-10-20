namespace DinoReader.Core;

/// <summary>A voice the caster may hand out: an opaque id and the voice's gender.</summary>
public sealed record VoiceOption(string Id, Gender Gender);

/// <summary>A character's voice; <see cref="IsShared"/> is set when another character has it too.</summary>
public sealed record VoiceAssignment(string CharacterId, string VoiceId, bool IsShared);

/// <summary>The finished cast: the narrator's voice and one assignment per character.</summary>
public sealed record VoiceCast(string NarratorVoiceId, IReadOnlyList<VoiceAssignment> Assignments)
{
    public string? VoiceFor(string characterId) =>
        Assignments.FirstOrDefault(a => a.CharacterId == characterId)?.VoiceId;
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
public sealed class VoiceCaster(int mainCharacterCount = 2)
{
    public VoiceCast Cast(
        IReadOnlyList<Character> characters,
        IReadOnlyList<VoiceOption> voices,
        string narratorVoiceId)
    {
        var pool = voices.Where(v => v.Id != narratorVoiceId).DistinctBy(v => v.Id).ToList();
        var state = new CastState(pool, MainIds(characters));
        var picks = new List<(int Order, Character Character, string VoiceId)>();
        foreach (var (character, order) in MainsFirst(characters, state.IsMain))
        {
            var voice = state.Pick(character);
            if (voice is not null) picks.Add((order, character, voice.Id));
        }
        return new VoiceCast(narratorVoiceId, Flag(picks.OrderBy(p => p.Order).ToList()));
    }

    private static IEnumerable<(Character Character, int Order)> MainsFirst(
        IReadOnlyList<Character> characters, Func<Character, bool> isMain) =>
        characters.Select((c, order) => (Character: c, Order: order))
            .OrderBy(x => isMain(x.Character) ? 0 : 1)
            .ThenBy(x => x.Order);

    private HashSet<string> MainIds(IReadOnlyList<Character> characters) =>
        characters
            .Select((c, order) => (Character: c, Order: order))
            .OrderByDescending(x => x.Character.WordCount)
            .ThenBy(x => x.Order)
            .Take(mainCharacterCount)
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
            if (voice is null) return null;
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
