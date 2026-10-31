<#
.SYNOPSIS
  Taps an element of the app on screen, found by its text, resource id or content description (Slice 206).
.DESCRIPTION
  `uiautomator dump` to /sdcard/window.xml (the old file is deleted first and the dump must report success, retried once, else the script fails: a stale
  dump is never tapped), read it back, check the app on screen is the expected dev app (-Package, default the .dbg package of -App rtst|dino; SystemUI
  nodes are ignored; exit 6 and nothing tapped when the screen shows another app), find the node among that app's nodes (exact match first, then case-insensitive
  contains; -Index picks the nth of several, 0-based; a contains match with more than one hit and no -Index exits 3 listing them, so "OK" never taps "Cook"),
  tap the centre of its bounds, wait until the screen changes (-WaitSeconds) and print what changed (nodes added or removed, including a switch or radio row
  flipping its checked/selected state). -Scroll swipes up
  and looks again, up to 6 swipes. Exit 3 when nothing matches, listing the clickable texts on screen. -List only prints the visible texts and ids.
  -ExpectText <text> makes the script exit 5 when that text is not on screen after the tap (within -WaitSeconds). Exit 1: the dump before the tap failed.
  Exit 7: the tap WAS sent but no dump after it could be read; the tap is never repeated by the script, so check the screen before tapping again.
  The screen comparison ignores the status bar (SystemUI), so a clock tick or a notification is not a change.
  -XmlFile reads a saved dump instead of the phone and taps nothing (used by test-tap.ps1). Wireless adb only; respects the phone lease.
  Source for the dump: Android's `uiautomator dump [file]` writes the view hierarchy as XML with text, resource-id, content-desc, clickable and bounds attributes.
