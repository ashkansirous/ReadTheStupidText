<#
.SYNOPSIS
  Self-test for Test-LogMarker (Slice 269): a marker followed by "," or ":" matches, a longer token does not. Needs no emulator. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

Assert-Equal 'a whole marker matches' $true (Test-LogMarker "I RTST-TEST: PAGE 1" 'PAGE 1')
Assert-Equal 'a marker followed by a comma matches' $true (Test-LogMarker "PAGE 1, words=4" 'PAGE 1')
Assert-Equal 'a marker followed by a colon matches' $true (Test-LogMarker "PAGE 1: ok" 'PAGE 1')
Assert-Equal 'PAGE 1 does not match PAGE 10' $false (Test-LogMarker "PAGE 10" 'PAGE 1')
Assert-Equal 'PAGE 1 does not match PAGE 1.5' $false (Test-LogMarker "PAGE 1.5" 'PAGE 1')
Assert-Equal 'PAGE 1 does not match PAGE 1/10' $false (Test-LogMarker "PAGE 1/10" 'PAGE 1')
Assert-Equal 'PAGE 1 does not match PAGE 1-x' $false (Test-LogMarker "PAGE 1-x" 'PAGE 1')
Assert-Equal 'a marker followed by a full stop at the end matches' $true (Test-LogMarker "done PAGE 1." 'PAGE 1')
Assert-Equal 'a marker inside a longer token does not match' $false (Test-LogMarker "XPAGE 1" 'PAGE 1')
Assert-Equal 'regex characters in a marker are literal' $true (Test-LogMarker "got a.b (1)" 'a.b (1)')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-log-marker: all passed.'
exit 0
