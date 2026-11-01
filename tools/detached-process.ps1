# Slice 241 item 5: starts a long background child that holds none of the caller's handles. Dot-sourced, not run.
# Start-Process with -RedirectStandardOutput creates the child with handle inheritance on, so the child (and its dotnet/msbuild descendants) keeps the
# caller's own stdout/stderr pipe open: a caller piped through `| tail` or a reader waits until the LAST holder exits (2026-11-01: ~8 min after run-checks had finished).
# ShellExecute-style start (UseShellExecute) passes no handles; the child's output goes to files through a cmd.exe redirect instead.
function ConvertTo-CmdLine([string]$FilePath, [string]$Arguments, [string]$StdoutPath, [string]$StderrPath) {
    # cmd /s /c "<line>" strips only the outer quotes, so inner quotes survive.
    $line = "`"$FilePath`" $Arguments >`"$StdoutPath`" 2>`"$StderrPath`""
    return "/d /s /c `"$line`""
}

# Returns the System.Diagnostics.Process of the cmd.exe wrapper (its HasExited / ExitCode are the child's). $Arguments is one ready-quoted string.
function Start-DetachedProcess([string]$FilePath, [string]$Arguments, [string]$StdoutPath, [string]$StderrPath) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = if ($env:ComSpec) { $env:ComSpec } else { 'cmd.exe' }
    $psi.Arguments = ConvertTo-CmdLine $FilePath $Arguments $StdoutPath $StderrPath
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    return [System.Diagnostics.Process]::Start($psi)
}
