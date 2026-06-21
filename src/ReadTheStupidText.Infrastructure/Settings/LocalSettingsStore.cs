using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Settings;

/// <summary>
/// Stores preferences in <see cref="ApplicationData.LocalSettings"/>. The speed
/// is persisted by enum name so the stored value survives reordering of the
/// enum; missing or unparseable values fall back to the defaults.
/// </summary>
public sealed class LocalSettingsStore : ISettingsStore
{
    private const string SpeedKey = "ReadingSpeed";
    private const string EnabledKey = "IsEnabled";
    private const bool EnabledDefault = true;

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public ReadingSpeed Speed
    {
        get => _settings.Values[SpeedKey] is string name
               && Enum.TryParse(name, out ReadingSpeed speed)
            ? speed
            : ReadingSpeedExtensions.Default;
        set => _settings.Values[SpeedKey] = value.ToString();
    }

    public bool IsEnabled
    {
        get => _settings.Values[EnabledKey] is bool enabled ? enabled : EnabledDefault;
        set => _settings.Values[EnabledKey] = value;
    }
}
