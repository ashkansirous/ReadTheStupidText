namespace ReadTheStupidText.Mobile.Shared;

/// <summary>Where the voice-cancel hold is saved (Preferences on the phone, memory in tests).</summary>
public interface IVoiceCancelStore
{
    string? Read();

    void Write(string? language);
}

/// <summary>
/// Slice 186: the user cancelled the voice pack download for one language, so no other pack (the scan pack)
/// starts by itself for that language. Saved, so a restart does not start it either. Set only by a user
/// cancel; cleared when the voice pack is Ready or the language changes. Owned by <see cref="VoicePackInstaller"/>.
/// </summary>
public sealed class VoiceCancelHold(IVoiceCancelStore store)
{
    public bool IsHeld(string language) => string.Equals(store.Read(), language, StringComparison.OrdinalIgnoreCase);

    public void Hold(string language) => store.Write(language);

    public void Release() => store.Write(null);

    /// <summary>A language change lifts a hold kept for any other language.</summary>
    public void ReleaseUnlessFor(string language)
    {
        if (!IsHeld(language))
        {
            Release();
        }
    }
}

/// <summary>Keeps the hold in memory only (tests).</summary>
public sealed class MemoryVoiceCancelStore : IVoiceCancelStore
{
    private string? _language;

    public string? Read() => Volatile.Read(ref _language);

    public void Write(string? language) => Volatile.Write(ref _language, language);
}

/// <summary>Keeps the hold in the app's Preferences so it survives a restart.</summary>
public sealed class PreferencesVoiceCancelStore : IVoiceCancelStore
{
    private const string Key = "VoiceCancelHeldLanguage";

    public string? Read() => Preferences.Default.Get(Key, (string?)null);

    public void Write(string? language)
    {
        if (language is null)
        {
            Preferences.Default.Remove(Key);
            return;
        }

        Preferences.Default.Set(Key, language);
    }
}
