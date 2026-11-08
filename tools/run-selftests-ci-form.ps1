<#
.SYNOPSIS
  Runs every single-line `run:` of tools-selftests.yml's `selftests` job the way CI does (Slice 315 item 7), in each declared shell, and reports the exit codes.
  Slice 284's builder ran a self-test in a form CI does not use (`-File`, which hides a stale $LASTEXITCODE) and CI went red.
  CI's `pwsh` / `powershell` shells run a temp script with `pwsh -command ". '{0}'"` / `powershell -command ". '{0}'"`, with `$ErrorActionPreference = 'stop'` put in front and
  `if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }` added at the end (GitHub Actions workflow syntax, "Exit codes and error action preference").
  This script builds the same temp script and starts the same command. Steps this script cannot reproduce (a multi-line `run: |` such as the PSScriptAnalyzer lint, which installs a module; a step with `if:`, `env:`, `shell:` or `working-directory:`)
  are printed as SKIPPED with the reason, never run wrongly and never dropped silently.
.PARAMETER Shell
  pwsh, powershell or both (default: the matrix of the workflow).
.PARAMETER Only
  A substring of the step command or name: run only the matching steps.
.PARAMETER List
  Print what would run and exit 0 (no-op switch: starts nothing).
.PARAMETER LibraryOnly
  Load the functions and return (for the self-test, which dot-sources this script). Not implied by the dot-source form: `pwsh -command ". ./tools/run-selftests-ci-form.ps1"` runs everything.
.PARAMETER Workflow
  Path to the workflow file (default: .github/workflows/tools-selftests.yml of this worktree).
#>
param(
    [ValidateSet('pwsh', 'powershell', 'both')][string]$Shell = 'both',
    [string]$Only = '',
    [switch]$List,
    [switch]$LibraryOnly,
    [string]$Workflow = (Join-Path $PSScriptRoot '..\.github\workflows\tools-selftests.yml')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'emulator\speed.ps1')   # Stop-ProcessTree (taskkill /T /F), the repo's way to end a process and its children
$script:StepTimeoutSeconds = 600

# Why this script cannot reproduce a run step the way CI would, or '' when it can: a step with its own `if:`, `env:`, `shell:` or `working-directory:`, or a multi-line `run: |` block.
# Such a step is listed as SKIPPED with the reason, never run wrongly and never dropped silently.
function Get-StepSkipReason($Keys, [string]$Command) {
    if ($Command -match '^[|>][+-]?$') { return 'multi-line run: block (this script runs one-line commands only)' }
    foreach ($key in @('if', 'env', 'shell', 'working-directory')) {
        if ($Keys -contains $key) { return "the step sets $key`: (this script does not reproduce it)" }
    }
    return ''
}

# The `run:` steps of the job `selftests`: Name, Command and SkipReason ('' = runnable). A step with `uses:` has no run and is not listed.
function Get-CiRunLines([string]$YamlText, [string]$Job = 'selftests') {
    $steps = New-Object Collections.Generic.List[object]
    $inJob = $false
    $cur = @{ Name = ''; Command = $null; Keys = (New-Object Collections.Generic.List[string]) }   # a hashtable: the block below changes its members, not a copy of a variable
    $flush = {
        if ($null -ne $cur.Command) { $steps.Add([pscustomobject]@{ Name = $cur.Name; Command = $cur.Command; SkipReason = (Get-StepSkipReason $cur.Keys.ToArray() $cur.Command) }) }
        $cur.Command = $null; $cur.Name = ''; $cur.Keys.Clear()
    }
    foreach ($line in ($YamlText -split "\r?\n")) {
        if ($line -match '^  (\w[\w-]*):\s*$') { & $flush; $inJob = ($Matches[1] -eq $Job); continue }
        if (-not $inJob) { continue }
        if ($line -match '^      - (\w[\w-]*):\s*(.*?)\s*$') { & $flush; $key = $Matches[1]; $value = $Matches[2] }
        elseif ($line -match '^        (\w[\w-]*):\s*(.*?)\s*$') { $key = $Matches[1]; $value = $Matches[2] }
        else { continue }
        if ($key -eq 'name') { $cur.Name = $value } elseif ($key -eq 'run') { $cur.Command = $value } else { $cur.Keys.Add($key) }
    }
    & $flush
    return $steps.ToArray()
}

