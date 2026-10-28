#if DEBUG
namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// The marker lines the flows and scripts assert on (Slice 162): logcat tag <c>RTST-TEST</c> on Android
/// (<c>adb logcat -s RTST-TEST</c>), the same text on the iOS console. One line per state change; never the
/// user's text.
/// </summary>
public static partial class TestHookLog
{
	public const string Tag = "RTST-TEST";
	public const string ReadStart = "READ_START";
	public const string ReadEnd = "READ_END";
	public const string ChunkPrefix = "CHUNK";
	public const string ChunkAudioPrefix = "CHUNK_AUDIO";
	public const string PackReady = "PACK_READY";
	public const string HookPrefix = "HOOK";
	public const string PagePrefix = "PAGE";
	public const string EngineReadyPrefix = "ENGINE_READY";
	public const string ChunkSynthPrefix = "CHUNK_SYNTH";
	public const string SynthStart = "start";
	public const string SynthEnd = "end";
	private const char LineFeed = (char)10;
	private const string FrameSeparator = " in ";
	private const string NoFrame = "no stack";
	private static long _readRequestedTicks;
	private static long _engineReadyTicks;

	public static void Write(string marker) => WritePlatform(marker);

	/// <summary>Notes that a hook asked for a read now (Slice 175); <c>ENGINE_READY</c> counts from here. A read the hook did not start has no such moment.</summary>
	public static void NoteReadRequested()
	{
		Interlocked.Exchange(ref _readRequestedTicks, System.Diagnostics.Stopwatch.GetTimestamp());
		Interlocked.Exchange(ref _engineReadyTicks, 0);
	}

	/// <summary>Writes <c>ENGINE_READY &lt;ms&gt;</c>: milliseconds from the read request to the engine being built and loaded (-1 when no hook asked for this read).</summary>
	public static void WriteEngineReady()
	{
		// Once per requested read: a Dino page read starts many readers (one per voice or sentence), and only the first marks the engine ready.
		if (Interlocked.Read(ref _engineReadyTicks) != 0)
		{
			return;
		}

		long now = System.Diagnostics.Stopwatch.GetTimestamp();
		long requested = Interlocked.Read(ref _readRequestedTicks);
		Interlocked.Exchange(ref _engineReadyTicks, now);
		long ms = requested == 0 ? -1 : (long)System.Diagnostics.Stopwatch.GetElapsedTime(requested, now).TotalMilliseconds;
		Write($"{EngineReadyPrefix} {ms}");
	}

	/// <summary>Writes <c>CHUNK_SYNTH n start|end &lt;ms&gt;</c>, with <c>ms</c> counted from the engine being ready (-1 before it).</summary>
	public static void WriteChunkSynth(int index, string edge)
	{
		long ready = Interlocked.Read(ref _engineReadyTicks);
		long ms = ready == 0 ? -1 : (long)System.Diagnostics.Stopwatch.GetElapsedTime(ready).TotalMilliseconds;
		Write($"{ChunkSynthPrefix} {index} {edge} {ms}");
	}

	/// <summary>The exception's type and its top stack frame (method only, no file path and never the message, which can hold the user's text), so a fault is findable from the log.</summary>
	public static string FaultSummary(Exception ex)
	{
		string? top = ex.StackTrace?.Split(LineFeed, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
		if (top is null)
		{
			return $"{ex.GetType().Name} {NoFrame}";
		}

		int path = top.IndexOf(FrameSeparator, StringComparison.Ordinal);
		return $"{ex.GetType().Name} {(path < 0 ? top : top[..path])}";
	}

	static partial void WritePlatform(string marker);
}
#endif
