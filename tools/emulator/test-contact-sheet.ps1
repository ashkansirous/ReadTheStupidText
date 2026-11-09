<#
.SYNOPSIS
  Self-test for contact-sheet.ps1 (Slice 253 item 2) and for the takeScreenshot steps it relies on. Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'contact-sheet.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

Assert-Equal 'tools dir: MAESTRO_HOME parent wins' 'D:\x\.tools' (Get-MaestroToolsDir 'D:\r\repo' 'D:\x\.tools\maestro')
Assert-Equal 'tools dir: a worktree uses its worktrees folder' 'D:\r\repo-worktrees\.tools' (Get-MaestroToolsDir 'D:\r\repo-worktrees\s1' '')
Assert-Equal 'tools dir: the primary uses <repo>-worktrees' 'D:\r\repo-worktrees\.tools' (Get-MaestroToolsDir 'D:\r\repo' '')

$work = Join-Path ([IO.Path]::GetTempPath()) ("rtst-sheet-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force "$work\tests\run1\takeScreenshot", "$work\frames"
    $png = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
    [IO.File]::WriteAllBytes("$work\tests\run1\takeScreenshot\shot-KL1-shelf.png", $png)
    $old = "$work\tests\run1\takeScreenshot\shot-L1-corner.png"
    [IO.File]::WriteAllBytes($old, $png)
    (Get-Item $old).LastWriteTime = (Get-Date).AddHours(-5)
    [IO.File]::WriteAllBytes("$work\frames\KL1.png", $png)

    Assert-Equal 'find: a shot is found by its name' 'shot-KL1-shelf.png' (Find-ShotFile @("$work\tests") 'shot-KL1-shelf' ([datetime]::MinValue)).Name
    Assert-Equal 'find: a shot older than -Since is not used' $null (Find-ShotFile @("$work\tests") 'shot-L1-corner' (Get-Date).AddHours(-1))
    Assert-Equal 'find: a missing root is skipped' $null (Find-ShotFile @("$work\nothing") 'shot-KL1-shelf' ([datetime]::MinValue))

    $manifest = Get-Content (Join-Path $PSScriptRoot 'screenshot-frames.json') -Raw | ConvertFrom-Json
    $rows = @(Get-ContactRows $manifest @("$work\tests") (Get-Date).AddHours(-1) "$work\frames")
    Assert-Equal 'sheet: one row per manifest entry' @($manifest).Count $rows.Count
    Assert-Equal 'sheet: a found shot is embedded' $true ($rows[0] -like '*data:image/png;base64,*' -and $rows[0] -notlike '*NO SHOT*')
    Assert-Equal 'sheet: a mock frame image is embedded beside it' 2 ([regex]::Matches($rows[0], 'data:image/png').Count)
    Assert-Equal 'sheet: a missing shot says which flow did not write it' $true ($rows[1] -like '*NO SHOT: the flow dino-landscape-shelf did not write shot-L1-corner.png*')
    Assert-Equal 'sheet: a missing mock image names the file and frame to open' $true ($rows[2] -like '*Dino Reader Batch 44 Landscape.dc.html, frame 113*')
    Assert-Equal 'sheet: html wraps the rows' $true ((New-ContactSheetHtml $rows) -like '<!doctype html>*</table>')
    Assert-Equal 'html: text is escaped' '&lt;b&gt; &amp; &quot;x&quot;' (ConvertTo-HtmlText '<b> & "x"')
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

# Every manifest shot is taken by the flow it names, and every takeScreenshot in the Dino flows is in the manifest (a shot nobody lays out is dead weight).
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$manifest = Get-Content (Join-Path $PSScriptRoot 'screenshot-frames.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $flowFile = Join-Path $repoRoot ".maestro\dino\$($entry.flow).yaml"
    $flowText = if (Test-Path $flowFile) { Get-Content $flowFile -Raw } else { '' }
    Assert-Equal "flow $($entry.flow) takes $($entry.shot)" $true ($flowText -match ('(?m)^- takeScreenshot:\s*' + [regex]::Escape($entry.shot) + '\s*$'))
}
$taken = foreach ($file in Get-ChildItem (Join-Path $repoRoot '.maestro\dino') -Filter '*.yaml' -File) {
    foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw), '(?m)^\s*- takeScreenshot:\s*(\S+)\s*$')) { $m.Groups[1].Value }
}
foreach ($name in @($taken)) { Assert-Equal "manifest lists $name" $true (@($manifest | Where-Object { $_.shot -eq $name }).Count -eq 1) }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-contact-sheet: all passed.'
exit 0
