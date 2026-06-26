using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Settings;

/// <summary>
/// Stores preferences in <see cref="ApplicationData.LocalSettings"/>. The speed
/// is persisted as its decimal multiplier; a missing value falls back to the
/// default, and any stored value is re-normalized by <see cref="PlaybackRate"/>.
/// </summary>
public sealed class LocalSettingsStore : ISettingsStore
{
    private const string SpeedKey = "PlaybackRate";

    // The legacy single auto-read flag, kept only to migrate existing profiles:
    // an old IsEnabled=false maps both new toggles off (see AutoRead getters).
    private const string LegacyEnabledKey = "IsEnabled";
    private const string AutoReadOnSelectionKey = "AutoReadOnSelection";
    private const string AutoReadOnCopyKey = "AutoReadOnCopy";
    private const string VoiceKey = "VoiceId";

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public PlaybackRate Speed
    {
        get => _settings.Values[SpeedKey] is double rate
            ? new PlaybackRate(rate)
            : PlaybackRate.Default;
        set => _settings.Values[SpeedKey] = value.Value;
    }

    public bool AutoReadOnSelection
    {
        get => AutoReadFlag(AutoReadOnSelectionKey);
        set => _settings.Values[AutoReadOnSelectionKey] = value;
    }

    public bool AutoReadOnCopy
    {
        get => AutoReadFlag(AutoReadOnCopyKey);
        set => _settings.Values[AutoReadOnCopyKey] = value;
    }

    // Resolves an auto-read toggle: the saved value if present, else the legacy
    // single flag (so an upgraded IsEnabled=false carries to both), else on.
    private bool AutoReadFlag(string key)
    {
        if (_settings.Values[key] is bool saved)
        {
            return saved;
        }

        return _settings.Values[LegacyEnabledKey] is bool legacy ? legacy : true;
    }

    public string? VoiceId
    {
        get => _settings.Values[VoiceKey] as string;
        set
        {
            if (value is null)
            {
                _settings.Values.Remove(VoiceKey);
            }
            else
            {
                _settings.Values[VoiceKey] = value;
            }
        }
    }
}
