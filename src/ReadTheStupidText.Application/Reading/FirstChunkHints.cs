namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The heads a phone reader has just prepared for the early start (Slice 139), so the read of the text they start is planned
/// with that head as its first chunk: the first units it asks the sentence cache for are the prepared ones. One instance per
/// reader (no process-wide state), keyed by the voice and language pack the head was prepared in, remembered only once the
/// head is stored, and consumed by the read it was for. Readers that never remember a head (Windows, Dino) plan exactly as before.
/// </summary>
internal sealed class FirstChunkHints
{
    private const int Capacity = 4;

    private readonly object _gate = new();
    private Entry[] _entries = [];

    private readonly record struct Entry(string Head, string Scope);

    /// <summary>The key a head is prepared and read under: the requested voice and the language pack (null: the reader's own).</summary>
    public static string ScopeOf(string? voiceId, VoiceModelPaths? model) =>
        model is null
            ? $"|{voiceId}"
            : $"{model.Kind}|{model.Language}|{model.RootDir}|{model.ModelDir}|{model.PackVersion}|{voiceId}";

    /// <summary>Remembers the stored <paramref name="text"/> as a head for <paramref name="scope"/> when it is one the chunker
    /// would accept (a whole sentence boundary within <paramref name="policy"/>). Returns whether it was kept.</summary>
    public bool Remember(string text, string scope, SpeechTextChunker.HeadPolicy policy)
    {
        string head = Normalize(text);
        if (!SpeechTextChunker.AcceptsHint(head, head, policy))
        {
            return false;
        }

        lock (_gate)
        {
            Entry[] kept = Array.FindAll(_entries, e => !(e.Scope == scope && e.Head == head));
            int skip = Math.Max(0, kept.Length + 1 - Capacity);
            Entry[] next = new Entry[kept.Length - skip + 1];
            Array.Copy(kept, skip, next, 0, kept.Length - skip);
            next[^1] = new Entry(head, scope);
            Volatile.Write(ref _entries, next);
        }

        return true;
    }

    /// <summary>The longest remembered head for <paramref name="scope"/> that <paramref name="text"/> starts with, which is
    /// forgotten here (the read it is for takes it), or null.</summary>
    public string? Take(string text, string scope)
    {
        Entry[] snapshot = Volatile.Read(ref _entries);
        if (snapshot.Length == 0)
        {
            return null;
        }

        string reflowed = Normalize(text);
        Entry? best = null;
        foreach (Entry entry in snapshot)
        {
            bool longer = best is null || entry.Head.Length > best.Value.Head.Length;
            if (longer && entry.Scope == scope && reflowed.StartsWith(entry.Head, StringComparison.Ordinal))
            {
                best = entry;
            }
        }

        if (best is { } taken)
        {
            Forget(taken);
            return taken.Head;
        }

        return null;
    }

    private void Forget(Entry taken)
    {
        lock (_gate)
        {
            Volatile.Write(ref _entries, Array.FindAll(_entries, e => e != taken));
        }
    }

    private static string Normalize(string text) => LineReflow.ReflowText(text ?? string.Empty).Trim();
}
