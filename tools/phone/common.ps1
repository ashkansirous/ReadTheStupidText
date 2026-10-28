# Shared helpers for the real-phone scripts (Slice 165). Dot-sourced, not run. Wireless adb only, never USB.
$script:SdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
if (-not $script:SdkRoot) { throw 'ANDROID_HOME (or ANDROID_SDK_ROOT) is not set.' }
$script:Adb = Join-Path $script:SdkRoot 'platform-tools\adb.exe'
$script:RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
. (Join-Path $PSScriptRoot 'quoting.ps1')
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

# Every adb call has a time limit, so a dropped wireless link fails loudly instead of blocking a wait loop in silence.
# install, pull and push move whole files, so they get the long limit.
$script:AdbTimeoutSeconds = 30
$script:AdbTransferTimeoutSeconds = 300
# Kill(bool) (the process tree) exists only in .NET Core; Windows PowerShell 5.1 has Kill() alone. A kill that fails is said, never swallowed.
function Stop-HungProcess([Diagnostics.Process]$Process) {
    try { $Process.Kill($true); return } catch [System.Management.Automation.MethodException] { }
    try { $Process.Kill() } catch { Write-Warning "Could not stop the hung adb process (id $($Process.Id)): $($_.Exception.Message)" }
}

function Invoke-Adb([string]$Serial, [string[]]$Arguments, [int]$TimeoutSeconds = 0) {
    if ($TimeoutSeconds -le 0) { $TimeoutSeconds = if ($Arguments[0] -in 'install', 'pull', 'push') { $script:AdbTransferTimeoutSeconds } else { $script:AdbTimeoutSeconds } }
    $info = New-Object Diagnostics.ProcessStartInfo $script:Adb
    # Windows PowerShell 5.1 (.NET Framework) has no ArgumentList: there the arguments go in one quoted string.
    if ($null -ne $info.ArgumentList) { foreach ($a in (@('-s', $Serial) + $Arguments)) { $info.ArgumentList.Add($a) } }
    else { $info.Arguments = (@('-s', $Serial) + $Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' ' }
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $proc = [Diagnostics.Process]::Start($info)
    $out = $proc.StandardOutput.ReadToEndAsync(); $err = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-HungProcess $proc
        throw "Phone not responding over adb: 'adb -s $Serial $($Arguments -join ' ')' did not finish in $TimeoutSeconds s."
    }
    $proc.WaitForExit()
    $global:LASTEXITCODE = $proc.ExitCode
    return @(($out.Result + $err.Result) -split "`r?`n" | Where-Object { $_ -ne '' })
}

# Sends a Debug test hook to the running dev build; returns the phone's clock just before the send (one shell call, so adb latency sits in neither).
# Values are single-quoted for the phone's shell, with any single quote escaped.
# The broadcast action of the app with this test scheme: uk.sirous.rtst-test.TEST_HOOK or uk.sirous.dino-test.TEST_HOOK. A wrong action fails silently
# (no error, no log line), so the scripts name it when no HOOK line arrives.
$script:HookActionFormat = 'uk.sirous.{0}.TEST_HOOK'

function Send-TimedHook([string]$Serial, [string]$Scheme, [string]$Command, [hashtable]$HookArgs) {
    $extras = ($HookArgs.GetEnumerator() | ForEach-Object { $quoted = $_.Value.Replace("'", "'\''"); "--es $($_.Key) '$quoted'" }) -join ' '
    $out = Invoke-Adb $Serial @('shell', "date +%s.%N; am broadcast -a $($script:HookActionFormat -f $Scheme) --es cmd $Command $extras")
    return [double]("$(@($out)[0])".Trim())
}

# Slice 180: what keeps a run from waiting in silence. `svc power stayon true` only works while charging, so a phone on battery dozes and Android freezes the app.
function Get-PowerState([string]$Serial) {
    $battery = "$(Invoke-Adb $Serial @('shell', 'dumpsys battery'))"
    $wake = "$(Invoke-Adb $Serial @('shell', 'dumpsys power | grep mWakefulness='))"
    $powered = $battery -match '(AC|USB|Wireless) powered: true'
    $state = if ($wake -match 'mWakefulness=(\w+)') { $Matches[1] } else { 'unknown' }
    return [pscustomobject]@{ Powered = $powered; Wakefulness = $state }
}

function Write-PowerState([string]$Serial) {
    $power = Get-PowerState $Serial
    Write-Host ("   phone: {0}, {1}" -f $(if ($power.Powered) { 'on charger (AC/USB/wireless powered)' } else { 'ON BATTERY' }), $power.Wakefulness)
    if (-not $power.Powered) { Write-Host '   WARNING: plug the phone in. On battery it dozes and Android freezes the app; a keep-awake loop runs for this run, but it is only a fallback.' }
    return $power
}

# A background job that wakes the screen every 15 s (a key event only: no unlock, no taps). Always stop it with Stop-KeepAwake.
function Start-KeepAwake([string]$Serial) {
    return Start-Job -ArgumentList $script:Adb, $Serial -ScriptBlock {
        param($adb, $serial)
        while ($true) {
            & $adb -s $serial shell input keyevent KEYCODE_WAKEUP 2>&1 | Out-Null
            Start-Sleep -Seconds 15
        }
    }
}

function Stop-KeepAwake($Job) {
    if (-not $Job) { return }
    Stop-Job $Job -ErrorAction SilentlyContinue
    Remove-Job $Job -Force -ErrorAction SilentlyContinue
}
