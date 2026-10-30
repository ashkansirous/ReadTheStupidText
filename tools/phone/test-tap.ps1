<#
.SYNOPSIS
  Self-test for the uiautomator dump parsing (common.ps1) and for tap.ps1 run against a saved dump (-XmlFile): node parse, entity decoding,
  exact-before-contains matching, -Index, bounds centre and the not-found exit code 3. Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = $env:TEMP }
. (Join-Path $PSScriptRoot 'common.ps1')

$xml = @'
<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation="0">
<node index="0" package="uk.sirous.readthestupidtext.dbg" text="" resource-id="" class="android.widget.FrameLayout" clickable="false" bounds="[0,0][1080,2400]">
<node index="0" package="uk.sirous.readthestupidtext.dbg" text="Settings" resource-id="uk.sirous.readthestupidtext.dbg:id/title" class="android.widget.TextView" clickable="false" content-desc="" bounds="[40,100][400,180]" />
<node index="1" package="uk.sirous.readthestupidtext.dbg" text="Norsk" resource-id="uk.sirous.readthestupidtext.dbg:id/lang_row" class="android.widget.TextView" clickable="true" content-desc="" bounds="[0,400][1080,520]" />
<node index="2" package="uk.sirous.readthestupidtext.dbg" text="Norsk bokmål (more)" resource-id="" class="android.widget.TextView" clickable="true" content-desc="" bounds="[0,600][1080,720]" />
<node index="3" package="uk.sirous.readthestupidtext.dbg" text="Tom &amp; Jerry &quot;club&quot;" resource-id="" class="android.widget.TextView" clickable="true" content-desc="Open club" bounds="[100,800][300,900]" />
<node index="4" package="uk.sirous.readthestupidtext.dbg" text="norsk" resource-id="" class="android.widget.TextView" clickable="true" content-desc="" bounds="[0,1000][1080,1100]" />
<node index="5" package="uk.sirous.readthestupidtext.dbg" text="" resource-id="" class="android.view.View" clickable="false" content-desc="Back" bounds="[0,0][100,100]" />
<node index="6" package="uk.sirous.readthestupidtext.dbg" text="Sound" resource-id="id/sw" class="android.widget.Switch" clickable="true" checked="true" selected="false" bounds="[0,1200][1080,1300]" />
</node>
<node index="1" package="com.android.systemui" text="12:34" resource-id="com.android.systemui:id/clock" class="android.widget.TextView" clickable="false" content-desc="" bounds="[0,0][200,60]" />
</hierarchy>
'@

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$nodes = @(Read-UiNodes $xml)
Assert-Equal 'node count' 9 $nodes.Count
Assert-Equal 'package parsed' 'uk.sirous.readthestupidtext.dbg' $nodes[1].Package
Assert-Equal 'entities decoded' 'Tom & Jerry "club"' $nodes[4].Text
Assert-Equal 'clickable parsed' $true $nodes[2].Clickable
Assert-Equal 'content-desc parsed' 'Open club' $nodes[4].Desc

Assert-Equal 'checked parsed' $true $nodes[7].Checked
Assert-Equal 'unchecked parsed' $false $nodes[2].Checked

# Did the screen change: a switch flipping or a row going away counts, not only new texts.
$flipped = @(Read-UiNodes ($xml -replace 'checked="true"', 'checked="false"'))
Assert-Equal 'same screen: no change' $false (Compare-Screens $nodes $nodes).Changed
Assert-Equal 'toggle flip is a change' $true (Compare-Screens $nodes $flipped).Changed
# The status bar is not the app: a clock tick or a notification is no change.
$clockTick = @(Read-UiNodes ($xml -replace '12:34', '12:35'))
Assert-Equal 'status-bar clock tick is no change' $false (Compare-Screens $nodes $clockTick).Changed
Assert-Equal 'status-bar nodes are not in the signature' 0 @(Get-ScreenSignature $nodes | Where-Object { $_ -match '12:34' }).Count
$fewer = @($nodes | Select-Object -First 3)
$diff = Compare-Screens $nodes $fewer
Assert-Equal 'subset screen is a change (nodes removed)' $true $diff.Changed
Assert-Equal 'nothing added on a subset' 0 $diff.Added.Count
Assert-Equal 'dump ok' $true (Test-UiDumpOk @('UI hierarchy dumped to: /sdcard/window.xml'))
Assert-Equal 'dump failed' $false (Test-UiDumpOk @('ERROR: could not get idle state.'))

