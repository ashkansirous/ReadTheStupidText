using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Infrastructure.Sanitizing;

/// <summary>
/// Applies the enabled <see cref="SanitizationRules"/> to intercepted text. The
/// set of enabled categories is read live from <see cref="ISettingsStore"/> on
/// every call, so toggling a category takes effect on the next read without a
/// restart. Pure aside from that settings read — all rewriting is regex-based.
/// </summary>
public sealed class TextSanitizer : ITextSanitizer
{
    private readonly ISettingsStore _settings;

    public TextSanitizer(ISettingsStore settings) => _settings = settings;

    public string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        SanitizerCategory enabled = _settings.EnabledSanitizers;
        string result = text;
        foreach (SanitizationRule rule in SanitizationRules.All)
        {
            if (enabled.HasFlag(rule.Category))
            {
                result = rule.Apply(result);
            }
        }

        return SanitizationRules.CollapseSpaces(result);
    }
}
