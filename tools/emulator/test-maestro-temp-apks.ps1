<#
.SYNOPSIS
  Self-test for the Maestro temp folder cleanup (Slice 315 items 1, 2, 9): the Maestro CLI (Java) leaves a tmp<digits>.apk copy of the dev app (69-97 MB) in its java.io.tmpdir per clearState.
  run-flows.ps1 gives each run its own java.io.tmpdir (MAESTRO_OPTS) and deletes that folder after the run, so nothing is chosen by time or name and a parallel run's files are never touched.
  Needs no emulator. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$base = Join-Path ([IO.Path]::GetTempPath()) ('rtst-maestro-tmp-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force $base
try {
    $mine = Get-MaestroTempDir $base 1
    $other = Get-MaestroTempDir $base 2
    Assert-Equal 'the folder is per instance' $false ($mine -eq $other)
    Assert-Equal 'the folder is per process' $true ($mine.EndsWith("i1-$PID"))
    Assert-Equal 'the option names the folder' "-Djava.io.tmpdir=$mine" (Get-MaestroTempOption $mine)

    $null = New-Item -ItemType Directory -Force $mine, $other
    foreach ($name in @('tmp2733599697363530152.apk', 'tmp7.apk', 'other.bin')) { Set-Content -LiteralPath (Join-Path $mine $name) -Value 'x' }
    Set-Content -LiteralPath (Join-Path $other 'tmp9.apk') -Value 'x'
    Set-Content -LiteralPath (Join-Path $base 'tmp1.apk') -Value 'x'   # a stranger's file next to the folders: never touched

    Assert-Equal 'three files counted and removed' 3 (Remove-MaestroTempDir $mine)
    Assert-Equal 'my folder is gone' $false (Test-Path -LiteralPath $mine)
    Assert-Equal 'a parallel run folder stays' $true (Test-Path -LiteralPath (Join-Path $other 'tmp9.apk'))
    Assert-Equal 'a file outside the folders stays' $true (Test-Path -LiteralPath (Join-Path $base 'tmp1.apk'))
    Assert-Equal 'a second cleanup finds nothing' 0 (Remove-MaestroTempDir $mine)

    $locked = Join-Path $other 'tmp555.apk'
    Set-Content -LiteralPath $locked -Value 'x'
    $stream = [IO.File]::Open($locked, 'Open', 'Read', 'None')
    try { Assert-Equal 'a file held open is skipped, not an error' 0 (Remove-MaestroTempDir $other) } finally { $stream.Dispose() }
    Assert-Equal 'the held file was still there' $true (Test-Path -LiteralPath $locked)
    Assert-Equal 'once released the folder goes' 2 (Remove-MaestroTempDir $other)

    # Item 1: the start-of-run sweep removes a dead pid's folder and never a live pid's (this process's own pid is alive).
    $deadPid = 900000
    while (Test-ProcessAlive $deadPid) { $deadPid++ }
    $dead = Join-Path $base "maestro-tmp\i1-$deadPid"
    $live = Join-Path $base "maestro-tmp\i2-$PID"
    $stranger = Join-Path $base 'maestro-tmp\keep-me'
    $null = New-Item -ItemType Directory -Force $dead, $live, $stranger
    Set-Content -LiteralPath (Join-Path $dead 'tmp1.apk') -Value 'x'
    Set-Content -LiteralPath (Join-Path $live 'tmp2.apk') -Value 'x'
    Assert-Equal 'the sweep removes one folder' 1 (Remove-StaleMaestroTempDirs $base)
    Assert-Equal 'the dead pid folder is gone' $false (Test-Path -LiteralPath $dead)
    Assert-Equal 'the live pid folder stays with its file' $true (Test-Path -LiteralPath (Join-Path $live 'tmp2.apk'))
    Assert-Equal 'a folder not named i<n>-<pid> stays' $true (Test-Path -LiteralPath $stranger)

    # Item 3: a path with a space becomes its short path, or throws; one without a space is unchanged.
    Assert-Equal 'a path without a space is unchanged' $base (Get-SpaceFreePath $base)
    $spaced = Join-Path $base 'with space'
    $null = New-Item -ItemType Directory -Force $spaced
    $threw = $false
    $result = ''
    try { $result = Get-SpaceFreePath $spaced } catch { $threw = $true }
    Assert-Equal 'a spaced path gives a space-free short path or a clear error' $true ($threw -or ($result -notmatch '\s' -and (Test-Path -LiteralPath $result)))

    $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'run-flows.ps1'))
    Assert-Equal 'run-flows.ps1 passes the folder to the JVM' $true ($source.Contains('Get-MaestroTempOption (Get-SpaceFreePath $script:MaestroTempDir)'))
    Assert-Equal 'run-flows.ps1 runs the cleanup' $true ($source.Contains('Remove-MaestroTempDir $script:MaestroTempDir'))
    Assert-Equal 'run-flows.ps1 sweeps stale folders' $true ($source.Contains('Remove-StaleMaestroTempDirs $toolsDir'))
} finally { Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-maestro-temp-apks: all passed.'
exit 0
