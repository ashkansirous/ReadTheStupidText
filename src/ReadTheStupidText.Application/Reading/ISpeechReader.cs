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
    /// utterance, when playback begins. May be raised off the UI thread: handlers
    /// must marshal to it themselves.</summary>
    event EventHandler<ReadChunk>? ChunkChanged;

    /// <summary>Raised with true when a read that had started has to wait for its next chunk of audio (the sentence is still being
    /// synthesized, or a second model is loading for a word) and with false when that wait ends (Slice 148). Raised on any thread. A reader
    /// that cannot know (the phone's own voice, which buffers inside the OS) never raises it, which is the default.</summary>
    event EventHandler<bool>? StallChanged
    {
        add { }
        remove { }
    }

    /// <summary>Raised on any thread when a later sentence of the playing chunk starts (Slice 190), for the phone's one-sentence highlight.
    /// A reader that does not know its sentence starts (Windows, the phone's own voice) never raises it, which is the default.</summary>
    event EventHandler<SentenceChange>? SentenceChanged
    {
        add { }
        remove { }
    }

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
    /// Reads a story as consecutive segments, each in its own voice (Slice 60). Each
    /// segment is chunked separately and every chunk carries its voice into synthesis;
    /// <see cref="ChunkChanged"/> reports each chunk's range in the whole story. Same
    /// supersede/stop rules as <see cref="SpeakAsync"/>. A voice picked with
    /// <see cref="SetVoice"/> mid-read does not affect a segment read. Engines that cannot
    /// voice a segment individually (<see cref="SupportsSegments"/> false, the default) return
    /// a task faulted with <see cref="NotSupportedException"/>.
    /// </summary>
    Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        Task.FromException(new NotSupportedException("This speech reader cannot read segments in separate voices."));

    /// <summary>Whether <see cref="SpeakSegmentsAsync"/> reads each segment in its own voice.
    /// False by default; callers check it to fall back to a single-voice read.</summary>
    bool SupportsSegments => false;

    /// <summary>Whether this reader can read text that arrives in blocks (<see cref="SpeakStreamAsync"/>). False by default.</summary>
    bool SupportsStreaming => false;

    /// <summary>
    /// Starts a read like <see cref="SpeakAsync"/>, but one that can grow (Slice 75): more text arrives later
    /// through <see cref="AppendText"/>, and the read ends only after <see cref="CompleteStream"/> and the
    /// last chunk has played. It is how a translated read starts speaking its first sentences while the rest
    /// is still being translated. Same supersede/stop rules as <see cref="SpeakAsync"/>. Readers that cannot
    /// stream (<see cref="SupportsStreaming"/> false) return a faulted task.
    /// </summary>
    Task SpeakStreamAsync(string firstText, int? activityId = null) =>
        Task.FromException(new NotSupportedException("This speech reader cannot read text that arrives in blocks."));

    /// <summary>Adds the next block to the read started by <see cref="SpeakStreamAsync"/>; a no-op when none is open. The activity id names the read, so a late call for a superseded one is ignored.</summary>
    void AppendText(string text, int? activityId = null)
    {
    }

    /// <summary>Says no more text will be appended, so the read can finish after the last chunk.</summary>
    void CompleteStream(int? activityId = null)
    {
    }

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
