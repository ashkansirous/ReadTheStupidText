<#
.SYNOPSIS
  Slice 241 item 5 self-test: a caller that reads a background-starting script's output through a pipe gets EOF when the script ends, not when the background child ends.
  Host only (no device). Runs each launch method once with a 15 s child: Start-DetachedProcess must return in well under that; the old Start-Process -Redirect form is measured and reported
  (informational: it proves the cause where the host inherits handles).
#>
$ErrorActionPreference = 'Stop'
$engine = (Get-Process -Id $PID).Path
$tmp = Join-Path $PSScriptRoot '..\tmp\detached-test'
$null = New-Item -ItemType Directory -Force $tmp
$tmp = (Resolve-Path $tmp).Path
$childSeconds = 15
$limitSeconds = 8
$failures = 0

function Measure-PipeRead([string]$Method) {
    $script = Join-Path $tmp "launcher-$Method.ps1"
    $body = @"
. '$((Join-Path $PSScriptRoot 'detached-process.ps1'))'
`$out = '$tmp\child-$Method.out'; `$err = '$tmp\child-$Method.err'
`$args2 = '-NoProfile -Command "Start-Sleep -Seconds $childSeconds"'
if ('$Method' -eq 'detached') { `$null = Start-DetachedProcess '$engine' `$args2 `$out `$err }
else { `$null = Start-Process '$engine' -ArgumentList @('-NoProfile','-Command','Start-Sleep -Seconds $childSeconds') -PassThru -WindowStyle Hidden -RedirectStandardOutput `$out -RedirectStandardError `$err }
Write-Output 'launcher done'
"@
    Set-Content $script $body
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $engine
    $psi.Arguments = "-NoProfile -File `"$script`""
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = [System.Diagnostics.Process]::Start($psi)
    $null = $p.StandardOutput.ReadToEnd()   # returns at EOF: when every holder of the pipe's write end is gone
    $null = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    return $sw.Elapsed.TotalSeconds
}

$detached = Measure-PipeRead 'detached'
Write-Host ("detached start: reader got EOF after {0:n1} s (limit {1} s)" -f $detached, $limitSeconds)
if ($detached -gt $limitSeconds) { Write-Host 'FAIL: the detached child held the caller pipe.'; $failures++ } else { Write-Host 'PASS: detached child does not hold the caller pipe.' }

if ($env:RTST_TEST_OLD_START_PROCESS -eq '1') {
    $old = Measure-PipeRead 'startprocess'
    Write-Host ("Start-Process -Redirect...: reader got EOF after {0:n1} s (child lives {1} s)" -f $old, $childSeconds)
}
Start-Sleep -Seconds ($childSeconds + 1)   # the sleeping children end by themselves; none is left behind
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
if ($failures) { exit 1 }
Write-Host 'test-detached-process: all passed.'
