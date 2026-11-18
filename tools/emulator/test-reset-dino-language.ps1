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

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-reset-dino-language: all passed.'
exit 0
