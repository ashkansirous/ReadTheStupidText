<#
.SYNOPSIS
  Self-test of the pure functions of reset-dino-language.ps1 (Slice 393 item 15): dot-sources it, no device.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'reset-dino-language.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { Write-Host "FAIL $Name : expected '$Expected', got '$Actual'"; $script:failed++ }
}
function Assert-Throws([string]$Name, [scriptblock]$Block) {
    try { & $Block; Write-Host "FAIL $Name : no error"; $script:failed++ } catch { }
}

Assert-Equal 'the hook broadcast for English' 'shell am broadcast -a uk.sirous.dino-test.TEST_HOOK --es cmd set-language --es lang en' ((Get-LanguageResetArguments 'en') -join ' ')
Assert-Equal 'a regional code is accepted' 'pt-BR' ((Get-LanguageResetArguments 'pt-BR')[-1])
Assert-Throws 'a shell metacharacter is refused' { Get-LanguageResetArguments 'en; rm' }
Assert-Throws 'an empty code is refused' { Get-LanguageResetArguments '' }
Assert-Equal 'the ok line is found' $true (Test-LanguageResetLogged "11-18 10:00:00 I RTST-TEST: HOOK set-language ok en`n")
Assert-Equal 'a failed line is not an ok' $false (Test-LanguageResetLogged 'I RTST-TEST: HOOK set-language failed unknown xx')
Assert-Equal 'an empty log is not an ok' $false (Test-LanguageResetLogged '')
Assert-Equal 'an installed package is listed' $true (Test-PackageListed "package:uk.sirous.dinoreader.dbg`r`n" 'uk.sirous.dinoreader.dbg')
Assert-Equal 'a longer package name is not the app' $false (Test-PackageListed 'package:uk.sirous.dinoreader.dbg.x' 'uk.sirous.dinoreader.dbg')
Assert-Equal 'an empty list is not installed' $false (Test-PackageListed '' 'uk.sirous.dinoreader.dbg')
Assert-Equal 'a device time is a -T stamp' '11-27 10:01:02.000' (Get-LogcatSinceStamp "11-27 10:01:02.000`r`n")
Assert-Equal 'an adb error is no stamp' $null (Get-LogcatSinceStamp 'error: device offline')
Assert-Equal 'a pid line means running' $true (Test-PidFound "12345`n")
Assert-Equal 'an empty pidof means not running' $false (Test-PidFound '')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-reset-dino-language: all passed.'
exit 0
