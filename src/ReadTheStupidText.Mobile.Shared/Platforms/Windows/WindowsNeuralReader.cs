using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The Windows neural engine for the MAUI apps (Slice 67): Infrastructure's sherpa-onnx
/// <see cref="SupertonicSpeechReader"/> (the one Read The Stupid Text reads with, including
/// segment reads in a voice per character) behind <see cref="ISpeechReader"/>. The only thing it
/// adds is turning Infrastructure's "no engine" exception into Mobile.Shared's, which is the one
/// <see cref="CompositeSpeechReader"/> catches to fall back to the Windows voice.
/// </summary>
public sealed class WindowsNeuralReader : ISpeechReader, IModelScopedSegmentReader, IDisposable
{
	private readonly SupertonicSpeechReader _inner;

	public WindowsNeuralReader(SupertonicSpeechReader inner)
	{
		_inner = inner;
		_inner.StateChanged += (_, s) => StateChanged?.Invoke(this, s);
		_inner.Completed += (_, e) => Completed?.Invoke(this, e);
		_inner.ProgressChanged += (_, p) => ProgressChanged?.Invoke(this, p);
		_inner.TimingChanged += (_, t) => TimingChanged?.Invoke(this, t);
		_inner.ChunkChanged += (_, c) => ChunkChanged?.Invoke(this, c);
		_inner.StallChanged += (_, stalled) => StallChanged?.Invoke(this, stalled);
	}

	public event EventHandler<PlaybackState>? StateChanged;
	public event EventHandler? Completed;
	public event EventHandler<double>? ProgressChanged;
	public event EventHandler<ReadTiming>? TimingChanged;
	public event EventHandler<ReadChunk>? ChunkChanged;
	public event EventHandler<bool>? StallChanged;

	public PlaybackState State => _inner.State;

	public bool SupportsSegments => _inner.SupportsSegments;

	public Task SpeakAsync(string text, int? activityId = null) =>
		Translate(() => _inner.SpeakAsync(text, activityId));

	public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
		Translate(() => _inner.SpeakSegmentsAsync(segments, activityId));

	// Slice 173: voiced segments in the pack the caller names, the app-language model untouched.
	public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, VoiceModelPaths model, int? activityId = null) =>
		Translate(() => _inner.SpeakSegmentsAsync(segments, model, activityId));

	public Task WarmUpAsync() => _inner.WarmUpAsync();

	public Task WarmUpAsync(VoiceModelPaths model) => _inner.WarmUpAsync(model);

	public void Pause() => _inner.Pause();

	public void Resume() => _inner.Resume();

	public void Stop() => _inner.Stop();

	public void SetSpeed(PlaybackRate speed) => _inner.SetSpeed(speed);

	public void SetVoice(string voiceId) => _inner.SetVoice(voiceId);

	public Task SkipForward() => _inner.SkipForward();

	public Task SkipBackward() => _inner.SkipBackward();

	public void Dispose() => _inner.Dispose();

	private static async Task Translate(Func<Task> read)
	{
		try
		{
			await read().ConfigureAwait(false);
		}
		catch (ReadTheStupidText.Infrastructure.Reading.NeuralEngineUnavailableException)
		{
			throw new ReadTheStupidText.Mobile.Shared.NeuralEngineUnavailableException();
		}
	}
}
