# Lets two illustrators share one Chrome, one clipboard and one Downloads folder (create-dino-story steps 3 and 6).
# Hold the lock only around the two shared moments, never while Gemini draws:
#   pwsh gemini-lock.ps1 Take <owner>      before `gemini-clipboard.ps1 Put` + ctrl+v, or before `__grab` + gemini-take-download.ps1
#   pwsh gemini-lock.ps1 Touch <owner>     optional, while holding it for a long step: refreshes the timestamp so it is not taken over
#   pwsh gemini-lock.ps1 Release <owner>   right after the paste shows its thumbnail, or right after the picture is saved
# Take waits up to 4 minutes; a lock not touched for 3 minutes is stale (its holder died) and is taken over: the stale file
# is renamed to a unique name first, and only the process whose rename succeeds may then create the new lock.
# Release only deletes a lock this owner holds. Spaces in <owner> become underscores.
param(
  [Parameter(Mandatory = $true)][ValidateSet('Take', 'Release', 'Touch')][string]$Action,
  [Parameter(Mandatory = $true)][string]$Owner
)
$ErrorActionPreference = 'Stop'

$lockFile = Join-Path $env:USERPROFILE 'Downloads\.gemini-illustrator.lock'
$staleSeconds = 180
$waitSeconds = 240
$Owner = ($Owner -replace '\s', '_')

function Read-Holder {
  try {
    $text = Get-Content -LiteralPath $lockFile -Raw -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($text)) { return '' }
    return $text.Trim().Split(' ')[0]
  }
  catch { return '' }
}

function Test-LockStale {
  try {
    $lock = Get-Item -LiteralPath $lockFile -ErrorAction Stop
    return $lock.LastWriteTime -lt (Get-Date).AddSeconds(-$staleSeconds)
  }
  catch { return $false }
}

function Remove-StaleLock {
  if (-not (Test-LockStale)) { return }
  $holder = Read-Holder
  $aside = "$lockFile.stale-$([guid]::NewGuid().ToString('N'))"
  try { Move-Item -LiteralPath $lockFile -Destination $aside -ErrorAction Stop }
  catch { return }
  $moved = Get-Item -LiteralPath $aside
  if ($moved.LastWriteTime -ge (Get-Date).AddSeconds(-$staleSeconds)) {
    # Another waiter had already replaced the stale lock with a live one: put it back if the name is still free.
    # If that fails it may be the only copy of a live lock, so it is never deleted: it stays at $aside.
    try { Move-Item -LiteralPath $aside -Destination $lockFile -ErrorAction Stop }
    catch { Write-Host "WARNING: could not put a live lock back; it is kept at $aside" }
    return
  }
  Write-Host "Taking over a stale lock held by $holder"
  Remove-Item -LiteralPath $aside -Force -ErrorAction SilentlyContinue
}

function New-LockFile {
  $stream = [IO.File]::Open($lockFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
  $writer = [IO.StreamWriter]::new($stream)
  try { $writer.Write("$Owner $(Get-Date -Format o)") } finally { $writer.Dispose() }
}

function Request-Lock {
  for ($i = 0; $i -lt $waitSeconds; $i++) {
    Remove-StaleLock
    try {
      New-LockFile
      Write-Host "Lock taken by $Owner"
      return
    }
    catch [IO.IOException], [UnauthorizedAccessException] { Start-Sleep -Seconds 1 }
  }
  throw "Lock still held after $waitSeconds s by $(Read-Holder)"
}

function Assert-Holder {
  if (-not (Test-Path -LiteralPath $lockFile)) { return $false }
  $holder = Read-Holder
  if ($holder -ne $Owner) { throw "The lock belongs to '$holder', not $Owner" }
  return $true
}

function Remove-Lock {
  if (-not (Assert-Holder)) { Write-Host 'No lock to release'; return }
  Remove-Item -LiteralPath $lockFile -Force
  Write-Host "Lock released by $Owner"
}

function Update-Lock {
  if (-not (Assert-Holder)) { throw "No lock to touch: $Owner no longer holds it (it was taken over as stale); Take it again." }
  (Get-Item -LiteralPath $lockFile).LastWriteTime = Get-Date
  Write-Host "Lock refreshed by $Owner"
}

switch ($Action) {
  'Take' { Request-Lock }
  'Touch' { Update-Lock }
  'Release' { Remove-Lock }
}
