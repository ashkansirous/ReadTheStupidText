# Lets two illustrators share one Chrome, one clipboard and one Downloads folder (create-dino-story steps 3 and 6).
# Hold the lock only around the two shared moments, never while Gemini draws:
#   pwsh gemini-lock.ps1 Take <owner>      before `gemini-clipboard.ps1 Put` + ctrl+v, or before `__grab` + gemini-take-download.ps1
#   pwsh gemini-lock.ps1 Release <owner>   right after the paste shows its thumbnail, or right after the picture is saved
# Take waits up to 3 minutes; a lock older than 2 minutes is stale (its holder died) and is taken over.
param(
  [Parameter(Mandatory = $true)][ValidateSet('Take', 'Release')][string]$Action,
  [Parameter(Mandatory = $true)][string]$Owner
)
$ErrorActionPreference = 'Stop'

$lockFile = Join-Path $env:USERPROFILE 'Downloads\.gemini-illustrator.lock'
$staleSeconds = 120
$waitSeconds = 180

function Remove-StaleLock {
  $lock = Get-Item -LiteralPath $lockFile -ErrorAction SilentlyContinue
  if ($lock -and $lock.LastWriteTime -lt (Get-Date).AddSeconds(-$staleSeconds)) {
    Write-Host "Taking over a stale lock held by $(Get-Content -LiteralPath $lockFile -Raw)"
    Remove-Item -LiteralPath $lockFile -Force
  }
}

function Request-Lock {
  for ($i = 0; $i -lt $waitSeconds; $i++) {
    Remove-StaleLock
    try {
      $stream = [IO.File]::Open($lockFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
      $writer = [IO.StreamWriter]::new($stream)
      $writer.Write("$Owner $(Get-Date -Format o)")
      $writer.Dispose()
      Write-Host "Lock taken by $Owner"
      return
    }
    catch [IO.IOException] { Start-Sleep -Seconds 1 }
  }
  throw "Lock still held after $waitSeconds s by $(Get-Content -LiteralPath $lockFile -Raw)"
}

function Remove-Lock {
  if (-not (Test-Path -LiteralPath $lockFile)) { Write-Host 'No lock to release'; return }
  $holder = (Get-Content -LiteralPath $lockFile -Raw).Split(' ')[0]
  if ($holder -ne $Owner) { throw "The lock belongs to $holder, not $Owner" }
  Remove-Item -LiteralPath $lockFile -Force
  Write-Host "Lock released by $Owner"
}

if ($Action -eq 'Take') { Request-Lock } else { Remove-Lock }
