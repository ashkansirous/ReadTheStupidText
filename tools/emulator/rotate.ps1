<#
.SYNOPSIS
  Turns an Android emulator or phone to landscape or portrait with adb (Slice 232). Needs no app test hook.
.DESCRIPTION
  Landscape: remembers the automatic rotation setting, turns it off (accelerometer_rotation 0) and sets user_rotation (0 portrait, 1 landscape). Portrait: sets user_rotation 0 and puts accelerometer_rotation back to the remembered value (1 if unknown).
  Maestro's own `setOrientation` is the first choice inside a flow; this script is for a flow runner or an agent that must turn the
  screen from outside, and for devices where setOrientation does nothing.
  -WhatIf prints the adb commands and runs none. -Serial defaults to the first emulator (emulator-5554).
.EXAMPLE
  pwsh tools/emulator/rotate.ps1 -Serial emulator-5554 -To landscape
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Serial = 'emulator-5554',
    [Parameter(Mandatory)][ValidateSet('landscape', 'portrait')][string]$To
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'activity.ps1') # Get-WorktreesRoot: the saved value lives with the other emulator state, not in the user's temp folder

$UserRotation = @{ portrait = 0; landscape = 1 }
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
$adb = if ($sdk) { Join-Path $sdk 'platform-tools\adb.exe' } else { 'adb' }

function Invoke-Adb([string[]]$AdbArgs) {
    $text = "adb -s $Serial $($AdbArgs -join ' ')"
    if ($PSCmdlet.ShouldProcess($Serial, $text)) {
        & $adb -s $Serial @AdbArgs
        if ($LASTEXITCODE -ne 0) { throw "adb failed ($LASTEXITCODE): $text" }
    }
}

function Get-Setting([string]$Name) {
    $value = (& $adb -s $Serial shell settings get system $Name 2>$null | Out-String).Trim()
    if ($value -match '^\d+$') { return $value }
    return $null
}

# The auto-rotate value from before the first landscape call is kept in a file in <worktrees>\.emulator (per device), so going back to portrait restores it (1 when unknown).
$stateDir = Join-Path (Get-WorktreesRoot) '.emulator'
$saved = Join-Path $stateDir "rotate-accelerometer-$($Serial -replace '[^\w.-]', '_').txt"
if ($To -eq 'landscape') {
    if (-not (Test-Path $saved) -and -not $WhatIfPreference) {
        $before = Get-Setting 'accelerometer_rotation'
        if ($null -ne $before) { $null = New-Item -ItemType Directory -Force $stateDir; Set-Content -Path $saved -Value $before }
    }
    Invoke-Adb @('shell', 'settings', 'put', 'system', 'accelerometer_rotation', '0')
    Invoke-Adb @('shell', 'settings', 'put', 'system', 'user_rotation', "$($UserRotation[$To])")
}
else {
    Invoke-Adb @('shell', 'settings', 'put', 'system', 'user_rotation', "$($UserRotation[$To])")
    $restore = if (Test-Path $saved) { (Get-Content $saved -Raw).Trim() } else { '1' }
    Invoke-Adb @('shell', 'settings', 'put', 'system', 'accelerometer_rotation', $restore)
    if ((Test-Path $saved) -and -not $WhatIfPreference) { Remove-Item $saved }
}
Write-Host "Rotated $Serial to $To."
