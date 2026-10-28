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

	public static void Write(string marker) => WritePlatform(marker);

	static partial void WritePlatform(string marker);
}
#endif
