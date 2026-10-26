# Keeps the display and the PC awake while illustrators draw (create-dino-story step 3). With the screen off, Chrome stops
# rendering: Gemini never finishes loading, pastes do nothing and screenshots time out (2026-10-26). This holds a temporary
# Windows request (SetThreadExecutionState), changes no setting, and the request ends when the script exits.
#   pwsh gemini-keep-awake.ps1 [-Hours 6]    run in the background; stop it early by creating Downloads\.gemini-keep-awake.stop
param([double]$Hours = 6)
$ErrorActionPreference = 'Stop'

Add-Type @'
using System.Runtime.InteropServices;
public static class KeepAwake {
  [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
}
'@

$continuous = [uint32]'0x80000000'
$systemRequired = [uint32]'0x00000001'
$displayRequired = [uint32]'0x00000002'
$stopFile = Join-Path $env:USERPROFILE 'Downloads\.gemini-keep-awake.stop'
$until = (Get-Date).AddHours($Hours)

$started = Get-Date
# A stop file already there before this start is leftover; one written after it is a stop request for this run.
if ((Test-Path -LiteralPath $stopFile) -and (Get-Item -LiteralPath $stopFile).LastWriteTime -lt $started) { Remove-Item -LiteralPath $stopFile -ErrorAction SilentlyContinue }
Write-Host "Keeping the display awake until $($until.ToString('HH:mm'))"
try {
  while ((Get-Date) -lt $until -and -not (Test-Path -LiteralPath $stopFile)) {
    [void][KeepAwake]::SetThreadExecutionState($continuous -bor $systemRequired -bor $displayRequired)
    Start-Sleep -Seconds 30
  }
}
finally {
  [void][KeepAwake]::SetThreadExecutionState($continuous)
  Remove-Item -LiteralPath $stopFile -ErrorAction SilentlyContinue
  Write-Host 'Display may sleep again'
}
