<#
.SYNOPSIS
  Slice 267 (Bug B): fails when two tappable elements on one screen overlap, or one is cut off by the screen edge, read from a `uiautomator dump`.
.DESCRIPTION
  Maestro taps the centre of an element's bounds from the accessibility tree, so a button covered by another still "passes" a flow. This reads the same tree and checks the bounds:
    - two enabled clickable nodes that overlap, when neither is inside the other in the tree (a card that holds its own button is fine; two siblings drawn on the same spot are not);
    - an enabled clickable node whose bounds reach past the screen (the root node's bounds) by more than the tolerance.
  Dot-source it for the pure functions (Get-LayoutProblems), or run it: `layout-check.ps1 -LayoutXmlPath dump.xml` checks a saved dump with no device, `layout-check.ps1 -AdbSerial emulator-5554 -ScreenLabel wizard-record`
  dumps the screen over adb first. Exit 0 = no problem, 1 = problems (one `label: message` line each), 2 = no usable dump.
  run-flows.ps1 calls Get-LayoutProblems after a flow whose header carries `# layout-check`, on the screen the flow ended on, and a flow with `# font-scale: <n>` runs at that system font scale.
#>
param(
    [string]$LayoutXmlPath,
    [string]$AdbSerial,
    [string]$ScreenLabel = 'screen'
)
$ErrorActionPreference = 'Stop'

$script:OverlapShareOfSmaller = 0.15   # a touching edge or a few pixels of shadow is not an overlap: the shared area must be this share of the smaller element
$script:EdgeTolerancePx = 2            # bounds are whole pixels and rounded: two pixels past the edge is not a cut-off

# Pure: the dump as an XmlDocument. The dump written to /dev/tty ends with a "UI hierarchy dumped to" line after the XML, which is cut off first.
function ConvertTo-UiDump([string]$Text) {
    $end = $Text.LastIndexOf('</hierarchy>')
    $start = $Text.IndexOf('<?xml')
    if ($start -lt 0) { $start = $Text.IndexOf('<hierarchy') }
    if ($end -lt 0 -or $start -lt 0) { return $null }
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml($Text.Substring($start, $end + '</hierarchy>'.Length - $start))
    return $doc
}

# Pure: "[l,t][r,b]" as @(l, t, r, b), or $null.
function ConvertTo-Bounds([string]$Text) {
    $m = [regex]::Match("$Text", '^\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]$')
    if (-not $m.Success) { return $null }
    return @([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value, [int]$m.Groups[4].Value)
}

function Get-Area($B) { return [math]::Max(0, $B[2] - $B[0]) * [math]::Max(0, $B[3] - $B[1]) }

function Get-IntersectionArea($A, $B) {
    $w = [math]::Min($A[2], $B[2]) - [math]::Max($A[0], $B[0])
    $h = [math]::Min($A[3], $B[3]) - [math]::Max($A[1], $B[1])
    if ($w -le 0 -or $h -le 0) { return 0 }
    return $w * $h
}

# Pure: every enabled clickable node with an area, as objects carrying its bounds, its name for the message and the chain of its ancestors' index paths.
function Get-TappableNodes([System.Xml.XmlNode]$Parent, [string]$Path, [System.Collections.Generic.List[object]]$Found) {
    $i = 0
    foreach ($node in $Parent.ChildNodes) {
        if ($node.NodeType -ne 'Element' -or $node.Name -ne 'node') { continue }
        $here = "$Path/$i"
        $i++
        $bounds = ConvertTo-Bounds $node.GetAttribute('bounds')
        if ($node.GetAttribute('clickable') -eq 'true' -and $node.GetAttribute('enabled') -ne 'false' -and $bounds -and (Get-Area $bounds) -gt 0) {
            $name = $node.GetAttribute('resource-id'); if (-not $name) { $name = $node.GetAttribute('text') }; if (-not $name) { $name = $node.GetAttribute('content-desc') }; if (-not $name) { $name = $node.GetAttribute('class') }
            $Found.Add([pscustomobject]@{ Path = $here; Bounds = $bounds; Name = "$name" })
        }
        Get-TappableNodes $node $here $Found
    }
}

function Test-IsAncestorPath([string]$Ancestor, [string]$Path) { return $Path.StartsWith($Ancestor + '/') }

# Pure: the problems of one dump, as strings ($Label first). Empty when the screen is fine.
function Get-LayoutProblems([string]$Text, [string]$Label = 'screen') {
    $doc = ConvertTo-UiDump $Text
    if (-not $doc) { return @("${Label}: no usable UI dump") }
    $rootNode = $doc.SelectSingleNode('//hierarchy/node')
    $screen = if ($rootNode) { ConvertTo-Bounds $rootNode.GetAttribute('bounds') } else { $null }
    $tappable = New-Object 'System.Collections.Generic.List[object]'
    Get-TappableNodes $doc.DocumentElement '' $tappable
    $problems = @()
    if ($screen) {
        foreach ($t in $tappable) {
            $b = $t.Bounds; $tol = $script:EdgeTolerancePx
            if ($b[0] -lt $screen[0] - $tol -or $b[1] -lt $screen[1] - $tol -or $b[2] -gt $screen[2] + $tol -or $b[3] -gt $screen[3] + $tol) {
                $problems += "${Label}: '$($t.Name)' is cut off by the screen edge (bounds [$($b -join ',')], screen [$($screen -join ',')])"
            }
        }
    }
    for ($a = 0; $a -lt $tappable.Count; $a++) {
        for ($c = $a + 1; $c -lt $tappable.Count; $c++) {
            $x = $tappable[$a]; $y = $tappable[$c]
            if ((Test-IsAncestorPath $x.Path $y.Path) -or (Test-IsAncestorPath $y.Path $x.Path)) { continue }
            $shared = Get-IntersectionArea $x.Bounds $y.Bounds
            $smaller = [math]::Min((Get-Area $x.Bounds), (Get-Area $y.Bounds))
            if ($smaller -gt 0 -and $shared -ge $script:OverlapShareOfSmaller * $smaller) {
                $problems += "${Label}: '$($x.Name)' and '$($y.Name)' overlap by $([int](100 * $shared / $smaller))% of the smaller one (bounds [$($x.Bounds -join ',')] and [$($y.Bounds -join ',')])"
            }
        }
    }
    return $problems
}

if ($MyInvocation.InvocationName -ne '.') {
    if ($LayoutXmlPath) { $dump = Get-Content -Raw -Path $LayoutXmlPath }
    elseif ($AdbSerial) {
        $adb = if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe' } else { 'adb' }
        $dump = (& $adb -s $AdbSerial shell uiautomator dump /dev/tty | Out-String)
    }
    else { Write-Host 'Pass -LayoutXmlPath <dump.xml> or -AdbSerial <emulator-5554>.'; exit 2 }
    $found = @(Get-LayoutProblems $dump $ScreenLabel)
    if ($found.Count -eq 1 -and $found[0] -match 'no usable UI dump') { Write-Host $found[0]; exit 2 }
    $found | ForEach-Object { Write-Host $_ }
    if ($found.Count -gt 0) { exit 1 }
    Write-Host "${ScreenLabel}: no overlapping or cut-off tappable elements."
}
