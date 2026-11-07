<#
.SYNOPSIS
  Self-test for Slice 267's layout-check.ps1: it must fail on the intro wizard bug (Skip drawn over Try it) and on a button cut off by the screen, and pass a clean screen. Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'layout-check.lib.ps1')
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

# The real pre-fix dump (large font): only Skip is clickable, "Try it" is a plain label (and its group's content-desc) drawn inside Skip's bounds.
$tryItUnderSkip = New-Dump ("<node bounds=""[58,2108][1023,2269]"" clickable=""false"" enabled=""true"">" +
    "<node content-desc=""Try it"" bounds=""[58,2108][1023,2269]"" clickable=""false"" enabled=""true""><node text=""Try it"" bounds=""[481,2149][601,2216]"" clickable=""false"" enabled=""true"" /></node>" +
    (New-Button 'Skip' '[58,2131][1023,2247]') + "</node>")
$underFound = @(Get-LayoutProblems $tryItUnderSkip 'wizard-record-large-font')
Assert-Equal 'a label drawn under another button fails' $true ($underFound.Count -ge 1)
Assert-Equal 'the message names the label and the button' $true (($underFound -join ' ') -match "text 'Try it' is covered by 'Skip'")

# No false positives: a label inside its own button, and text in a card under a clickable card parent.
$ownLabel = New-Dump ("<node bounds=""[300,2000][780,2144]"" clickable=""true"" enabled=""true""><node text=""Try it"" bounds=""[400,2040][680,2100]"" clickable=""false"" enabled=""true"" /></node>")
Assert-Equal 'a label inside its own button passes' 0 @(Get-LayoutProblems $ownLabel 'x').Count
$card = New-Dump ("<node bounds=""[100,500][980,900]"" clickable=""true"" enabled=""true""><node bounds=""[120,520][960,880]"" clickable=""false"" enabled=""true""><node text=""Title"" bounds=""[140,540][900,600]"" clickable=""false"" enabled=""true"" /><node content-desc=""Cover"" bounds=""[140,620][900,860]"" clickable=""false"" enabled=""true"" /></node></node>" +
    "<node text=""Heading"" bounds=""[100,100][980,200]"" clickable=""false"" enabled=""true"" />")
Assert-Equal 'text in a card under a clickable parent passes' 0 @(Get-LayoutProblems $card 'shelf').Count

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

# Slice 297 item 3: a full-screen tap-to-dismiss scrim (clickable, no text, whole-window bounds) beside a button is not an overlap; a real overlap next to it still fails.
$scrim = New-Dump ("<node bounds=""[0,0][1080,2400]"" clickable=""true"" enabled=""true"" />" + (New-Button 'Close' '[300,2000][780,2144]') + "<node text=""Menu"" bounds=""[100,500][900,700]"" clickable=""false"" enabled=""true"" />")
Assert-Equal 'a scrim sibling passes' 0 @(Get-LayoutProblems $scrim 'menu').Count
$scrimAndOverlap = New-Dump ("<node bounds=""[0,0][1080,2400]"" clickable=""true"" enabled=""true"" />" + (New-Button 'Try it' '[300,2100][780,2244]') + (New-Button 'Skip' '[300,2100][780,2244]'))
Assert-Equal 'a real overlap still fails beside a scrim' 1 @(Get-LayoutProblems $scrimAndOverlap 'wizard-record').Count
$bigButton = New-Dump ((New-Button 'Everything' '[0,0][1080,2400]') + (New-Button 'Other' '[300,2000][780,2144]'))
Assert-Equal 'a full-screen node with text is not a scrim' 1 @(Get-LayoutProblems $bigButton 'x').Count

# Slice 297 item 3 (review): a scrim drawn AFTER a button is on top of it and swallows its taps, so it is reported; a text label under a later scrim too, but not under an earlier one.
$scrimOnTop = New-Dump ((New-Button 'Close' '[300,2000][780,2144]') + "<node bounds=""[0,0][1080,2400]"" clickable=""true"" enabled=""true"" />")
Assert-Equal 'a scrim drawn over a button is reported' 1 @(Get-LayoutProblems $scrimOnTop 'x').Count
$scrimOverLabel = New-Dump ("<node text=""Menu"" bounds=""[100,500][900,700]"" clickable=""false"" enabled=""true"" />" + "<node bounds=""[0,0][1080,2400]"" clickable=""true"" enabled=""true"" />")
Assert-Equal 'a scrim drawn over a label is reported' 1 @(Get-LayoutProblems $scrimOverLabel 'x').Count

# Slice 297 item 4: the CLI wrapper keeps param(): a positional dump path and the -Name=value form still work, and no dump exits 2.
$cli = Join-Path $PSScriptRoot 'layout-check.ps1'
$dumpFile = Join-Path ([System.IO.Path]::GetTempPath()) "layout-check-selftest-$PID.xml"
Set-Content -Path $dumpFile -Value $scrimAndOverlap
$shell = (Get-Process -Id $PID).Path
& $shell -NoProfile -File $cli $dumpFile | Out-Null
Assert-Equal 'positional dump path is read (overlap, exit 1)' 1 $LASTEXITCODE
& $shell -NoProfile -File $cli -LayoutXmlPath $dumpFile -ScreenLabel wizard | Out-Null
Assert-Equal 'named dump path is read (overlap, exit 1)' 1 $LASTEXITCODE
& $shell -NoProfile -File $cli | Out-Null
Assert-Equal 'no argument exits 2' 2 $LASTEXITCODE
Remove-Item $dumpFile

# Slice 297 item 2: dot-sourcing the check leaves the caller's scope alone (no param() variables, no changed ErrorActionPreference).
function Test-DotSourceLeaks {
    $ErrorActionPreference = 'Continue'
    . (Join-Path $PSScriptRoot 'layout-check.lib.ps1')
    return @{ Preference = "$ErrorActionPreference"; Label = [bool](Get-Variable ScreenLabel -ErrorAction SilentlyContinue); Xml = [bool](Get-Variable LayoutXmlPath -ErrorAction SilentlyContinue) }
}
$leak = Test-DotSourceLeaks
Assert-Equal 'dot-sourcing keeps the caller ErrorActionPreference' 'Continue' $leak.Preference
Assert-Equal 'dot-sourcing defines no ScreenLabel in the caller' $false $leak.Label
Assert-Equal 'dot-sourcing defines no LayoutXmlPath in the caller' $false $leak.Xml

# run-flows.ps1 must wire the check in.
$runFlows = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1') -Raw
Assert-Equal 'run-flows reads the layout-check header' $true ($runFlows -match 'layout-check')
Assert-Equal 'run-flows calls Get-LayoutProblems' $true ($runFlows -match 'Get-LayoutProblems')
Assert-Equal 'run-flows reads the font-scale header' $true ($runFlows -match 'font-scale')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-layout-check: all passed.'
exit 0
