#if DEBUG
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Turns the reader's and the voice pack installer's events into <see cref="TestHookLog"/> lines:
/// ENGINE_READY / CHUNK_SYNTH when the engine is loaded and each chunk's audio is generated (Slice 175), READ_START when playback begins, CHUNK n for every spoken chunk (counted per read; the first can come just before READ_START), READ_END when it goes idle, PACK_READY
/// when a voice pack finishes installing (Slice 162).
/// </summary>
public sealed class TestMarkers
{
	private bool _reading;
	private int _chunks;

	public TestMarkers(ISpeechReader reader, VoicePackInstaller installer)
	{
		PackDownloadLog.Sink = TestHookLog.Write; // download diagnostics reach logcat in Debug builds
		reader.StateChanged += (_, state) => OnState(state);
		reader.ChunkChanged += (_, _) => OnChunk();
		installer.Changed += (_, _) => OnInstaller(installer.State);
		ReadTimingTracker.ChunkAudioRecorded += OnChunkAudio;
		ReadTimingTracker.ReadStarted += OnReadStarted;
	}

	// The reader tracks a read right after its engine is loaded, so this is "engine ready", and the first chunk's synthesis begins now (Slice 175).
	// Later chunks are generated in parallel, so only their end is observable here; chunk 0's end minus the ENGINE_READY moment is the cold synthesis time.
	private static void OnReadStarted(int chunkCount)
	{
		TestHookLog.WriteEngineReady();
		TestHookLog.WriteChunkSynth(0, TestHookLog.SynthStart);
	}

	// The first chunk's audio marks a new read, so the numbering restarts even when the previous read never went Idle (a second read while playing).
	private void OnChunkAudio(int index, TimeSpan duration)
	{
		if (index == 0)
		{
			Interlocked.Exchange(ref _chunks, 0);
		}

		TestHookLog.WriteChunkSynth(index, TestHookLog.SynthEnd);
		TestHookLog.Write($"{TestHookLog.ChunkAudioPrefix} {index} {duration.TotalMilliseconds:0}ms");
	}

	private void OnState(PlaybackState state)
	{
		if (state == PlaybackState.Playing && !_reading)
		{
			_reading = true;
			TestHookLog.Write(TestHookLog.ReadStart);
		}
		else if (state == PlaybackState.Idle && _reading)
		{
			_reading = false;
			Interlocked.Exchange(ref _chunks, 0);
			TestHookLog.Write(TestHookLog.ReadEnd);
		}
	}

	private void OnChunk() => TestHookLog.Write($"{TestHookLog.ChunkPrefix} {Interlocked.Increment(ref _chunks)}");

	private static void OnInstaller(PackInstallState state)
	{
		if (state == PackInstallState.Ready)
		{
			TestHookLog.Write(TestHookLog.PackReady);
		}
	}
}
#endif
