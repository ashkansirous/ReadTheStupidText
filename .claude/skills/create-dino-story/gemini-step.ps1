# One call per shell part of a Gemini picture (create-dino-story step 6). Each verb folds several existing scripts into one tool call,
# so an illustrator agent spends fewer steps per picture (every step resends its whole context). All existing scripts keep working.
#   pwsh gemini-step.ps1 prepare -Owner <owner> -Picture <line-up.jpg>[,<place.jpg>]
#       usage check (stops at 99%), lock Take, clipboard Put (two files: PutFiles, one ctrl+v for both). Run it only AFTER the
#       new chat, loader, health check and model check passed (the lock is held from here: one clipboard writer). Then ctrl+v in your
#       own tab and `await __waitAttached(<n>)` in the SAME next step (the lock goes stale 180 s after Take, and waitAttached gives up after 15 s,
#       so one browser batch right after prepare is covered; if more than 2 minutes pass, run `touch` first). The lock is HELD: run `release` as soon as that returns, and on ANY failure or stop.
#   pwsh gemini-step.ps1 touch -Owner <owner>   refreshes the held lock's timestamp only (the clipboard is left alone, so a two-picture file list
#       survives). Refuses unless <owner> still holds the lock (a stale takeover means: release, start again from prepare).
#   pwsh gemini-step.ps1 put -Owner <owner> -Picture <file>   second picture only: when one paste attached just one of two. Refuses
#       unless <owner> still holds the lock (a stale takeover means: release, start again from prepare).
#   pwsh gemini-step.ps1 release -Owner <owner>
#   pwsh gemini-step.ps1 save -Owner <owner> -Path <out folder>/cand-<page>.jpg -Bytes <n>[,<n>]
#       -Bytes is REQUIRED (positive; the sizes `__drawPicture`/`__grab` printed). Lock Take, gemini-take-download.ps1 -Untouched (only a
#       download of exactly that size from the last 120 s; file moved byte for byte, 896x1200 small copy made beside it), lock Release,
#       line70.ps1 on it; prints ONE line: SAVED <path> <WxH> <bytes> | small <path> | line70 <overlay png>
#       Both save and collect take -UserAskedRedo (only when the user named that page; an accepted page is refused without it) and
#       -BackupFolder <working folder>/previous (where a replaced picture is copied first).
#   pwsh gemini-step.ps1 collect -Owner <owner> -Path <out folder>/<prefix> -Bytes <n>,<n>,...
#       recovery: -Bytes is ONE size per picture, oldest first (what `await __recoverChat()` printed; 0 = Gemini gave no blob). Saves
#       each as <prefix>-1, <prefix>-2 ... untouched (+ small copies), skips a name already saved (ALREADY SAVED, not MISSING), and
#       prints one line per picture, MISSING for a download that never arrived (Chrome dropped it).
# Exit codes: 0 done; 1 a step failed (the line says which); 2 saved but the small copy failed (never download again).
param(
  [Parameter(Mandatory = $true, Position = 0)][ValidateSet('prepare', 'put', 'touch', 'release', 'save', 'collect')][string]$Verb,
  [string]$Owner,
  [string]$Picture,   # one file, or two joined by a comma (pwsh -File passes a comma list as one string)
  [string]$Path,
  [string]$Bytes = '',   # sizes joined by a comma, parsed here (pwsh -File would read "123,456" as the number 123456)
  [switch]$UserAskedRedo,   # save/collect only, Slice 447: pass only when the user named this page (an accepted page is refused without it)
  [string]$BackupFolder = ''   # save/collect only: where a replaced picture is copied, normally <working folder>/previous
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$usageCheck = Join-Path $HOME '.claude/skills/usage-check/usage-check.mjs'
$usageStopAt = '99'
$collectFreshSeconds = 3600
$pictures = @($Picture -split ',' | Where-Object { $_ })

# Each -Bytes entry must be a whole number (not below zero; `save` also needs above zero). A bad entry gives $null, so the verb stops before the lock.
function Read-Sizes([string]$text) {
  $list = @()
  foreach ($part in @($text -split ',' | Where-Object { $_ })) {
    $value = 0L
    if (-not [long]::TryParse($part.Trim(), [ref]$value) -or $value -lt 0) { return $null }
    $list += $value
  }
  , [long[]]$list
}
$sizes = Read-Sizes $Bytes

function Invoke-Script([string]$script, [string[]]$arguments, [switch]$Sta) {
  $flags = @('-NoProfile'); if ($Sta) { $flags += '-STA' }
  $output = & pwsh @flags -File (Join-Path $here $script) @arguments 2>&1
  [pscustomobject]@{ Code = $LASTEXITCODE; Text = (($output | ForEach-Object { "$_" }) -join "`n") }
}

# gemini-take-download.ps1 runs in this process (typed arguments; its `exit 2` ends only that script); Write-Host output is captured.
function Invoke-Take([string]$target, [long[]]$wanted, [int]$fresh) {
  $arguments = @{ Path = $target; Untouched = $true; FreshSeconds = $fresh }
  if ($wanted.Count) { $arguments.Bytes = $wanted }
  if ($UserAskedRedo) { $arguments.UserAskedRedo = $true }
  if ($BackupFolder) { $arguments.BackupFolder = $BackupFolder }
  $global:LASTEXITCODE = 0
  $output = try { & (Join-Path $here 'gemini-take-download.ps1') @arguments 6>&1 2>&1 } catch { "ERROR: $($_.Exception.Message)" }
  [pscustomobject]@{ Code = $global:LASTEXITCODE; Text = (($output | ForEach-Object { "$_" }) -join "`n") }
}

function Stop-Step([string]$message, [int]$code = 1) { Write-Host $message; exit $code }

function Assert-Owner { if (-not $Owner) { Stop-Step 'FAILED: -Owner is required' } }

function Test-Usage {
  $result = & node $usageCheck --stop-at $usageStopAt 2>&1
  if ($LASTEXITCODE -ne 0) { Stop-Step "STOP: usage check says no ($(($result | ForEach-Object { "$_" }) -join ' '))" }
}

function Invoke-Prepare {
  Assert-Owner
  if (-not $pictures.Count) { Stop-Step 'FAILED: -Picture is required' }
  Test-Usage
  $lock = Invoke-Script 'gemini-lock.ps1' @('Take', $Owner)
  if ($lock.Code -ne 0) { Stop-Step "FAILED: lock Take: $($lock.Text)" }
  $action = if ($pictures.Count -gt 1) { 'PutFiles' } else { 'Put' }
  $put = $null
  try { $put = Invoke-Script 'gemini-clipboard.ps1' (@($action, ($pictures -join ','))) -Sta }
  catch { $put = [pscustomobject]@{ Code = 1; Text = $_.Exception.Message } }
  if ($put.Code -ne 0) { Invoke-Script 'gemini-lock.ps1' @('Release', $Owner) | Out-Null; Stop-Step "FAILED: clipboard: $($put.Text)" }
  Write-Host "PREPARED lock held by $Owner; $action x $($pictures.Count); now ctrl+v, await __waitAttached($($pictures.Count)), then release (also on any failure)"
}

function Invoke-Put {
  Assert-Owner
  if (-not $pictures.Count) { Stop-Step 'FAILED: -Picture is required' }
  $touch = Invoke-Script 'gemini-lock.ps1' @('Touch', $Owner)
  if ($touch.Code -ne 0) { Stop-Step "FAILED: $Owner does not hold the lock, nothing put on the clipboard: $($touch.Text -replace '\s+', ' ')" }
  $put = Invoke-Script 'gemini-clipboard.ps1' @('Put', $pictures[0]) -Sta
  Stop-Step "PUT $($pictures[0]) (exit $($put.Code))" $put.Code
}

function Invoke-Touch {
  Assert-Owner
  $touch = Invoke-Script 'gemini-lock.ps1' @('Touch', $Owner)
  if ($touch.Code -ne 0) { Stop-Step "FAILED: $Owner does not hold the lock: $($touch.Text -replace '\s+', ' ')" }
  Stop-Step "TOUCHED $Owner" 0
}

function Invoke-Release {
  Assert-Owner
  $release = Invoke-Script 'gemini-lock.ps1' @('Release', $Owner)
  Stop-Step "RELEASED $Owner (exit $($release.Code))" $release.Code
}

function Read-SavedLine([string]$text) {
  $saved = [regex]::Match($text, 'Saved (?<path>.+?) \((?<dims>\d+x\d+), (?<bytes>\d+) bytes')
  $small = [regex]::Match($text, 'Small copy (?<path>.+?) \(look')
  if (-not $saved.Success) { return $null }
  [pscustomobject]@{ Path = $saved.Groups['path'].Value; Dims = $saved.Groups['dims'].Value; Bytes = $saved.Groups['bytes'].Value; Small = $small.Groups['path'].Value }
}

function Get-OverlayPath([string]$picture) {
  $result = Invoke-Script 'line70.ps1' @($picture)
  $png = $result.Text -split "`n" | Where-Object { $_ -like '*.png' } | Select-Object -Last 1
  if ($result.Code -ne 0 -or -not $png) { return "FAILED($($result.Text -replace '\s+', ' '))" }
  $png.Trim()
}

function Invoke-Save {
  Assert-Owner
  if (-not $Path) { Stop-Step 'FAILED: -Path is required' }
  if ($null -eq $sizes -or -not $sizes.Count -or @($sizes | Where-Object { $_ -le 0 }).Count) { Stop-Step 'FAILED: -Bytes (positive size(s) printed by __grab) is required; without it another illustrator''s download could be taken' }
  $lock = Invoke-Script 'gemini-lock.ps1' @('Take', $Owner)
  if ($lock.Code -ne 0) { Stop-Step "FAILED: lock Take: $($lock.Text)" }
  try { $take = Invoke-Take $Path $sizes 120 }
  finally { Invoke-Script 'gemini-lock.ps1' @('Release', $Owner) | Out-Null }
  $saved = Read-SavedLine $take.Text
  if (-not $saved) { Stop-Step "FAILED: no picture saved: $($take.Text -replace '\s+', ' ')" }
  $warning = if ($take.Text -match 'WARNING: [^\n]*') { " | $($Matches[0])" } else { '' }
  Stop-Step "SAVED $($saved.Path) $($saved.Dims) $($saved.Bytes) bytes | small $($saved.Small) | line70 $(Get-OverlayPath $saved.Path)$warning" $(if ($take.Code -eq 2) { 2 } else { 0 })
}

# An untouched save keeps the download's extension, so "already saved" is any <base>.jpg/.jpeg/.png/.webp (never the .small.jpg copy).
function Find-SavedPicture([string]$base) {
  foreach ($extension in @('jpg', 'jpeg', 'png', 'webp')) {
    if (Test-Path -LiteralPath "$base.$extension") { return "$base.$extension" }
  }
  $null
}

function Invoke-Collect {
  Assert-Owner
  if (-not $Path -or $null -eq $sizes -or -not $sizes.Count) { Stop-Step 'FAILED: -Path (folder/prefix) and -Bytes (whole numbers, comma-separated) are required' }
  $lock = Invoke-Script 'gemini-lock.ps1' @('Take', $Owner)
  if ($lock.Code -ne 0) { Stop-Step "FAILED: lock Take: $($lock.Text)" }
  $missing = 0
  try {
    for ($i = 0; $i -lt $sizes.Count; $i++) {
      $target = "$Path-$($i + 1).jpg"
      $existing = if ($UserAskedRedo) { $null } else { Find-SavedPicture "$Path-$($i + 1)" }   # a named redo replaces the page, so it is never "already saved"
      if ($existing) { Write-Host "$($i + 1): ALREADY SAVED $existing"; continue }
      if ($sizes[$i] -le 0) { $missing++; Write-Host "$($i + 1): MISSING (Gemini gave no blob for this picture)"; continue }
      $take = Invoke-Take $target @($sizes[$i]) $collectFreshSeconds
      $saved = Read-SavedLine $take.Text
      if ($saved) { Write-Host "$($i + 1): SAVED $($saved.Path) $($saved.Dims) $($saved.Bytes) bytes | small $($saved.Small)" }
      else { $missing++; Write-Host "$($i + 1): MISSING $($sizes[$i]) bytes ($($take.Text -replace '\s+', ' '))" }
    }
  }
  finally { Invoke-Script 'gemini-lock.ps1' @('Release', $Owner) | Out-Null }
  Stop-Step "COLLECTED $($sizes.Count - $missing) of $($sizes.Count)" $(if ($missing) { 1 } else { 0 })
}

switch ($Verb) {
  'prepare' { Invoke-Prepare }
  'put' { Invoke-Put }
  'touch' { Invoke-Touch }
  'release' { Invoke-Release }
  'save' { Invoke-Save }
  'collect' { Invoke-Collect }
}
