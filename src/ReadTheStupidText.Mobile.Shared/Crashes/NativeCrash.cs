namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>A crash the operating system described after the fact (Slice 227): an Android exit reason, an iOS MetricKit diagnostic.</summary>
/// <param name="Kind">A <c>CrashKind</c> value.</param>
/// <param name="Type">The signal, exception type or reason name.</param>
/// <param name="Message">The system's description (file paths are scrubbed when the report is built).</param>
/// <param name="Stack">The call stack or trace, capped when the report is built.</param>
/// <param name="Time">When it happened (iOS MetricKit: the end of the period the payload covers, not the crash itself).</param>
/// <param name="PeriodStart">iOS MetricKit only: the start of that period, so a crash already reported by the app can be matched to the diagnostic; null elsewhere.</param>
internal sealed record NativeCrash(string Kind, string Type, string Message, string Stack, DateTimeOffset Time, DateTimeOffset? PeriodStart = null);
