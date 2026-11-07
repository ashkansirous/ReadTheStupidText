<#
.SYNOPSIS
  Slice 267 (Bug B): fails when two tappable elements on one screen overlap, or one is cut off by the screen edge, read from a `uiautomator dump`.
.DESCRIPTION
  Maestro taps the centre of an element's bounds from the accessibility tree, so a button covered by another still "passes" a flow. This reads the same tree and checks the bounds:
    - two enabled clickable nodes that overlap, when neither is inside the other in the tree (a card that holds its own button is fine; two siblings drawn on the same spot are not);
    - a node with visible text or a content description that overlaps a clickable node which is neither its ancestor nor its descendant (a label inside its own button is fine; the label need not be clickable itself);
    - an enabled clickable node whose bounds reach past the screen (the root node's bounds) by more than the tolerance.
  The pure functions (Get-LayoutProblems) live in layout-check.lib.ps1, which run-flows.ps1 and the self-test dot-source (Slice 297: this file has a param() block, so it is never dot-sourced). Run it: `layout-check.ps1 -LayoutXmlPath dump.xml` checks a saved dump with no device, `layout-check.ps1 -AdbSerial emulator-5554 -ScreenLabel wizard-record`
  dumps the screen over adb first. Exit 0 = no problem, 1 = problems (one `label: message` line each), 2 = no usable dump.
  run-flows.ps1 calls Get-LayoutProblems after a flow whose header carries `# layout-check`, on the screen the flow ended on, and a flow with `# font-scale: <n>` runs at that system font scale.
#>
param(
    [string]$LayoutXmlPath,
    [string]$AdbSerial,
    [string]$ScreenLabel = 'screen'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'layout-check.lib.ps1')   # Slice 297: the pure functions live in the library, which run-flows.ps1 and the self-test dot-source

if ($LayoutXmlPath) { $dump = Get-Content -Raw -Path $LayoutXmlPath }
elseif ($AdbSerial) {
    $adb = if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe' } else { 'adb' }
    $devicePath = '/sdcard/rtst-ui-dump.xml'   # not /dev/tty: from a non-tty host shell that returns no XML
    & $adb -s $AdbSerial shell rm -f $devicePath | Out-Null
    & $adb -s $AdbSerial shell uiautomator dump $devicePath | Out-Null
    $dump = (& $adb -s $AdbSerial exec-out cat $devicePath | Out-String)
    & $adb -s $AdbSerial shell rm -f $devicePath | Out-Null
}
else { Write-Host 'Pass -LayoutXmlPath <dump.xml> or -AdbSerial <emulator-5554>.'; exit 2 }
try { $found = @(Get-LayoutProblems $dump $ScreenLabel) }
catch { Write-Host "${ScreenLabel}: no usable UI dump ($($_.Exception.Message))"; exit 2 }   # a malformed dump is exit 2, not a layout failure
if ($found.Count -eq 1 -and $found[0] -match 'no usable UI dump') { Write-Host $found[0]; exit 2 }
$found | ForEach-Object { Write-Host $_ }
if ($found.Count -gt 0) { exit 1 }
Write-Host "${ScreenLabel}: no overlapping or cut-off tappable elements."
