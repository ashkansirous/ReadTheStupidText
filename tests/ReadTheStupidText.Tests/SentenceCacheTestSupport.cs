using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

/// <summary>An in-memory <see cref="ISentenceStore"/> that records deletes, so the cache's LRU
/// logic is tested without a disk.</summary>
internal sealed class InMemorySentenceStore : ISentenceStore
{
    private readonly Dictionary<string, byte[]> _files = new();

    public List<string> Deleted { get; } = [];

    public bool Exists(string id) => _files.ContainsKey(id);

    public IReadOnlyList<StoredSentence> List() =>
        _files.Select(f => new StoredSentence(f.Key, f.Key, f.Value.Length, TimeSpan.Zero)).ToList();

    public Task<byte[]?> ReadAsync(string id) => Task.FromResult(_files.GetValueOrDefault(id));

    public Task<StoredSentence> WriteAsync(string id, byte[] wav)
    {
        _files[id] = wav;
        return Task.FromResult(new StoredSentence(id, id, wav.Length, WavFile.TryGetDuration(wav) ?? TimeSpan.Zero));
    }

    public void Touch(string id)
    {
    }

    public void Delete(string id)
    {
        _files.Remove(id);
        Deleted.Add(id);
    }

    public void Lose(string id) => _files.Remove(id);
}

internal sealed class RecordingSystemLog : ISystemLog
{
    public List<string> Lines { get; } = [];

    public void Info(string message, int? activityId = null) => Lines.Add(message);

    public void Debug(string message, int? activityId = null) => Lines.Add(message);

    public void Warning(string message, int? activityId = null, Exception? error = null) => Lines.Add(message);

    public void Error(string message, int? activityId = null, Exception? error = null) => Lines.Add(message);
}

internal static class SentenceCacheTestData
{
    public const int SampleRate = 1000;

    public static SentenceCacheKey Key(
        string text = "Hello there, world.", string voice = "F1", string language = "en",
        VoiceEngineKind engine = VoiceEngineKind.Supertonic, string model = "/packs/supertonic-3",
        string packVersion = "1.0.0") =>
        new(text, engine, language, voice, model, packVersion);

    /// <summary>A valid WAV of <paramref name="samples"/> samples at <see cref="SampleRate"/>.</summary>
    public static byte[] Wav(int samples = 100, float level = 0.5f) =>
        WavFile.Encode(Enumerable.Repeat(level, samples).ToArray(), SampleRate);
}
