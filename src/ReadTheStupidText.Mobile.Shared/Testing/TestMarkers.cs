#if DEBUG
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Turns the reader's and the voice pack installer's events into <see cref="TestHookLog"/> lines:
/// READ_START when playback begins, CHUNK n for every spoken chunk, READ_END when it goes idle, PACK_READY
/// when a voice pack finishes installing (Slice 162).
/// </summary>
public sealed class TestMarkers
{
	private bool _reading;
	private int _chunks;

	public TestMarkers(ISpeechReader reader, VoicePackInstaller installer)
	{
		reader.StateChanged += (_, state) => OnState(state);
		reader.ChunkChanged += (_, _) => OnChunk();
		installer.Changed += (_, _) => OnInstaller(installer.State);
	}

	private void OnState(PlaybackState state)
	{
		if (state == PlaybackState.Playing && !_reading)
		{
			_reading = true;
			_chunks = 0;
			TestHookLog.Write(TestHookLog.ReadStart);
		}
		else if (state == PlaybackState.Idle && _reading)
		{
			_reading = false;
			TestHookLog.Write(TestHookLog.ReadEnd);
		}
	}

	private void OnChunk() => TestHookLog.Write($"{TestHookLog.ChunkPrefix} {++_chunks}");

	private static void OnInstaller(PackInstallState state)
	{
		if (state == PackInstallState.Ready)
		{
			TestHookLog.Write(TestHookLog.PackReady);
		}
	}
}
#endif
