using System.Security.Cryptography;
using System.Text;
using Sirous.Reading.Application.Crashes;
using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared.Crashes;

/// <summary>
/// The reference code of one "Report a problem" report (Slice 249): 8 random letters and digits with no look-alikes (no 0, O, 1, I, L), shown as <c>K7QX-2MRD</c>.
/// It names that one report and nothing else (not the install, not the person) and is made fresh for each report. The report carries it as the message
/// <c>user report K7QX2MRD</c> (<see cref="MessageFor"/>), which the Worker checks with the same alphabet. Pure, so the format is tested.
/// </summary>
public static class ReportCode
{
    public const int Length = 8;
    public const int GroupLength = 4;
    public const string MessagePrefix = "user report ";
    public const string UserReportType = "user_report";

    /// <summary>Capital letters and digits without 0, O, 1, I and L. The Worker's pattern must match.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>A fresh code from the system's random generator.</summary>
    public static string Generate() => Generate(RandomNumberGenerator.GetInt32);

    /// <summary>A code using <paramref name="pick"/> (exclusive upper bound in, index out), so a test can fix it.</summary>
    public static string Generate(Func<int, int> pick)
    {
        var code = new StringBuilder(Length);
        for (int i = 0; i < Length; i++)
        {
            code.Append(Alphabet[pick(Alphabet.Length)]);
        }

        return code.ToString();
    }

    /// <summary>The code as shown: two groups of four joined by a dash. Anything that is not a valid code is returned as it came.</summary>
    public static string Display(string code) => IsValid(code) ? $"{code[..GroupLength]}-{code[GroupLength..]}" : code;

    /// <summary>The code as the report carries it: capitals, no dash or space ("k7qx-2mrd" becomes "K7QX2MRD"). Not validated.</summary>
    public static string Normalize(string text) =>
        new(text.Where(c => c is not ('-' or ' ')).Select(char.ToUpperInvariant).ToArray());

    public static bool IsValid(string code) => code.Length == Length && code.All(c => Alphabet.Contains(c));

    /// <summary>The message a report with <paramref name="code"/> carries.</summary>
    public static string MessageFor(string code) => MessagePrefix + code;

    /// <summary>The code in a user report's message, or null when the message is not <c>user report &lt;valid code&gt;</c>.</summary>
    public static string? FromMessage(string message) =>
        message.StartsWith(MessagePrefix, StringComparison.Ordinal) && IsValid(message[MessagePrefix.Length..]) ? message[MessagePrefix.Length..] : null;
}

/// <summary>Builds the report of one "Report a problem" press (Slice 249). Pure.</summary>
public static class UserReportBuilder
{
    /// <summary>
    /// The user report for <paramref name="code"/>: the build facts and the log tail like a crash report, no exception and no stack. Null when the install channel sends
    /// nothing (a dev install). The code must be a valid <see cref="ReportCode"/>.
    /// </summary>
    public static CrashReport? Build(CrashContext context, string code, string logTail, DateTimeOffset now)
    {
        string? channel = InstallChannels.NameOf(context.Channel);
        if (channel is null || !ReportCode.IsValid(code))
        {
            return null;
        }

        return CrashReportJson.Capped(new CrashReport(
            context.App, context.Platform, context.Version, channel, CrashKind.UserReport,
            ReportCode.UserReportType, ReportCode.MessageFor(code), string.Empty, logTail, CrashReportJson.MinuteOf(now), context.Device));
    }
}
