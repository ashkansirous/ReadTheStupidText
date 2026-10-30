namespace ReadTheStupidText.Mobile.Shared;
// Namespace must match the linked Mobile.Shared files (e.g. VoicePackInstaller.cs) so MainThread resolves.

/// <summary>Test stand-in for MAUI's MainThread, which the linked VoicePackInstaller.cs refers to.</summary>
internal static class MainThread
{
    public static bool IsMainThread => true;

    public static void BeginInvokeOnMainThread(Action action) => action();
}

/// <summary>Test stand-in for MAUI's Preferences, which the linked VoiceCancelHold.cs refers to (in memory).</summary>
internal static class Preferences
{
    public static PreferencesStub Default { get; } = new();
}

internal sealed class PreferencesStub
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _values = new();

    public T? Get<T>(string key, T? defaultValue) => _values.TryGetValue(key, out object? v) ? (T)v : defaultValue;

    public void Set<T>(string key, T value) => _values[key] = value!;

    public void Remove(string key) => _values.TryRemove(key, out _);
}