$exact = @(Find-UiNodes $nodes 'Text' 'Norsk')
Assert-Equal 'exact match wins (contains is not used)' 1 $exact.Count
Assert-Equal 'exact match is the row' 'uk.sirous.readthestupidtext.dbg:id/lang_row' $exact[0].Id
$contains = @(Find-UiNodes $nodes 'Text' 'NORSK')
Assert-Equal 'case-insensitive contains finds three' 3 $contains.Count
Assert-Equal 'contains keeps screen order' 'Norsk' $contains[0].Text
Assert-Equal 'id exact' 1 @(Find-UiNodes $nodes 'Id' 'uk.sirous.readthestupidtext.dbg:id/title').Count
Assert-Equal 'desc contains' 1 @(Find-UiNodes $nodes 'Desc' 'club').Count
Assert-Equal 'not found' 0 @(Find-UiNodes $nodes 'Text' 'Svenska').Count

Assert-Equal 'match kind exact' 'Exact' (Resolve-UiMatch $nodes 'Text' 'Norsk').Kind
Assert-Equal 'match kind contains' 'Contains' (Resolve-UiMatch $nodes 'Text' 'NORSK').Kind
Assert-Equal 'match kind none' 'None' (Resolve-UiMatch $nodes 'Text' 'Svenska').Kind

# Foreground package: the expected app must be on screen (SystemUI ignored); someone else's app or an empty dump is not.
$dbg = 'uk.sirous.readthestupidtext.dbg'
Assert-Equal 'foreground match' 'Match' (Resolve-ForegroundPackage $nodes $dbg)
Assert-Equal 'foreground is another app' 'Mismatch' (Resolve-ForegroundPackage $nodes 'uk.sirous.dinoreader.dbg')
Assert-Equal 'foreground store app' 'Mismatch' (Resolve-ForegroundPackage @(Read-UiNodes ($xml -replace 'readthestupidtext\.dbg" text', 'readthestupidtext" text')) $dbg)
Assert-Equal 'foreground: only SystemUI is Unknown' 'Unknown' (Resolve-ForegroundPackage @($nodes | Where-Object { $_.Package -eq 'com.android.systemui' }) $dbg)
Assert-Equal 'foreground: no package in the dump is Unknown' 'Unknown' (Resolve-ForegroundPackage @(Read-UiNodes ($xml -replace ' package="[^"]*"', '')) $dbg)

$centre = Get-NodeCentre $exact[0]
Assert-Equal 'centre x' 540 $centre.X
Assert-Equal 'centre y' 460 $centre.Y

# tap.ps1 itself against the saved dump, in a child of the same PowerShell edition.
$dump = Join-Path ([IO.Path]::GetTempPath()) ('rtst-tap-test-' + [guid]::NewGuid().ToString('N') + '.xml')
$engine = (Get-Process -Id $PID).Path
try {
    Set-Content $dump $xml -Encoding UTF8
    $tap = Join-Path $PSScriptRoot 'tap.ps1'
    $out = & $engine -NoProfile -File $tap -Text 'Norsk' -XmlFile $dump
    Assert-Equal 'tap finds the node: exit 0' 0 $LASTEXITCODE
    Assert-Equal 'tap prints the centre' $true (($out -join ' ') -match '\(540, 460\)')
    $out = & $engine -NoProfile -File $tap -Text 'NORSK' -Index 2 -XmlFile $dump
    Assert-Equal 'tap -Index 2 of the contains list: exit 0' 0 $LASTEXITCODE
    $out = & $engine -NoProfile -File $tap -Text 'NORSK' -XmlFile $dump
    Assert-Equal 'tap contains with several hits and no -Index: exit 3' 3 $LASTEXITCODE
    Assert-Equal 'tap lists the ambiguous hits' $true (($out -join ' ') -match '\[2\] norsk')
    $out = & $engine -NoProfile -File $tap -Text 'ound' -XmlFile $dump
    Assert-Equal 'tap a single contains hit: exit 0' 0 $LASTEXITCODE
    $out = & $engine -NoProfile -File $tap -Text 'Norsk' -App dino -XmlFile $dump
    Assert-Equal 'tap with another app expected: exit 6' 6 $LASTEXITCODE
    $ErrorActionPreference = 'Continue'  # the refusal is written to stderr; Windows PowerShell 5.1 turns that into an error under Stop
    $out = & $engine -NoProfile -File $tap -Text 'Norsk' -Package 'com.android.settings' -XmlFile $dump 2>&1
    $ErrorActionPreference = 'Stop'
    Assert-Equal 'tap refuses a package outside the dev allow-list' $true ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 6)
    $out = & $engine -NoProfile -File $tap -Text 'Svenska' -XmlFile $dump
    Assert-Equal 'tap not found: exit 3' 3 $LASTEXITCODE
    Assert-Equal 'tap lists the clickable texts' $true (($out -join ' ') -match 'Norsk bokm')
} finally {
    Remove-Item $dump -Force -ErrorAction SilentlyContinue
}

if ($failed) { Write-Host "$failed tap checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All tap checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
