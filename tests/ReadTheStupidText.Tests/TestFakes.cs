using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Tests;

/// <summary>Shared test doubles for the settings store and system log.</summary>
internal sealed class SilentLog : ISystemLog
{
    public void Info(string message, int? activityId = null) { }
    public void Debug(string message, int? activityId = null) { }
    public void Warning(string message, int? activityId = null, Exception? error = null) { }
    public void Error(string message, int? activityId = null, Exception? error = null) { }
}

internal sealed class TestSettings : ISettingsStore
{
    public PlaybackRate Speed { get; set; } = PlaybackRate.Default;
    public bool AutoReadOnSelection { get; set; }
    public bool AutoReadOnCopy { get; set; }
    public string? VoiceId { get; set; }
    public SanitizerCategory EnabledSanitizers { get; set; }
    public PanelPosition? PanelPosition { get; set; }
    public string? Language { get; set; }
    public bool TranslateEnabled { get; set; }
    public string? TranslateSourceLanguage { get; set; }
}
