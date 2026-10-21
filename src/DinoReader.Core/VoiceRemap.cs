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
}
