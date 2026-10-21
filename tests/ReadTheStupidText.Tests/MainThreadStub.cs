namespace ReadTheStupidText.Mobile;

/// <summary>Test stand-in for MAUI's MainThread, which the linked VoicePackInstaller.cs refers to.</summary>
internal static class MainThread
{
    public static bool IsMainThread => true;

    public static void BeginInvokeOnMainThread(Action action) => action();
}