#>
param(
    [Parameter(Mandatory, ParameterSetName = 'Text')][string]$Text,
    [Parameter(Mandatory, ParameterSetName = 'Id')][string]$Id,
    [Parameter(Mandatory, ParameterSetName = 'Desc')][string]$Desc,
    [Parameter(Mandatory, ParameterSetName = 'List')][switch]$List,
    [int]$Index = 0,
    [ValidateSet('rtst', 'dino')][string]$App = 'rtst',
    [string]$Package,
    [switch]$Scroll,
    [int]$WaitSeconds = 10,
    [string]$ExpectText,
    [string]$Serial,
    [string]$XmlFile
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$script:DumpPath = '/sdcard/window.xml'
$script:MaxSwipes = 6
$script:SwipeMs = 300
$script:PollMs = 500
$script:DumpAttempts = 2
$script:ExitExpectMissed = 5
$script:ExitWrongApp = 6
$script:ExitTapNotConfirmed = 7
$script:IndexGiven = $PSBoundParameters.ContainsKey('Index')
$script:ExpectedPackage = if ($Package) { $Package } else { Get-DevPackage $App }
Assert-DevPackage $script:ExpectedPackage

function Get-ScreenNodes([string]$Phone) {
    if ($XmlFile) { return @(Read-UiNodes (Get-Content $XmlFile -Raw)) }
    for ($attempt = 1; $attempt -le $script:DumpAttempts; $attempt++) {
        Invoke-Adb $Phone @('shell', 'rm', '-f', $script:DumpPath) | Out-Null
        $result = Invoke-Adb $Phone @('shell', 'uiautomator', 'dump', $script:DumpPath)
        if (Test-UiDumpOk $result) { return @(Read-UiNodes ((Invoke-Adb $Phone @('shell', 'cat', $script:DumpPath)) -join "`n")) }
        Start-Sleep -Milliseconds 700
    }
    throw "uiautomator dump failed $($script:DumpAttempts) times (the screen may never go idle): $($result -join ' | ')"
}

function Get-DistinctTexts($Nodes) {
    return @($Nodes | ForEach-Object { if ($_.Text) { $_.Text } elseif ($_.Desc) { $_.Desc } } | Select-Object -Unique)
}

function Write-ScreenList($Nodes) {
    Write-Host 'On screen (text | id | clickable):'
    foreach ($n in $Nodes | Where-Object { $_.Text -or $_.Id -or $_.Desc }) {
        Write-Host ("  {0} | {1} | {2}" -f $(if ($n.Text) { $n.Text } else { $n.Desc }), $n.Id, $n.Clickable)
    }
}

# Swipes up the middle of the screen (the screen size is the largest bounds in the dump).
function Invoke-SwipeUp([string]$Phone, $Nodes) {
    $width = ($Nodes | Measure-Object -Property Right -Maximum).Maximum
    $height = ($Nodes | Measure-Object -Property Bottom -Maximum).Maximum
    $x = [int]($width / 2)
    Invoke-Adb $Phone @('shell', 'input', 'swipe', $x, [int]($height * 0.75), $x, [int]($height * 0.25), $script:SwipeMs) | Out-Null
    Start-Sleep -Milliseconds 700
}

# Exits 6 (nothing tapped) when the screen is not the expected dev app: the store app, the launcher or the user's own apps are never tapped.
function Assert-ExpectedApp($Nodes) {
    $state = Resolve-ForegroundPackage $Nodes $script:ExpectedPackage
    if ($state -eq 'Match') { return }
    $seen = @(Select-AppNodes $Nodes | Where-Object { $_.Package } | ForEach-Object { $_.Package } | Select-Object -Unique)
    Write-Host "The screen is not $($script:ExpectedPackage) (seen: $(if ($seen) { $seen -join ', ' } else { 'no package in the dump' })). Not tapping."
    exit $script:ExitWrongApp
}

# The nth match among the expected app's nodes, scrolling for it when asked. Returns @{ Node; Nodes; Kind; Ambiguous } (Node is $null when nothing matched
# or when a contains match has several hits and no -Index was given; Ambiguous then holds them).
function Find-OnScreen([string]$Phone, [string]$Attribute, [string]$Value) {
    $nodes = Get-ScreenNodes $Phone
    for ($swipe = 0; ; $swipe++) {
        Assert-ExpectedApp $nodes
        $match = Resolve-UiMatch @($nodes | Where-Object { $_.Package -ceq $script:ExpectedPackage }) $Attribute $Value
        $found = @($match.Nodes)
        if ($match.Kind -eq 'Contains' -and $found.Count -gt 1 -and -not $script:IndexGiven) { return @{ Node = $null; Nodes = $nodes; Kind = $match.Kind; Ambiguous = $found } }
        if ($found.Count -gt $Index) { return @{ Node = $found[$Index]; Nodes = $nodes; Kind = $match.Kind; Ambiguous = @() } }
        if (-not $Scroll -or $XmlFile -or $swipe -ge $script:MaxSwipes) { return @{ Node = $null; Nodes = $nodes; Kind = $match.Kind; Ambiguous = @() } }
        Invoke-SwipeUp $Phone $nodes
        $nodes = Get-ScreenNodes $Phone
    }
}

# The full, uncut change (every signature) appended to <worktrees>\.phone\tap-changes.log. A log that cannot be written is said, never fatal.
function Write-ScreenChangeLog($Diff) {
    try {
        $dir = Join-Path (Get-WorktreesRoot) '.phone'
        $null = New-Item -ItemType Directory -Force $dir
        $entry = "{0:s} Added: {1}`r`n{0:s} Removed: {2}" -f (Get-Date), ($Diff.Added -join ' ; '), ($Diff.Removed -join ' ; ')
        Add-Content (Join-Path $dir 'tap-changes.log') $entry
    } catch { Write-Host "   (could not write tap-changes.log: $($_.Exception.Message))" }
}

# Waits until the screen differs from $Before (any app node added or removed, toggle state included), prints what changed and returns the last nodes seen.
# The tap is already sent: a dump that fails here is retried (the dump only, never the tap) until the deadline; $null comes back when none could be read.
function Write-ScreenChange([string]$Phone, $Before) {
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $afterNodes = $null
    do {
        Start-Sleep -Milliseconds $script:PollMs
        try { $afterNodes = Get-ScreenNodes $Phone } catch { Write-Host "   (dump after the tap failed, trying again: $($_.Exception.Message))"; continue }
        $diff = Compare-Screens $Before $afterNodes
        if ($diff.Changed) {
            # Slice 216: the run output gets the changed texts only, cut to 120 characters; the full signatures go to tap-changes.log.
            Write-Host ('Screen changed. ' + (Format-ScreenChangeLine 'Added' $diff.Added))
            if ($diff.Removed.Count -gt 0) { Write-Host (Format-ScreenChangeLine 'Removed' $diff.Removed) }
            Write-ScreenChangeLog $diff
            return $afterNodes
        }
    } while ((Get-Date) -lt $deadline)
    if ($afterNodes) { Write-Host "Screen did not change within $WaitSeconds s (texts now: $((Get-DistinctTexts $afterNodes) -join ' | '))." }
    return $afterNodes
}

$phone = if ($XmlFile) { 'saved-dump' } else { Find-WirelessPhone $Serial }
if (-not $phone) { Write-PairingAsk; exit 2 }
if (-not $XmlFile) { Assert-PhoneLease $phone }
if ($List) { Write-ScreenList (Get-ScreenNodes $phone); exit 0 }
$attribute = $PSCmdlet.ParameterSetName
$value = switch ($attribute) { 'Text' { $Text } 'Id' { $Id } 'Desc' { $Desc } }
$hit = Find-OnScreen $phone $attribute $value
if ($hit.Ambiguous.Count -gt 0) {
    Write-Host "$($hit.Ambiguous.Count) elements only contain $attribute '$value' (no exact match); not guessing. Pass the full text, or -Index n to choose:"
    $i = 0; foreach ($n in $hit.Ambiguous) { Write-Host ("  [{0}] {1} | {2}" -f $i++, $(if ($n.Text) { $n.Text } else { $n.Desc }), $n.Id) }
    exit $script:ExitNodeNotFound
}
if (-not $hit.Node) {
    Write-Host "No element with $attribute '$value' (index $Index) on screen."
    Write-ScreenList ($hit.Nodes | Where-Object { $_.Clickable })
    exit $script:ExitNodeNotFound
}
if ($hit.Kind -eq 'Contains') { Write-Host "NOTE: matched by contains only (no exact $attribute '$value')." }
$centre = Get-NodeCentre $hit.Node
Write-Host "Found $attribute '$value' at ($($centre.X), $($centre.Y))."
if ($XmlFile) { exit 0 }
Invoke-Adb $phone @('shell', 'input', 'tap', $centre.X, $centre.Y) | Out-Null
$afterNodes = Write-ScreenChange $phone $hit.Nodes
if (-not $afterNodes) {
    Write-Host 'The tap was sent but no dump after it could be read, so the result is not confirmed. The tap is NOT repeated: look at the screen before tapping again.'
    exit $script:ExitTapNotConfirmed
}
if ($ExpectText -and (Get-DistinctTexts $afterNodes) -cnotcontains $ExpectText) {
    Write-Host "Expected text '$ExpectText' is not on screen after the tap."
    exit $script:ExitExpectMissed
}
exit 0
