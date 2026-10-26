namespace ReadTheStupidText.Mobile.Shared;
// Namespace must match the linked Mobile.Shared files (e.g. VoicePackInstaller.cs) so MainThread resolves.

/// <summary>Test stand-in for MAUI's MainThread, which the linked VoicePackInstaller.cs refers to.</summary>
internal static class MainThread
{
    public static bool IsMainThread => true;

    public static void BeginInvokeOnMainThread(Action action) => action();
}
