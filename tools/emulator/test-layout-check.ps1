<#
.SYNOPSIS
  Self-test for Slice 267's layout-check.ps1: it must fail on the intro wizard bug (Skip drawn over Try it) and on a button cut off by the screen, and pass a clean screen. Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'layout-check.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function New-Dump([string]$Body) {
    return "<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation=""0""><node bounds=""[0,0][1080,2400]"" clickable=""false"" enabled=""true"">$Body</node></hierarchy>`nUI hierarchy dumped to: /dev/tty"
}
function New-Button([string]$Text, [string]$Bounds) {
    return "<node text=""$Text"" bounds=""$Bounds"" clickable=""true"" enabled=""true"" />"
}

# The Record step before the fix: Skip and Try it in the same cell (Slice 267, Bug B).
$overlap = New-Dump ((New-Button 'Try it' '[300,2100][780,2244]') + (New-Button 'Skip' '[300,2100][780,2244]'))
$found = @(Get-LayoutProblems $overlap 'wizard-record')
Assert-Equal 'identical bounds overlap' 1 $found.Count
Assert-Equal 'the message names both buttons' $true (($found -join ' ') -match "Try it.*Skip")

# After the fix: Try it over Skip, each with its own height.
$stacked = New-Dump ((New-Button 'Try it' '[300,2000][780,2144]') + (New-Button 'Skip' '[300,2152][780,2196]'))
Assert-Equal 'stacked buttons pass' 0 @(Get-LayoutProblems $stacked 'wizard-record').Count

# A card that holds its own button is fine; two siblings overlapping partly are not.
$nested = New-Dump ("<node bounds=""[100,500][980,900]"" clickable=""true"" enabled=""true""><node text=""Open"" bounds=""[200,700][500,850]"" clickable=""true"" enabled=""true"" /></node>")
Assert-Equal 'a button inside a clickable card passes' 0 @(Get-LayoutProblems $nested 'shelf').Count
$partial = New-Dump ((New-Button 'A' '[100,500][600,700]') + (New-Button 'B' '[400,600][900,800]'))
Assert-Equal 'a partial overlap of siblings fails' 1 @(Get-LayoutProblems $partial 'x').Count

# Touching edges and a disabled node are not problems.
$touching = New-Dump ((New-Button 'A' '[100,500][500,700]') + (New-Button 'B' '[500,500][900,700]'))
Assert-Equal 'touching edges pass' 0 @(Get-LayoutProblems $touching 'x').Count
$disabled = New-Dump ((New-Button 'A' '[100,500][500,700]') + "<node text=""B"" bounds=""[100,500][500,700]"" clickable=""true"" enabled=""false"" />")
Assert-Equal 'a disabled node is not tappable' 0 @(Get-LayoutProblems $disabled 'x').Count

# A button past the bottom of the screen.
$cut = New-Dump (New-Button 'Done' '[300,2300][780,2500]')
$cutFound = @(Get-LayoutProblems $cut 'ready')
Assert-Equal 'a button cut off by the screen fails' 1 $cutFound.Count
Assert-Equal 'the cut-off message says so' $true (($cutFound -join ' ') -match 'cut off')

Assert-Equal 'no dump is reported' $true ((@(Get-LayoutProblems 'nothing' 'x') -join ' ') -match 'no usable UI dump')

# run-flows.ps1 must wire the check in.
$runFlows = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1') -Raw
Assert-Equal 'run-flows reads the layout-check header' $true ($runFlows -match 'layout-check')
Assert-Equal 'run-flows calls Get-LayoutProblems' $true ($runFlows -match 'Get-LayoutProblems')
Assert-Equal 'run-flows reads the font-scale header' $true ($runFlows -match 'font-scale')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-layout-check: all passed.'
