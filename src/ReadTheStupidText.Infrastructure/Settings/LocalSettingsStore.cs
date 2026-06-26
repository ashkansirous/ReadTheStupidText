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
    private const string AutoReadOnSelectionKey = "AutoReadOnSelection";
    private const string AutoReadOnCopyKey = "AutoReadOnCopy";

    // The pre-Slice-12 single auto-read gate. Read only to migrate older profiles:
    // an existing IsEnabled=false maps both new flags off (see ReadAutoReadFlag).
    private const string LegacyEnabledKey = "IsEnabled";
    private const bool AutoReadDefault = true;

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
        get => ReadAutoReadFlag(AutoReadOnSelectionKey);
        set => _settings.Values[AutoReadOnSelectionKey] = value;
    }

    public bool AutoReadOnCopy
    {
        get => ReadAutoReadFlag(AutoReadOnCopyKey);
        set => _settings.Values[AutoReadOnCopyKey] = value;
    }

    // A new flag takes its own stored value when present; otherwise it inherits
    // the old single toggle (so IsEnabled=false carries forward to both flags off,
    // IsEnabled=true/unset to on), defaulting on for a fresh install.
    private bool ReadAutoReadFlag(string key)
    {
        if (_settings.Values[key] is bool flag)
        {
            return flag;
        }

        return _settings.Values[LegacyEnabledKey] is bool legacy ? legacy : AutoReadDefault;
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
