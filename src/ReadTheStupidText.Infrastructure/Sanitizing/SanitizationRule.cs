using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Infrastructure.Sanitizing;

/// <summary>
/// One sanitizer category paired with the pure transform that applies it. Kept
/// free of settings and IO so the rules can be exercised in isolation by the
/// unit tests; <see cref="TextSanitizer"/> runs the enabled ones in order.
/// </summary>
public sealed record SanitizationRule(SanitizerCategory Category, Func<string, string> Apply);
