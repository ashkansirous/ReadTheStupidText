<#
.SYNOPSIS
  Slice 253 item 2: a contact sheet (one HTML page) with each screenshot a Dino flow took (takeScreenshot steps named shot-<frame>-<screen>) beside the design mock frame it was built against,
  so the "against frame N by eye" checks in docs/plan/unverified.md can be done at a glance.
.DESCRIPTION
  The list of shots comes from screenshot-frames.json (shot name, flow, label, mock file, frame number): add a line there when a flow gets a new takeScreenshot.
  Maestro puts a takeScreenshot PNG in the flow's artifact folder under the Java user.home that run-flows.ps1 sets (<worktrees>\.tools\maestro-home\i<n>\.maestro\tests\...), so the newest
  PNG whose name contains the shot name, written after -Since, is used. The mock side is docs/design/dino-reader/frames/<frame>.png when that file exists (export it from the mock once);
  else the sheet names the mock file and frame to open. The PNGs are embedded, so the sheet is one file you can open or send. Pass -SearchRoot to look somewhere else (the self-test does).
  Measure/produce: run the flows with run-flows.ps1 -App dino -Flow dino-landscape-shelf,dino-landscape-reading,dino-landscape-settings, then
  contact-sheet.ps1 -Since (Get-Date).AddMinutes(-15)   (prints the path of the sheet it wrote).
#>
param(
    [string[]]$SearchRoot,
    [datetime]$Since = [datetime]::MinValue,
    [string]$OutFile,
    [string]$FramesDir
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $FramesDir) { $FramesDir = Join-Path $repoRoot 'docs\design\dino-reader\frames' }

# Pure: the newest PNG under the roots whose name contains $ShotName and that was written at or after $Since; $null when there is none.
function Find-ShotFile([string[]]$Roots, [string]$ShotName, [datetime]$Since) {
    $hits = foreach ($root in $Roots) {
        if (-not (Test-Path $root)) { continue }
        Get-ChildItem -Path $root -Filter '*.png' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*$ShotName*" -and $_.LastWriteTime -ge $Since }
    }
    return @($hits | Sort-Object LastWriteTime -Descending | Select-Object -First 1)[0]
}

function ConvertTo-HtmlText([string]$Text) { return ($Text -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;' -replace '"', '&quot;') }

function ConvertTo-DataUri([string]$Path) { return 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path)) }

# Pure: one table row. $ShotPath / $FramePath may be $null (the row then says what is missing instead of showing a picture).
function New-ContactRow($Entry, [string]$ShotPath, [string]$FramePath) {
    $label = ConvertTo-HtmlText $Entry.label
    $shotCell = if ($ShotPath) { '<img src="' + (ConvertTo-DataUri $ShotPath) + '" alt="device">' } else { '<p class="missing">NO SHOT: the flow ' + (ConvertTo-HtmlText $Entry.flow) + ' did not write ' + (ConvertTo-HtmlText $Entry.shot) + '.png</p>' }
    $frameCell = if ($FramePath) { '<img src="' + (ConvertTo-DataUri $FramePath) + '" alt="mock">' } else { '<p class="missing">Open ' + (ConvertTo-HtmlText $Entry.mock) + ', frame ' + (ConvertTo-HtmlText $Entry.frame) + '.</p>' }
    return "<tr><th>$label<br><small>frame $(ConvertTo-HtmlText $Entry.frame)</small></th><td>$shotCell</td><td>$frameCell</td></tr>"
}

function New-ContactSheetHtml([string[]]$Rows) {
    $head = '<!doctype html><meta charset="utf-8"><title>Dino contact sheet</title><style>body{font:14px sans-serif}table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:6px;vertical-align:top}img{max-width:560px}.missing{color:#b00}</style>'
    return $head + '<h1>Device shot beside mock frame</h1><table><tr><th>Screen</th><th>Device</th><th>Mock</th></tr>' + ($Rows -join '') + '</table>'
}

# Pure: the folder that holds maestro-home and maestro-out, as run-flows.ps1 derives it (the parent of the Maestro folder): MAESTRO_HOME's parent when set, else <worktrees>\.tools.
function Get-MaestroToolsDir([string]$RepoRoot, [string]$MaestroHome) {
    if ($MaestroHome) { return Split-Path $MaestroHome -Parent }
    $parent = Split-Path $RepoRoot -Parent
    if ((Split-Path $parent -Leaf) -notlike '*-worktrees') { $parent = Join-Path $parent ((Split-Path $RepoRoot -Leaf) + '-worktrees') }
    return Join-Path $parent '.tools'
}

# Pure: the rows for every manifest entry. $Roots are searched for the shots; $Frames is the folder of exported mock frames.
function Get-ContactRows($Manifest, [string[]]$Roots, [datetime]$Since, [string]$Frames) {
    foreach ($entry in @($Manifest)) {
        $shot = Find-ShotFile $Roots $entry.shot $Since
        $framePath = Join-Path $Frames "$($entry.frame).png"
        New-ContactRow $entry $(if ($shot) { $shot.FullName } else { $null }) $(if (Test-Path $framePath) { $framePath } else { $null })
    }
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by the self-test: functions only
if (-not $SearchRoot) {
    $toolsDir = Get-MaestroToolsDir $repoRoot $env:MAESTRO_HOME
    $SearchRoot = @((Join-Path $toolsDir 'maestro-home'), (Join-Path $toolsDir 'maestro-out'))
}
if (-not $OutFile) { $OutFile = Join-Path $repoRoot 'tmp\contact-sheet.html' }
$manifest = Get-Content (Join-Path $PSScriptRoot 'screenshot-frames.json') -Raw | ConvertFrom-Json
$rows = @(Get-ContactRows $manifest $SearchRoot $Since $FramesDir)
$null = New-Item -ItemType Directory -Force (Split-Path $OutFile -Parent)
Set-Content -Path $OutFile -Value (New-ContactSheetHtml $rows) -Encoding UTF8
$missing = @($rows | Where-Object { $_ -like '*NO SHOT*' }).Count
Write-Host "CONTACT SHEET: $OutFile ($(@($manifest).Count) screens, $missing without a device shot)."
if ($missing -gt 0) { Write-Host "Searched: $($SearchRoot -join ', ') (pass -SearchRoot if the Maestro folder is elsewhere)."; exit 1 }
