using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Speaks text aloud and exposes live playback control. The implementation
/// (Infrastructure) owns the synthesis and audio engine; the Application and
/// UI layers only ever see this contract.
/// </summary>
public interface ISpeechReader
{
    /// <summary>Raised whenever <see cref="State"/> changes.</summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>Raised once when an utterance finishes playing to its natural end
    /// (not when it is stopped/superseded), so the caller can mark it complete.</summary>
    event EventHandler? Completed;

    /// <summary>Raised as playback advances with the read-through fraction (0..1) of
    /// the current utterance. Best-effort for chunked synthesis (each chunk weighted
    /// equally); resets toward 0 when a new read starts or playback stops.</summary>
    event EventHandler<double>? ProgressChanged;

    /// <summary>Raised roughly once per second while reading, and immediately whenever
    /// <see cref="ReadTiming.Total"/> changes, with the read-through elapsed/total of
    /// the current utterance (Decision 33). <see cref="ReadTiming.Total"/> is null
    /// until every chunk has finished synthesizing.</summary>
    event EventHandler<ReadTiming>? TimingChanged;

    /// <summary>Raised whenever a new chunk begins playing (Decision 46/50) — the
    /// reading text box's whole-chunk highlight source. An engine that doesn't
    /// chunk (the WinRT fallback) raises it once per read, for the whole
    /// utterance, when playback begins.</summary>
    event EventHandler<ReadChunk>? ChunkChanged;

    PlaybackState State { get; }

    /// <summary>
    /// Synthesizes <paramref name="text"/> and begins playback. Starting a new
    /// utterance cancels any in-flight synthesis/playback of a previous one, so a
    /// superseded (e.g. long) read can never play after the one that replaced it.
    /// The optional <paramref name="activityId"/> is the activity-log entry id this
    /// read belongs to, stamped onto the engine's per-chunk timing log lines so they
    /// join back to the same row (latency diagnostics).
    /// </summary>
    Task SpeakAsync(string text, int? activityId = null);

    /// <summary>
    /// Eagerly prepares the synthesis engine (building any heavy model and warming
    /// its compute graph) so the first real read does not pay the cold-start cost.
    /// Runs off the UI thread, is idempotent, and is a no-op for engines with
    /// nothing to preload. A read arriving before warm-up finishes still works —
    /// it falls through to the same lazy build.
    /// </summary>
    Task WarmUpAsync();

    void Pause();

    void Resume();

    /// <summary>Stops playback immediately, cancels any in-flight synthesis, and
    /// returns to idle. Unlike <see cref="Pause"/>, the utterance cannot resume.</summary>
    void Stop();

    /// <summary>Applies a new speed live, without restarting playback.</summary>
    void SetSpeed(PlaybackRate speed);

    /// <summary>
    /// Selects the narrator voice (by <see cref="VoiceInfo.Id"/>) for the next
    /// read. A voice cannot be swapped mid-utterance; an unknown id is ignored
    /// (the current voice is kept).
    /// </summary>
    void SetVoice(string voiceId);

    /// <summary>
    /// Jumps playback ~10s forward (Decision 32) — best-effort, not an exact
    /// 10.000s seek: implementations may snap to the nearest reachable boundary
    /// (e.g. a chunk start) instead. Clamped to the furthest point already
    /// synthesized/known; a no-op while idle.
    /// </summary>
    Task SkipForward();

    /// <summary>
    /// Jumps playback ~10s backward (Decision 32) — best-effort, not an exact
    /// 10.000s seek. Clamped to zero; a no-op while idle.
    /// </summary>
    Task SkipBackward();
}
