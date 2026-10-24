using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// One-time clean-up for Slice 99: the translate source choice has no UI any more, so a stored one must
/// not steer the voice. Cleared once, so Slice 101 can bring the setting back without it being wiped on every start.
/// </summary>
public static class TranslateSourceMigration
{
    private const string ClearedKey = "Slice99.TranslateSourceCleared";

    public static void RunOnce(ISettingsStore settings)
    {
        if (Preferences.Default.Get(ClearedKey, false))
        {
            return;
        }

        settings.TranslateSourceLanguage = null;
        Preferences.Default.Set(ClearedKey, true);
    }
}