# The text of the temp script CI writes for a pwsh/powershell step.
function New-CiScriptText([string]$Command) {
    return "`$ErrorActionPreference = 'stop'`r`n$Command`r`nif ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit `$LASTEXITCODE }`r`n"
}

function Invoke-CiStep([string]$ShellName, [string]$Command, [string]$WorkDir) {
    $scriptFile = Join-Path ([IO.Path]::GetTempPath()) ('ci-form-' + [guid]::NewGuid().ToString('N') + '.ps1')
    try {
        [IO.File]::WriteAllText($scriptFile, (New-CiScriptText $Command), (New-Object Text.UTF8Encoding $true))   # with a BOM: 5.1 reads a BOM-less file as ANSI
        $info = New-Object Diagnostics.ProcessStartInfo $ShellName
        $info.Arguments = "-NoProfile -NonInteractive -command `". '$scriptFile'`""
        $info.WorkingDirectory = $WorkDir
        $info.UseShellExecute = $false
        $proc = [Diagnostics.Process]::Start($info)
        if (-not $proc.WaitForExit($script:StepTimeoutSeconds * 1000)) { Stop-ProcessTree $proc.Id; return 124 }   # Slice 315 item 6: the whole tree (child pwsh, adb, Maestro), not only the shell
        return $proc.ExitCode
    } finally { Remove-Item -LiteralPath $scriptFile -Force -ErrorAction SilentlyContinue }
}

function Get-RequestedShells([string]$Requested) {
    if ($Requested -eq 'both') { return @('pwsh', 'powershell') }
    return @($Requested)
}

# Slice 315 item 4: 0 only when every requested shell ran, no step failed and at least one step executed; a shell that was not installed (NOT RUN) is never a green.
function Get-CiFormExitCode([int]$Failures, [int]$NotRun, [int]$Executed) {
    if ($Failures -gt 0) { return 1 }
    if ($NotRun -gt 0 -or $Executed -eq 0) { return 1 }
    return 0
}

function Start-CiFormRun {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $steps = @(Get-CiRunLines ([IO.File]::ReadAllText($Workflow)) | Where-Object { -not $Only -or $_.Command.Contains($Only) -or $_.Name.Contains($Only) })
    if ($steps.Count -eq 0) { Write-Host 'run-selftests-ci-form: no step matched.'; return 2 }
    $failures = 0
    $skipped = 0
    $notRun = 0
    $executed = 0
    foreach ($shellName in (Get-RequestedShells $Shell)) {
        if (-not $List -and -not (Get-Command $shellName -ErrorAction SilentlyContinue)) { Write-Host "NOT RUN [$shellName]: shell not installed here."; $notRun++; continue }
        foreach ($step in $steps) {
            if ($step.SkipReason) { Write-Host "SKIPPED [$shellName] $($step.Command) ($($step.Name)): $($step.SkipReason)"; $skipped++; continue }
            if ($List) { Write-Host "[$shellName] $($step.Command)   ($($step.Name))"; continue }
            $code = Invoke-CiStep $shellName $step.Command $repoRoot
            $executed++
            Write-Host ("{0,-4} [{1}] {2} (exit {3})" -f $(if ($code -eq 0) { 'PASS' } else { 'FAIL' }), $shellName, $step.Command, $code)
            if ($code -ne 0) { $failures++ }
        }
    }
    Write-Host "run-selftests-ci-form: $failures failing step(s), $skipped skipped, $notRun shell(s) NOT RUN."
    if ($List) { return 0 }
    return (Get-CiFormExitCode $failures $notRun $executed)
}

# Review finding 7: CI's own shell dot-sources a script too (`-command ". '<script>'"`), so the dot-source form cannot mean "functions only"; the self-test asks for that with -LibraryOnly.
if ($LibraryOnly) { return }
exit (Start-CiFormRun)
