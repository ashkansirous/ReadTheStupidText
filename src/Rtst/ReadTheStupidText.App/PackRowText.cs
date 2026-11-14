using Sirous.Reading.Application.Net;
using Sirous.Reading.Application.Reading;

namespace ReadTheStupidText_App;

/// <summary>What the voice pack run is doing right now, as the language window sees it (the installer's state, or "just started").</summary>
internal readonly record struct RunView(PackSetupState State, double Progress, HttpErrorKind? Failure, bool CanRetry);

/// <summary>The second line, the error line (empty = none) and whether Retry shows at the row's end.</summary>
internal sealed record RowText(string Detail, string Error, bool ShowRetry);

/// <summary>The window's two run flags: a run was just started and has not published yet; an exception escaped the installer.</summary>
internal readonly record struct RunFlags(bool Starting, bool EscapedFailure);

/// <summary>Which state the run row shows: the installer's, "just started" (Resolving), or the general failure of an escaped exception.</summary>
internal enum RunShown { Installer, Resolving, EscapedFailure }

/// <summary>Slice 373: the "which state to show" decision, pure so the tests link it.</summary>
internal static class RunStateRule
{
    // Only Resolving, Downloading or Ready end a start and clear an escaped failure; a Failed or Idle publish
    // (a stale one from the previous run) changes nothing.
    public static RunFlags OnPublish(RunFlags flags, PackSetupState state) =>
        state is PackSetupState.Resolving or PackSetupState.Downloading or PackSetupState.Ready ? new RunFlags(false, false) : flags;

    public static RunShown Decide(RunFlags flags) =>
        flags.Starting ? RunShown.Resolving : flags.EscapedFailure ? RunShown.EscapedFailure : RunShown.Installer;
}

/// <summary>
/// Slice 373 (Missing States mock RT2): the one place that decides what a language row shows. Pure (the words come in as functions), so the tests link this file.
/// A row that is not the run row shows only "not downloaded" when its pack is known not installed; the run row shows the run's state.
/// </summary>
internal static class PackRowText
{
    public static RowText Compose(bool isRunRow, RunView run, bool? installed, string notDownloaded, Func<RunView, string> stateLine, Func<RunView, string> errorLine)
    {
        string idle = installed == false ? notDownloaded : string.Empty;
        if (!isRunRow)
        {
            return new RowText(idle, string.Empty, false);
        }

        return run.State switch
        {
            PackSetupState.Failed or PackSetupState.HashMismatch or PackSetupState.InvalidPack => new RowText(idle, errorLine(run), run.CanRetry),
            PackSetupState.Resolving or PackSetupState.Downloading or PackSetupState.Ready or PackSetupState.Offline or PackSetupState.Unavailable =>
                new RowText(stateLine(run), string.Empty, run.CanRetry),
            _ => new RowText(idle, string.Empty, false),
        };
    }
}
