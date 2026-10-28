# Shared helpers for the real-phone scripts (Slice 165). Dot-sourced, not run. Wireless adb only, never USB.
$script:SdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
if (-not $script:SdkRoot) { throw 'ANDROID_HOME (or ANDROID_SDK_ROOT) is not set.' }
$script:Adb = Join-Path $script:SdkRoot 'platform-tools\adb.exe'
$script:RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
# Wireless debugging shows up as `adb-<serial>-<suffix>._adb-tls-connect._tcp` or `<ip>:<port>`; emulators and USB serials never do.
$script:WirelessPattern = '^(adb-.+_adb-tls-connect\._tcp|(?!127\.)\d{1,3}(\.\d{1,3}){3}:\d+)$'

function Get-AdbDevices {
    $ErrorActionPreference = 'Continue'
    & $script:Adb mdns services 2>&1 | Out-Null
    $lines = & $script:Adb devices 2>&1
    $lines | Where-Object { $_ -match '^(\S+)\s+(device|offline|unauthorized)\s*$' } |
        ForEach-Object { [pscustomobject]@{ Serial = $Matches[1]; State = $Matches[2] } }
}

# The wireless phone that is ready, or $null. With -Serial, only that exact serial counts.
function Find-WirelessPhone([string]$Serial) {
    $devices = @(Get-AdbDevices | Where-Object { $_.State -eq 'device' -and $_.Serial -match $script:WirelessPattern })
    if ($Serial) { $devices = @($devices | Where-Object { $_.Serial -eq $Serial }) }
    return $devices | Select-Object -First 1 -ExpandProperty Serial
}

function Write-PairingAsk {
    Write-Host 'The phone is not connected over wireless debugging. I need two things from you:'
    Write-Host '  1. On the phone: Settings > Developer options > Wireless debugging > Pair device with pairing code.'
    Write-Host '  2. Tell me the pairing IP:port and the 6-digit code shown there, then the IP:port on the main Wireless debugging screen.'
    Write-Host 'Then run: connect.ps1 -PairAddress <ip:port> -PairCode <6 digits> -ConnectAddress <ip:port>'
    Write-Host '(Wireless debugging only. USB is never used.)'
}

function Invoke-Adb([string]$Serial, [string[]]$Arguments) {
    $ErrorActionPreference = 'Continue'
    return (& $script:Adb -s $Serial @Arguments 2>&1)
}

# Sends a Debug test hook to the running dev build; returns the phone's clock just before the send (one shell call, so adb latency sits in neither).
# Values are single-quoted for the phone's shell, with any single quote escaped.
function Send-TimedHook([string]$Serial, [string]$Scheme, [string]$Command, [hashtable]$HookArgs) {
    $extras = ($HookArgs.GetEnumerator() | ForEach-Object { $quoted = $_.Value.Replace("'", "'\''"); "--es $($_.Key) '$quoted'" }) -join ' '
    $out = Invoke-Adb $Serial @('shell', "date +%s.%N; am broadcast -a uk.sirous.$Scheme.TEST_HOOK --es cmd $Command $extras")
    return [double]("$(@($out)[0])".Trim())
}
