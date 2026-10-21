namespace DinoReader.Core;

/// <summary>
/// Keeps a character's voice when a story is spoken in another language (Slice 66, Decision 74): the
/// same voice when the other language's pack has it (the Supertonic styles serve every language they
/// speak), otherwise the pack's voice of the same gender at the same place in the list, so two
/// characters still sound different.
/// </summary>
public static class VoiceRemap
{
    public static string Map(string voiceId, IReadOnlyList<VoiceOption> from, IReadOnlyList<VoiceOption> to)
    {
        if (to.Count == 0 || to.Any(v => v.Id == voiceId)) return voiceId;
        VoiceOption? source = from.FirstOrDefault(v => v.Id == voiceId);
        var sameGender = source is null || source.Gender == Gender.Unknown
            ? to
            : to.Where(v => v.Gender == source.Gender || v.Gender == Gender.Unknown).ToList();
        var pool = sameGender.Count > 0 ? sameGender : to;
        int position = source is null ? 0 : from.Where(v => v.Gender == source.Gender).ToList().FindIndex(v => v.Id == voiceId);
        return pool[Math.Max(position, 0) % pool.Count].Id;
    }

    /// <summary>
    /// Maps a whole cast at once (narrator first), so two voices never land on the same voice while the other
    /// language's pack has a free one. A voice the pack already has is kept; the rest take a free voice of the same gender
    /// (an Unknown-gender voice on either side matches any), then any free voice, then share one when the pack is smaller.
    /// </summary>
    public static IReadOnlyDictionary<string, string> MapSet(
        IEnumerable<string> voiceIds, IReadOnlyList<VoiceOption> from, IReadOnlyList<VoiceOption> to)
    {
        var ids = voiceIds.Distinct().ToList();
        var result = new Dictionary<string, string>();
        var used = new HashSet<string>();
        foreach (string id in ids.Where(id => to.Any(v => v.Id == id)))
        {
            result[id] = id;
            used.Add(id);
        }

        foreach (string id in ids.Where(id => !result.ContainsKey(id)))
        {
            string mapped = FreeVoice(id, from, to, used) ?? Map(id, from, to);
            result[id] = mapped;
            used.Add(mapped);
        }

        return result;
    }

    private static string? FreeVoice(string voiceId, IReadOnlyList<VoiceOption> from, IReadOnlyList<VoiceOption> to, HashSet<string> used)
    {
        var free = to.Where(v => !used.Contains(v.Id)).ToList();
        VoiceOption? source = from.FirstOrDefault(v => v.Id == voiceId);
        bool anyGender = source is null || source.Gender == Gender.Unknown;
        var sameGender = anyGender ? free : free.Where(v => v.Gender == source!.Gender || v.Gender == Gender.Unknown).ToList();
        return (sameGender.Count > 0 ? sameGender : free).FirstOrDefault()?.Id;
    }
}
