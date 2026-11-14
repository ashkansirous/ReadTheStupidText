using System.Diagnostics;
using Sirous.Reading.Mobile.Shared.Crashes;

namespace DinoReader;

/// <summary>
/// The store reads' diagnostic lines (Slice 358). They go to the rolling log that a Release (Play-installed) build keeps, through <see cref="AppTrace"/>, so a run can tell a billing
/// error from an empty answer. A line carries only response codes, counts and product ids, never an account, price or purchase token (the log's tail goes out with a crash report).
/// </summary>
public static class PurchaseTrace
{
    private const string Tag = "[Purchases]";

    /// <summary>Writes one line; also to the debugger output in a Debug build.</summary>
    public static void Line(string message)
    {
        Debug.WriteLine($"Dino Reader {message}");
        AppTrace.Line($"{Tag} {message}");
    }
}
