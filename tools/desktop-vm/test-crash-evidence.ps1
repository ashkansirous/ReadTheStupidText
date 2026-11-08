<#
.SYNOPSIS
  Self-test for the crash-evidence step (Slice 259 item 4): runs the guest-side block (_common.ps1 $script:CrashEvidenceBlock) on the host against a fake package
  folder and checks the files a failed run leaves. Needs no VM and no package. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("rtst-crash-evidence-" + [guid]::NewGuid().ToString('N'))
$oldLocal = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $root 'local'
    $pkg = Join-Path $env:LOCALAPPDATA 'Packages\Fake.Pkg_abc'
    New-Item -ItemType Directory -Force -Path (Join-Path $pkg 'LocalState'), (Join-Path $pkg 'TempState\logs') | Out-Null
    1..300 | ForEach-Object { "trace line $_" } | Set-Content (Join-Path $pkg 'LocalState\window-trace.log')
    'log line' | Set-Content (Join-Path $pkg 'TempState\logs\app-20260101.log')
    $out = Join-Path $root 'out'
    & $script:CrashEvidenceBlock $out 'NoSuchExe.Test' 'Fake.Pkg' ([datetime]::Now.AddMinutes(-5)) $script:CrashEvidenceTailLines
    Assert-Equal 'events.txt is written even with no events' $true (Test-Path (Join-Path $out 'events.txt'))
    Assert-Equal 'events.txt says there were none' $true ((Get-Content (Join-Path $out 'events.txt') -Raw) -like '*No Application-log errors naming NoSuchExe.Test*')
    $trace = @(Get-Content (Join-Path $out 'trace-tail-window-trace.log.txt'))
    Assert-Equal 'trace tail keeps the last 200 lines' 200 $trace.Count
    Assert-Equal 'trace tail ends with the newest line' 'trace line 300' $trace[-1]
    Assert-Equal 'rolling log tail is copied' $true (Test-Path (Join-Path $out 'trace-tail-app-20260101.log.txt'))
    $none = Join-Path $root 'out-none'
    & $script:CrashEvidenceBlock $none 'NoSuchExe.Test' 'Missing.Pkg' ([datetime]::Now.AddMinutes(-5)) $script:CrashEvidenceTailLines
    Assert-Equal 'a missing package folder is written down, not an error' $true (Test-Path (Join-Path $none 'trace-tail-none.txt'))
} finally {
    $env:LOCALAPPDATA = $oldLocal
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}
if ($script:failed -gt 0) { Write-Host "$script:failed check(s) failed"; exit 1 }
Write-Host 'crash-evidence self-test: all passed'
exit 0
