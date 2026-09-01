using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Stores preferences via MAUI's <see cref="Preferences"/> API (Decision 41) —
/// the mobile analogue of Windows' <c>ApplicationData.Current.LocalSettings</c>.
/// Same interface, same keys where they apply (voice id, playback rate); the
/// Windows-only settings (auto-read gates, panel position) are still backed by
/// Preferences for a uniform implementation, but nothing on Android ever reads
/// them (Decision 38 — no auto-read triggers, no panel to reposition).
/// </summary>
public sealed class MobilePreferencesSettingsStore : ISettingsStore
{
    private const string SpeedKey = "PlaybackRate";
    private const string AutoReadOnSelectionKey = "AutoReadOnSelection";
    private const string AutoReadOnCopyKey = "AutoReadOnCopy";
    private const string VoiceKey = "VoiceId";
    private const string SanitizerKey = "EnabledSanitizers";
    private const string PanelXKey = "PanelX";
    private const string PanelYKey = "PanelY";

    private const bool AutoReadDefault = true;
    private const double NoSpeedStored = -1;
    private const int NoPanelCoordinateStored = int.MinValue;

    public PlaybackRate Speed
    {
        get
        {
            double stored = Preferences.Default.Get(SpeedKey, NoSpeedStored);
            return stored == NoSpeedStored ? PlaybackRate.Default : new PlaybackRate(stored);
        }
        set => Preferences.Default.Set(SpeedKey, value.Value);
    }

    public bool AutoReadOnSelection
    {
        get => Preferences.Default.Get(AutoReadOnSelectionKey, AutoReadDefault);
        set => Preferences.Default.Set(AutoReadOnSelectionKey, value);
    }

    public bool AutoReadOnCopy
    {
        get => Preferences.Default.Get(AutoReadOnCopyKey, AutoReadDefault);
        set => Preferences.Default.Set(AutoReadOnCopyKey, value);
    }

    public string? VoiceId
    {
        get => Preferences.Default.Get(VoiceKey, (string?)null);
        set
        {
            if (value is null)
            {
                Preferences.Default.Remove(VoiceKey);
            }
            else
            {
                Preferences.Default.Set(VoiceKey, value);
            }
        }
    }

    public SanitizerCategory EnabledSanitizers
    {
        get => (SanitizerCategory)Preferences.Default.Get(SanitizerKey, (int)SanitizerCategory.All);
        set => Preferences.Default.Set(SanitizerKey, (int)value);
    }

    public PanelPosition? PanelPosition
    {
        get
        {
            int x = Preferences.Default.Get(PanelXKey, NoPanelCoordinateStored);
            int y = Preferences.Default.Get(PanelYKey, NoPanelCoordinateStored);
            return x != NoPanelCoordinateStored && y != NoPanelCoordinateStored
                ? new PanelPosition(x, y)
                : null;
        }
        set
        {
            if (value is { } position)
            {
                Preferences.Default.Set(PanelXKey, position.X);
                Preferences.Default.Set(PanelYKey, position.Y);
            }
            else
            {
                Preferences.Default.Remove(PanelXKey);
                Preferences.Default.Remove(PanelYKey);
            }
        }
    }
}
