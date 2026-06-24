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
    private const string EnabledKey = "IsEnabled";
    private const string VoiceKey = "VoiceId";
    private const bool EnabledDefault = true;

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public PlaybackRate Speed
    {
        get => _settings.Values[SpeedKey] is double rate
            ? new PlaybackRate(rate)
            : PlaybackRate.Default;
        set => _settings.Values[SpeedKey] = value.Value;
    }

    public bool IsEnabled
    {
        get => _settings.Values[EnabledKey] is bool enabled ? enabled : EnabledDefault;
        set => _settings.Values[EnabledKey] = value;
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
