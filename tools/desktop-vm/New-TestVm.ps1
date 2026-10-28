<#
.SYNOPSIS
  Builds the headless Windows 11 test VM from scratch (Slice 163) and takes the 'clean' checkpoint.
.DESCRIPTION
  Downloads the current Windows 11 Enterprise evaluation ISO (90-day licence), builds an answer-file DVD, creates a
  Gen2 VM (4 vCPU, 6 GB, no network adapter, no window), installs unattended, signs the VM user in automatically and
  turns Developer Mode on. Then, with a network adapter attached for that one step, it activates the evaluation, installs
  every pending Windows update (rebooting as needed) and switches Windows Update off for good; the adapter is removed and
  the running desktop is checkpointed as 'clean' (so the image is current as of its build date and frozen afterwards).
  The ISO the script downloaded is deleted afterwards (-KeepIso keeps it). Needs Hyper-V Administrators, not elevation.
  A wait with no progress for -StallMin minutes saves a screenshot under <VmRoot>\screens and fails with its path.
  Rebuild with -Force when the evaluation expires (about every 90 days) or the VM is damaged.
  -UpdateOnly (about 15 minutes) is the monthly refresh instead of a rebuild: it restores the existing 'clean', lifts the
  Windows Update freeze for one run, installs the pending updates the same way, freezes it again, takes 'clean-new' and only
  then swaps it in for the old 'clean' (renames first, the slow merge last; any failure or Ctrl+C restores the old one, and it
  refuses to publish when the guest got no network or the update deadline passed). It downloads no ISO, deletes no VM and does
  not extend the evaluation; it cannot be combined with -Force, -IsoPath or -SkipUpdates.
#>
[CmdletBinding()]
param(
    [string] $VmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm',
    [string] $IsoPath,
    [switch] $Force,
    [int] $InstallTimeoutMin = 75,
    [int] $UpdateTimeoutMin = 120,
    [int] $StallMin = 10,
    [switch] $SkipUpdates,
    [switch] $KeepIso,
    [switch] $UpdateOnly
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
$script:StallMin = $StallMin
$script:ActiveVmRoot = $VmRoot

$EvalCenterPage = 'https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise'
$EvalIsoPattern = 'Windows 11 Enterprise ISO 64-bit \(en-US\)[^>]*href="(https://go\.microsoft\.com/fwlink/[^"]+)"'
$VhdSizeBytes = 64GB
$BootKeyCode = 0x58   # virtual-key X; see Send-BootKeys
$OnlineSwitchName = 'Default Switch'
$OnlineAdapterName = 'BuildOnly'
$NetworkWaitPolls = 24
$MaxUpdateRounds = 6
$UpdateRebootTimeoutMin = 30
$GuestUpdateDir = 'C:\desktop-test\updates'
$GuestUpdateTask = 'rtst-wu-round'
$ActivationDeadlineSec = 180
$UpdateRebootStallMin = 25
$UpdateServices = 'wuauserv'          # must be Disabled in the read-back
$UpdateSoftServices = 'UsoSvc'        # can be protected and revived by WaaSMedic: only a warning
$DeadTaskPolls = 2
$ReadBackAttempts = 3
$ReadBackPauseSec = 20
$NewCheckpointName = 'clean-new'      # -UpdateOnly: the refreshed image, renamed to 'clean' only once it exists
$OldCheckpointName = 'clean-prev'     # -UpdateOnly: the old 'clean' during the swap (an interrupted swap is repaired by name)
$UpdateOnlyDesktopTimeoutMin = 15
$script:UpdateWarnings = @()           # -UpdateOnly: problems that did not stop the run, repeated in the final summary

# Runs in the guest as SYSTEM (one round): search, then download and install each update on its own so the status moves.
$GuestUpdateScript = @'
param([string] $StatusFile, [string] $ResultFile)
$ErrorActionPreference = 'Stop'
$CodesOk = 2, 3   # orcSucceeded, orcSucceededWithErrors

function Write-Status([string] $phase, [int] $done, [int] $total, [string] $title) {
    $rx = (Get-NetAdapterStatistics -ErrorAction SilentlyContinue | Measure-Object ReceivedBytes -Sum).Sum
    $cpu = [int](Get-Process TiWorker, TrustedInstaller, MoUsoCoreWorker -ErrorAction SilentlyContinue | Measure-Object CPU -Sum).Sum
    @{ phase = $phase; done = $done; total = $total; title = $title; rx = $rx; cpu = $cpu } | ConvertTo-Json | Set-Content $StatusFile
}

function Install-One($session, $update, [int] $index, [int] $total) {
    if ($update.InstallationBehavior.CanRequestUserInput) { throw 'skipped: the update may ask for user input' }
    if (-not $update.EulaAccepted) { $update.AcceptEula() }
    $coll = New-Object -ComObject Microsoft.Update.UpdateColl
    [void]$coll.Add($update)
    Write-Status 'downloading' $index $total $update.Title
    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $coll
    $d = $downloader.Download()
    if ($d.ResultCode -notin $CodesOk) { throw ('download failed, result code {0}, HRESULT 0x{1:X8}' -f $d.ResultCode, $d.HResult) }
    Write-Status 'installing' $index $total $update.Title
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $coll
    $r = $installer.Install()
    if ($r.ResultCode -notin $CodesOk) { throw ('install failed, result code {0}, HRESULT 0x{1:X8}' -f $r.ResultCode, $r.HResult) }
    return @{ ok = $true; reboot = [bool]$r.RebootRequired }
}

# A search can fail right after the network comes up (DNS or NAT not ready, 0x80072EE7 / 0x80072EFD): retry before giving up.
function Search-Updates($session) {
    for ($attempt = 1; $true; $attempt++) {
        # The search result, not .Updates: returning a one-item COM collection unrolls it to a bare update with no Count.
        try { return $session.CreateUpdateSearcher().Search($SearchCriteria) }
        catch { if ($attempt -ge $SearchAttempts) { throw }; Start-Sleep -Seconds $SearchRetryPauseSec }
    }
}

# Software only: no drivers. Feature updates stay in on purpose (the image should carry the latest Windows).
$SearchCriteria = "IsInstalled=0 and IsHidden=0 and Type='Software'"
$SearchAttempts = 3; $SearchRetryPauseSec = 30
$found = 0; $installed = 0; $failed = 0; $reboot = $false; $failure = $null
$failures = New-Object System.Collections.Generic.List[string]
$outcomes = New-Object System.Collections.Generic.List[string]
try {
    $session = New-Object -ComObject Microsoft.Update.Session
    Write-Status 'searching' 0 0 ''
    $updates = (Search-Updates $session).Updates
    $found = $updates.Count
    for ($i = 0; $i -lt $found; $i++) {
        $update = $updates.Item($i)
        try { $one = Install-One $session $update ($i + 1) $found }
        catch { $one = @{ ok = $false; reboot = $false }; $failures.Add("$($update.Title): $($_.Exception.Message)") }
        if ($one.ok) { $installed++ } else { $failed++ }
        if ($one.reboot) { $reboot = $true }
        $outcomes.Add(('{0} {1}' -f $(if ($one.ok) { 'installed' } else { 'FAILED   ' }), $update.Title))
    }
} catch { $failure = $_.Exception.Message }
@{ found = $found; installed = $installed; failed = $failed; reboot = $reboot; error = $failure; failures = @($failures); outcomes = @($outcomes) } | ConvertTo-Json | Set-Content $ResultFile
'@

# Runs in the guest (admin session): returns the warnings for the steps that failed.
$GuestFreezeScript = @'
$ErrorActionPreference = 'Stop'
$warnings = New-Object System.Collections.Generic.List[string]
function Set-Reg([string] $path, [string] $name, [int] $value) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -Path $path -Name $name -Value $value -PropertyType DWord -Force | Out-Null
}
function Invoke-Step([string] $what, [scriptblock] $step) {
    try { & $step } catch { $warnings.Add("$what failed: $($_.Exception.Message)") }
}
$wu = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
Invoke-Step 'Windows Update policies' {
    Set-Reg $wu 'DoNotConnectToWindowsUpdateInternetLocations' 1
    Set-Reg $wu 'SetDisableUXWUAccess' 1
    Set-Reg $wu 'DisableOSUpgrade' 1
    Set-Reg "$wu\AU" 'NoAutoUpdate' 1
    Set-Reg "$wu\AU" 'AUOptions' 1
}
function Disable-UpdateService([string] $svc) {
    # Set-Service can be refused for a protected service: the Start value in the registry (4 = disabled) is the same switch.
    try { Set-Service -Name $svc -StartupType Disabled }
    catch { Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Start -Value 4 -Type DWord }
    Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
}
foreach ($svc in 'wuauserv', 'UsoSvc') {   # keep in step with $UpdateServices and $UpdateSoftServices on the host
    Invoke-Step "disabling service $svc" { Disable-UpdateService $svc }
}
foreach ($path in '\Microsoft\Windows\UpdateOrchestrator\', '\Microsoft\Windows\WindowsUpdate\') {
    foreach ($task in (Get-ScheduledTask -TaskPath $path -ErrorAction SilentlyContinue)) {
        Invoke-Step "disabling task $($task.TaskPath)$($task.TaskName)" { Disable-ScheduledTask -TaskPath $task.TaskPath -TaskName $task.TaskName | Out-Null }
    }
}
Invoke-Step 'finish-setting-up screen' { Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement' 'ScoobeSystemSettingEnabled' 0 }
Invoke-Step 'tips and suggestions' {
    $cdm = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
    foreach ($n in 'SubscribedContent-338389Enabled', 'SubscribedContent-310093Enabled', 'SubscribedContent-338393Enabled', 'SystemPaneSuggestionsEnabled', 'SoftLandingEnabled') { Set-Reg $cdm $n 0 }
    Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableSoftLanding' 1
    Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableWindowsConsumerFeatures' 1
}
$warnings
'@

function Get-EvalIso {
    param([string] $Dir)
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    $existing = Get-ChildItem $Dir -Filter '*.iso' -ErrorAction SilentlyContinue | Where-Object Name -like 'win11-ent-eval*' | Select-Object -First 1
    if ($existing) { return $existing.FullName }
    Write-Host 'Looking up the current evaluation ISO link...'
    $page = (Invoke-WebRequest -Uri $EvalCenterPage -UseBasicParsing).Content
    if ($page -notmatch $EvalIsoPattern) { throw "Evaluation ISO link not found on $EvalCenterPage (page layout changed); pass -IsoPath." }
    $url = $Matches[1] -replace '&amp;', '&'
    $target = Join-Path $Dir 'win11-ent-eval-en-us.iso'
    $partial = "$target.part"
    Write-Host "Downloading $url (about 8 GB) to $partial; resumable, Ctrl+C to stop"
    & curl.exe -fL -# -C - -o $partial $url
    if ($LASTEXITCODE -ne 0) { throw "ISO download failed (curl $LASTEXITCODE); run again to resume." }
    Move-Item $partial $target
    return $target
}

function Write-AnswerStage {
    param([string] $Dir, [pscredential] $Credential)
    $xml = Get-Content "$PSScriptRoot\autounattend.template.xml" -Raw
    $pw = [System.Security.SecurityElement]::Escape($Credential.GetNetworkCredential().Password)
    $xml = $xml.Replace('@@USER@@', $Credential.UserName).Replace('@@PASSWORD@@', $pw)
    $stage = Join-Path $Dir 'answer-stage'
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    [IO.File]::WriteAllText((Join-Path $stage 'autounattend.xml'), $xml, (New-Object Text.UTF8Encoding $false))
    return $stage
}

function Initialize-IsoWriter {
    if ('IsoStreamWriter' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System; using System.IO; using System.Runtime.InteropServices; using System.Runtime.InteropServices.ComTypes;
public static class IsoStreamWriter {
  public static void Write(object comStream, string path) {
    IStream s = (IStream)comStream; STATSTG st; s.Stat(out st, 1);
    byte[] buf = new byte[1 << 20]; IntPtr read = Marshal.AllocHGlobal(4);
    using (FileStream fs = File.Create(path)) {
      long left = st.cbSize;
      while (left > 0) { int n = (int)Math.Min(buf.Length, left); s.Read(buf, n, read); int got = Marshal.ReadInt32(read); if (got == 0) break; fs.Write(buf, 0, got); left -= got; }
    }
    Marshal.FreeHGlobal(read);
  }
}
'@
}

function New-AnswerIso {
    # The answer file holds the VM password, so the staged copy is removed even when the build throws.
    param([string] $Dir, [string] $IsoFile, [pscredential] $Credential)
    $stage = Write-AnswerStage -Dir $Dir -Credential $Credential
    try {
        Initialize-IsoWriter
        $image = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
        $image.FileSystemsToCreate = 3
        $image.VolumeName = 'ANSWER'
        $image.Root.AddTree($stage, $false)
        [IsoStreamWriter]::Write($image.CreateResultImage().ImageStream, $IsoFile)
    } finally { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
}

# Runs in the guest (admin session) for -UpdateOnly: undoes the update switches of $GuestFreezeScript so one update run can work.
$GuestUnfreezeScript = @'
$ErrorActionPreference = 'Stop'
$wu = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
foreach ($name in 'DoNotConnectToWindowsUpdateInternetLocations', 'SetDisableUXWUAccess', 'DisableOSUpgrade') {
    Remove-ItemProperty -Path $wu -Name $name -ErrorAction SilentlyContinue
}
foreach ($name in 'NoAutoUpdate', 'AUOptions') { Remove-ItemProperty -Path "$wu\AU" -Name $name -ErrorAction SilentlyContinue }
function Enable-UpdateService([string] $svc) {
    # Same registry fallback as the freeze (3 = manual).
    try { Set-Service -Name $svc -StartupType Manual }
    catch { Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Start -Value 3 -Type DWord }
}
foreach ($svc in 'wuauserv', 'UsoSvc') {
    try { Enable-UpdateService $svc } catch { Write-Warning "enabling service ${svc}: $($_.Exception.Message)" }
}
try { Start-Service -Name wuauserv }
catch {
    # The service manager can keep the cached start type (disabled) until a reboot; ask it to change the type, then start.
    Write-Warning "starting wuauserv failed ($($_.Exception.Message)); retrying after sc.exe config."
    & sc.exe config wuauserv start= demand | Out-Null
    Start-Service -Name wuauserv
}
'@

function Remove-ExistingVm {
    $vm = Get-VM -Name $script:VmName -ErrorAction SilentlyContinue
    if (-not $vm) { return }
    if (-not $Force) { throw "VM '$($script:VmName)' already exists. Use -Force to delete and rebuild it." }
    if ($vm.State -notin 'Off', 'Saved') { Stop-VM -Name $script:VmName -TurnOff -Force }
    Get-VMCheckpoint -VMName $script:VmName | Remove-VMCheckpoint -Confirm:$false
    Remove-VM -Name $script:VmName -Force
    Remove-Item (Join-Path $VmRoot 'vm') -Recurse -Force -ErrorAction SilentlyContinue
}

function New-VmShell {
    param([string] $VhdPath, [string] $InstallIso, [string] $AnswerIso)
    New-VHD -Path $VhdPath -SizeBytes $VhdSizeBytes -Dynamic | Out-Null
    New-VM -Name $script:VmName -Generation 2 -MemoryStartupBytes 6GB -VHDPath $VhdPath -Path (Join-Path $VmRoot 'vm') | Out-Null
    # No network adapter on purpose: the app under test must not reach analytics, the Worker or the pack CDN.
    Get-VMNetworkAdapter -VMName $script:VmName | Remove-VMNetworkAdapter
    Set-VMProcessor -VMName $script:VmName -Count 4
    Set-VMMemory -VMName $script:VmName -DynamicMemoryEnabled $false -StartupBytes 6GB
    Set-VM -Name $script:VmName -CheckpointType Standard -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction TurnOff
    Enable-VMIntegrationService -VMName $script:VmName -Name 'Guest Service Interface'
    Set-VMFirmware -VMName $script:VmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
    Add-VMDvdDrive -VMName $script:VmName -Path $InstallIso
    Add-VMDvdDrive -VMName $script:VmName -Path $AnswerIso
    $dvd = Get-VMDvdDrive -VMName $script:VmName | Where-Object Path -eq $InstallIso
    $hdd = Get-VMHardDiskDrive -VMName $script:VmName
    Set-VMFirmware -VMName $script:VmName -BootOrder $dvd, $hdd
}

function Send-BootKeys {
    # "Press any key to boot from CD or DVD": tap X through the virtual keyboard for the first 16 seconds. Not Space or
    # Enter: a late key reaches Setup's window, whose focused Cancel button they press ("Are you sure you want to quit?").
    # X is no button's access key there (Cancel is C, Yes/No are Y/N), so a late X does nothing.
    $cs = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$($script:VmName)'"
    $kb = Get-CimAssociatedInstance -InputObject $cs -ResultClassName Msvm_Keyboard
    1..8 | ForEach-Object { Invoke-CimMethod -InputObject $kb -MethodName TypeKey -Arguments @{ keyCode = $BootKeyCode } | Out-Null; Start-Sleep -Seconds 2 }
}

function Wait-Provisioned {
    # A fresh session per poll: Windows may reboot again after first logon, and a session held across that goes Broken.
    param([pscredential] $Credential)
    $started = Get-Date
    $deadline = $started.AddMinutes($InstallTimeoutMin + 15)
    $nextNote = $started
    $watch = New-StallWatch -What 'Windows Setup and first logon'
    while ((Get-Date) -lt $deadline) {
        $ready = Invoke-FreshGuest -Credential $Credential -Script { (Test-Path C:\Windows\Temp\rtst-provisioned.txt) -and [bool](Get-Process explorer -ErrorAction SilentlyContinue) }
        if ($ready) { return }
        Step-StallWatch -Watch $watch
        if ((Get-Date) -ge $nextNote) {
            Write-Host ("  waiting for Windows Setup and first logon, {0:N0} min" -f ((Get-Date) - $started).TotalMinutes)
            $nextNote = (Get-Date).AddMinutes(1)
        }
        Start-Sleep -Seconds 10
    }
    $png = Save-VmScreenshot -Reason 'provisioning-timeout'
    throw "First-logon provisioning did not finish; screen: $png"
}

function Complete-Guest {
    param([pscredential] $Credential)
    $session = New-VmSession -Credential $Credential
    try {
        Invoke-Command -Session $session -ScriptBlock {
            Remove-Item C:\Windows\Panther\unattend*.xml, C:\Windows\Panther\autounattend*.xml -Force -ErrorAction SilentlyContinue
            Remove-Item C:\Windows\Panther\Unattend, C:\Windows\Panther\UnattendGC -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item C:\Windows\System32\Sysprep\unattend.xml -Force -ErrorAction SilentlyContinue
            New-Item -ItemType Directory -Force -Path C:\desktop-test | Out-Null
        }
    } finally { Remove-PSSession $session -ErrorAction SilentlyContinue }
}

function Get-GuestBootTime {
    param([pscredential] $Credential)
    $boot = Invoke-FreshGuest -Credential $Credential -Script { (Get-CimInstance Win32_OperatingSystem).LastBootUpTime }
    if (-not $boot) { throw 'Could not read the guest boot time.' }
    return $boot
}

function Wait-Desktop {
    # Ready = a guest that booted AFTER $BootedBefore shows its shell, and keeps showing it (same boot) for $StableSec.
    # Every poll uses a fresh session, so nothing opened before the reboot is ever reused.
    param([pscredential] $Credential, [datetime] $BootedBefore, [int] $StableSec = 60, [int] $TimeoutMin = 10, [int] $StallLimitMin = 0)
    $started = Get-Date
    $deadline = $started.AddMinutes($TimeoutMin)
    $watch = New-StallWatch -What 'the rebooted desktop' -LimitMin $StallLimitMin
    $stableSince = $null
    $stableBoot = $null
    while ((Get-Date) -lt $deadline) {
        $state = Invoke-FreshGuest -Credential $Credential -Script {
            [pscustomobject]@{
                Boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
                Up = [bool](Get-Process explorer -ErrorAction SilentlyContinue) -and [bool](Get-Process StartMenuExperienceHost -ErrorAction SilentlyContinue)
            }
        }
        if ($state -and $state.Up -and $state.Boot -gt $BootedBefore) {
            if (-not $stableSince -or $state.Boot -ne $stableBoot) { $stableSince = Get-Date; $stableBoot = $state.Boot }
            elseif (((Get-Date) - $stableSince).TotalSeconds -ge $StableSec) { return }
        } else { $stableSince = $null }
        Step-StallWatch -Watch $watch -Token "$($state.Boot)|$($state.Up)"
        Write-Host ("  waiting for the rebooted desktop, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds)
        Start-Sleep -Seconds 10
    }
    $png = Save-VmScreenshot -Reason 'desktop-timeout'
    throw "The VM desktop did not come up (after the reboot) and stay up; screen: $png"
}

function Get-GuestWindowsVersion {
    param([pscredential] $Credential)
    $text = Invoke-FreshGuest -Credential $Credential -Script {
        # ProductName still says "Windows 10" on Windows 11; the OS caption is right.
        $k = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
        '{0} {1} (build {2}.{3})' -f (Get-CimInstance Win32_OperatingSystem).Caption, $k.DisplayVersion, $k.CurrentBuild, $k.UBR
    }
    if ($text) { return $text }
    return 'unknown (guest not answering)'
}

function Wait-GuestNetwork {
    # True once the guest has a usable IPv4 address (not loopback, not link-local).
    param([pscredential] $Credential)
    for ($i = 0; $i -lt $NetworkWaitPolls; $i++) {
        Start-Sleep -Seconds 5
        $online = Invoke-FreshGuest -Credential $Credential -Script { [bool](Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.)' }) }
        if ($online) { return $true }
    }
    return $false
}

function Invoke-EvaluationActivation {
    # An evaluation licence starts its 90 days only once Windows has activated it against Microsoft. A failure is a warning, not a stop.
    param([pscredential] $Credential)
    # slmgr can hang with no screen to show for it, so the guest runs it with a deadline and kills it when that passes.
    $result = Invoke-FreshGuest -Credential $Credential -Script {
        $out = Join-Path $env:TEMP 'rtst-slmgr.txt'
        $p = Start-Process cscript -ArgumentList '//nologo', 'C:\Windows\System32\slmgr.vbs', '/ato' -PassThru -WindowStyle Hidden -RedirectStandardOutput $out
        $null = $p.Handle   # Windows PowerShell 5.1 reports ExitCode as null unless the handle is opened before the exit
        if (-not $p.WaitForExit($using:ActivationDeadlineSec * 1000)) { $p.Kill(); return 'TIMEOUT' }
        $text = (Get-Content $out -Raw -ErrorAction SilentlyContinue)
        Remove-Item $out -Force -ErrorAction SilentlyContinue
        if ($null -ne $p.ExitCode -and $p.ExitCode -ne 0) { return "EXIT $($p.ExitCode) $text" }
        return "$text".Trim()
    }
    if (-not $result) { Write-Warning 'Activation did not answer (the evaluation may stay in notification mode).'; return }
    if ($result -eq 'TIMEOUT') { Write-Warning "Activation did not finish within $ActivationDeadlineSec s and was stopped (the evaluation may stay in notification mode)."; return }
    if ($result -like 'EXIT *') { Write-Warning "Activation failed: $result"; return }
    Write-Host "Activation: $result"
}

function Start-UpdateRound {
    # The Windows Update Agent refuses remote callers, so each round runs inside the guest as a one-shot SYSTEM task.
    param([pscredential] $Credential)
    $session = New-VmSession -Credential $Credential
    try {
        Invoke-Command -Session $session -ArgumentList $GuestUpdateScript, $GuestUpdateDir, $GuestUpdateTask -ScriptBlock {
            param($text, $dir, $task)
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            Remove-Item "$dir\status.json", "$dir\result.json" -Force -ErrorAction SilentlyContinue
            Set-Content -Path "$dir\update-round.ps1" -Value $text -Encoding UTF8
            $arg = "-NoProfile -ExecutionPolicy Bypass -File `"$dir\update-round.ps1`" -StatusFile `"$dir\status.json`" -ResultFile `"$dir\result.json`""
            $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
            $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 3)
            Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
            Register-ScheduledTask -TaskName $task -Action $action -Principal $principal -Settings $settings | Out-Null
            Start-ScheduledTask -TaskName $task
        }
    } finally { Remove-PSSession $session -ErrorAction SilentlyContinue }
}

function Read-UpdateFiles {
    param([pscredential] $Credential)
    $dir = $GuestUpdateDir
    $task = $GuestUpdateTask
    return Invoke-FreshGuest -Credential $Credential -Script {
        [pscustomobject]@{
            Status = (Get-Content "$using:dir\status.json" -Raw -ErrorAction SilentlyContinue)
            Result = (Get-Content "$using:dir\result.json" -Raw -ErrorAction SilentlyContinue)
            # Live counters sampled now, so a long download or install shows movement between the guest's status writes.
            Rx = (Get-NetAdapterStatistics -ErrorAction SilentlyContinue | Measure-Object ReceivedBytes -Sum).Sum
            Cpu = [int](Get-Process TiWorker, TrustedInstaller, MoUsoCoreWorker -ErrorAction SilentlyContinue | Measure-Object CPU -Sum).Sum
            TaskState = [string](Get-ScheduledTask -TaskName $using:task -ErrorAction SilentlyContinue).State
            TaskCode = (Get-ScheduledTaskInfo -TaskName $using:task -ErrorAction SilentlyContinue).LastTaskResult
        }
    }
}

function ConvertFrom-JsonSafe {
    # A file read mid-write is not valid JSON: treat that as "not ready yet" ($null), never an error.
    param([string] $Text)
    if (-not $Text) { return $null }
    try { return ($Text | ConvertFrom-Json -ErrorAction Stop) } catch { return $null }
}

function Write-UpdateNote {
    param([int] $Round, $Status, [datetime] $Started)
    $what = if ($Status) { "$($Status.phase) $($Status.done)/$($Status.total) $($Status.title)" } else { 'starting' }
    Write-Host ("  updates round {0}/{1}: {2}, {3:N0} min" -f $Round, $MaxUpdateRounds, $what, ((Get-Date) - $Started).TotalMinutes)
}

function Assert-UpdateTaskAlive {
    # The guest task exits without a result file when its script dies or the guest reboots by itself: fail the round at once.
    # Two polls in a row, because the task can finish between reading the result file and reading the task state.
    param($Files, [ref] $DeadPolls, [int] $Round)
    if (-not $Files -or -not $Files.TaskState -or $Files.TaskState -eq 'Running') { $DeadPolls.Value = 0; return }
    $DeadPolls.Value++
    if ($DeadPolls.Value -lt $DeadTaskPolls) { return }
    $png = Save-VmScreenshot -Reason 'update-task-ended'
    throw "Windows Update round $Round ended in the guest without a result (task state $($Files.TaskState), last result $($Files.TaskCode)); screen: $png"
}

function Wait-UpdateRound {
    # Polls the guest's status and result files. Progress = the status changing, or bytes received / installer CPU sampled on each poll.
    # Returns $null when the overall deadline passes first (the caller stops the guest task and carries on).
    param([pscredential] $Credential, [int] $Round, [datetime] $Deadline)
    $watch = New-StallWatch -What "Windows Update round $Round"
    $started = Get-Date
    $nextNote = $started
    $deadPolls = 0
    while ((Get-Date) -lt $Deadline) {
        $files = Read-UpdateFiles -Credential $Credential
        $result = if ($files) { ConvertFrom-JsonSafe $files.Result }
        if ($result) { return $result }
        Assert-UpdateTaskAlive -Files $files -DeadPolls ([ref]$deadPolls) -Round $Round
        $statusText = if ($files -and $files.Status) { $files.Status } else { '' }
        $live = if ($files) { "$($files.Rx)|$($files.Cpu)" } else { '' }
        Step-StallWatch -Watch $watch -Token "$statusText|$live"
        if ((Get-Date) -ge $nextNote) {
            Write-UpdateNote -Round $Round -Started $started -Status (ConvertFrom-JsonSafe $statusText)
            $nextNote = (Get-Date).AddMinutes(1)
        }
        Start-Sleep -Seconds 15
    }
    return $null
}

function Stop-UpdateRound {
    # Best effort: the round is abandoned, so its SYSTEM task must not keep installing into the frozen image.
    param([pscredential] $Credential)
    $task = $GuestUpdateTask
    [void](Invoke-FreshGuest -Credential $Credential -Script { Stop-ScheduledTask -TaskName $using:task -ErrorAction SilentlyContinue })
}

function Restart-GuestGracefully {
    # Windows commits a big update while it shuts down ("Working on updates"). Restart-VM is a hard reset that skips that,
    # so a cumulative update reported as installed never applied (2026-10-28: the build stayed on the old UBR after two rounds).
    # Only a guest that does not answer gets the hard reset.
    param([pscredential] $Credential)
    $asked = Invoke-FreshGuest -Credential $Credential -Script { Restart-Computer -Force; $true }
    if (-not $asked) { Write-Warning 'The guest did not answer a restart request; resetting the VM instead.'; Restart-VM -Name $script:VmName -Force }
}

function Restart-GuestForUpdates {
    param([pscredential] $Credential)
    Write-Host '  an update needs a reboot; restarting Windows (it finishes the update while it shuts down)...'
    $bootedBefore = Get-GuestBootTime -Credential $Credential
    Restart-GuestGracefully -Credential $Credential
    Wait-Desktop -Credential $Credential -BootedBefore $bootedBefore -TimeoutMin $UpdateRebootTimeoutMin -StallLimitMin $UpdateRebootStallMin
}

function Write-OnlineProblem {
    # A full build goes on with what it has (nothing better exists); -UpdateOnly keeps the old 'clean' instead of publishing a no-op or half-done refresh.
    param([string] $Message)
    if ($UpdateOnly) { throw "$Message The old '$($script:CheckpointName)' is kept." }
    Write-Warning $Message
}

function Add-UpdateWarning([string] $message) {
    Write-Warning $message
    $script:UpdateWarnings += $message
}

function Install-AllUpdates {
    # Install, reboot when required, search again; stops when nothing is left, nothing more installs, or after $MaxUpdateRounds.
    param([pscredential] $Credential)
    $deadline = (Get-Date).AddMinutes($UpdateTimeoutMin)
    for ($round = 1; $round -le $MaxUpdateRounds; $round++) {
        if ((Get-Date) -ge $deadline) { Write-OnlineProblem "-UpdateTimeoutMin $UpdateTimeoutMin reached before round $round; continuing with what is installed."; return }
        Start-UpdateRound -Credential $Credential
        $r = Wait-UpdateRound -Credential $Credential -Round $round -Deadline $deadline
        if (-not $r) {
            Stop-UpdateRound -Credential $Credential
            Write-OnlineProblem "-UpdateTimeoutMin $UpdateTimeoutMin reached during round $round; stopped it, continuing with what is installed."
            return
        }
        Write-Host ("Updates round {0}: found {1}, installed {2}, failed {3}, reboot required: {4}" -f $round, $r.found, $r.installed, $r.failed, $r.reboot)
        foreach ($o in @($r.outcomes)) { if ($o) { Write-Host "  $o" } }
        foreach ($f in @($r.failures)) { if ($f) { Add-UpdateWarning "update failed: $f" } }
        if ($r.error) { throw "Windows Update failed in the guest: $($r.error)" }
        if ($null -eq $r.found) { Write-Warning "Round $round reported no update count; searching again."; continue }
        if ($r.found -eq 0) { Write-Host '  Windows is up to date.'; return }
        if ($r.installed -eq 0 -and -not $r.reboot) { Add-UpdateWarning "No update could be installed in round $round; stopping."; return }
        if ($r.reboot) { Restart-GuestForUpdates -Credential $Credential }
    }
    Add-UpdateWarning "Updates were still being found after $MaxUpdateRounds rounds; continuing with what is installed."
}

function Invoke-OnlineBuild {
    # The one step with a network: activate, then install every pending update. The adapter is removed before the checkpoint.
    param([pscredential] $Credential)
    if (-not (Get-VMSwitch -Name $OnlineSwitchName -ErrorAction SilentlyContinue)) { Write-OnlineProblem "No '$OnlineSwitchName' virtual switch: no activation and no updates."; return }
    Write-Host "Attaching a network adapter for activation and updates only ('$OnlineSwitchName')..."
    Add-VMNetworkAdapter -VMName $script:VmName -SwitchName $OnlineSwitchName -Name $OnlineAdapterName
    try {
        if (-not (Wait-GuestNetwork -Credential $Credential)) { Write-OnlineProblem 'The guest got no network address: no activation and no updates.'; return }
        Invoke-EvaluationActivation -Credential $Credential
        if ($SkipUpdates) { Write-Host 'Skipping Windows updates (-SkipUpdates).'; return }
        Install-AllUpdates -Credential $Credential
        Write-Host ("Windows after updates: {0}" -f (Get-GuestWindowsVersion -Credential $Credential))
    } finally { Get-VMNetworkAdapter -VMName $script:VmName -Name $OnlineAdapterName -ErrorAction SilentlyContinue | Remove-VMNetworkAdapter }
}

function Disable-GuestWindowsUpdate {
    # Freezes the image: Windows Update off by policy, services and tasks disabled, no "finish setting up" screen or tips.
    param([pscredential] $Credential)
    Write-Host 'Turning Windows Update off for good...'
    $session = New-VmSession -Credential $Credential
    try {
        $warnings = Invoke-Command -Session $session -ScriptBlock ([scriptblock]::Create($GuestFreezeScript))
        foreach ($w in $warnings) { Write-Warning $w }
        Remove-GuestUpdateLeftovers -Session $session
    } finally { Remove-PSSession $session -ErrorAction SilentlyContinue }
}

function Enable-GuestWindowsUpdate {
    # -UpdateOnly: lifts the freeze for this one run; Disable-GuestWindowsUpdate puts it back before the checkpoint.
    param([pscredential] $Credential)
    Write-Host 'Turning Windows Update on for this run...'
    $session = New-VmSession -Credential $Credential
    try { Invoke-Command -Session $session -ScriptBlock ([scriptblock]::Create($GuestUnfreezeScript)) }
    finally { Remove-PSSession $session -ErrorAction SilentlyContinue }
}

function Remove-GuestUpdateLeftovers {
    # The SYSTEM update task and its folder must not ship inside the 'clean' image.
    param($Session)
    $problems = Invoke-Command -Session $Session -ArgumentList $GuestUpdateTask, $GuestUpdateDir -ScriptBlock {
        param($task, $dir)
        Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        if (Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue) { "task $task is still registered" }
        if (Test-Path $dir) { "folder $dir is still there" }
    }
    foreach ($p in $problems) { Write-Warning "update leftovers: $p" }
}

# Runs in the guest: lists everything that would let Windows Update run (empty when the image is frozen).
$GuestVerifyScript = @'
param([string[]] $Services, [string[]] $SoftServices)
$problems = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
function Test-Policy([string] $path, [string] $name) {
    $v = (Get-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue).$name
    if ($v -ne 1) { $problems.Add("policy $path\$name is '$v', expected 1") }
}
$wu = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
Test-Policy $wu 'DoNotConnectToWindowsUpdateInternetLocations'
Test-Policy "$wu\AU" 'NoAutoUpdate'
foreach ($name in $Services) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (-not $svc) { $problems.Add("service $name not found"); continue }
    if ($svc.StartType -ne 'Disabled') { $problems.Add("service $name start type is $($svc.StartType), expected Disabled") }
}
foreach ($name in $SoftServices) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if ($svc -and $svc.StartType -ne 'Disabled') { $warnings.Add("service $name start type is $($svc.StartType)") }
}
[pscustomobject]@{ Answered = $true; Problems = @($problems); Warnings = @($warnings) }
'@

function Read-BackUpdateState {
    # One transient PowerShell Direct failure right after the reboot must not throw away a long build: ask up to $ReadBackAttempts times.
    param([pscredential] $Credential)
    $text = $GuestVerifyScript
    $services = $UpdateServices
    $soft = $UpdateSoftServices
    for ($attempt = 1; $true; $attempt++) {
        $reply = Invoke-FreshGuest -Credential $Credential -Script { & ([scriptblock]::Create($using:text)) $using:services $using:soft }
        if (($reply -and $reply.Answered) -or $attempt -ge $ReadBackAttempts) { return $reply }
        Start-Sleep -Seconds $ReadBackPauseSec
    }
}

function Assert-WindowsUpdateOff {
    # Read-back before the checkpoint: a 'clean' image with Windows Update on must never be taken.
    param([pscredential] $Credential)
    $reply = Read-BackUpdateState -Credential $Credential
    if ($reply) { foreach ($w in @($reply.Warnings)) { if ($w) { Write-Warning "update read-back: $w" } } }
    $adapters = @(Get-VMNetworkAdapter -VMName $script:VmName -ErrorAction SilentlyContinue)
    $problems = if ($reply -and $reply.Answered) { @($reply.Problems) } else { @('the guest did not answer the Windows Update read-back') }
    $all = @($problems) + @($adapters | ForEach-Object { "network adapter $($_.Name) is still attached" })
    if ($all.Count -eq 0) { Write-Host 'Verified: Windows Update is off and the VM has no network adapter.'; return }
    $png = Save-VmScreenshot -Reason 'update-not-off'
    throw "Refusing to checkpoint: $($all -join '; '); screen: $png"
}

function Complete-Build {
    # Detach the media, reboot once so the desktop is settled, and checkpoint it (a full build takes 'clean'; -UpdateOnly takes 'clean-new' first).
    param([pscredential] $Credential, [string] $CheckpointName = $script:CheckpointName)
    Write-Host 'Detaching the media and rebooting once to settle...'
    Get-VMDvdDrive -VMName $script:VmName | Set-VMDvdDrive -Path $null
    Set-VMFirmware -VMName $script:VmName -FirstBootDevice (Get-VMHardDiskDrive -VMName $script:VmName)
    $bootedBefore = Get-GuestBootTime -Credential $Credential
    Restart-GuestGracefully -Credential $Credential
    Wait-Desktop -Credential $Credential -BootedBefore $bootedBefore -TimeoutMin $UpdateRebootTimeoutMin -StallLimitMin $UpdateRebootStallMin
    Assert-WindowsUpdateOff -Credential $Credential
    Checkpoint-VM -Name $script:VmName -SnapshotName $CheckpointName
}

function Test-Checkpoint {
    param([string] $Name)
    $found = Get-VMCheckpoint -VMName $script:VmName -Name $Name -ErrorAction SilentlyContinue
    return [bool]$found
}

function Remove-StrayCheckpoint {
    param([string] $Name)
    if (-not (Test-Checkpoint $Name)) { return }
    Write-Host "Merging the old checkpoint '$Name' away, this can take a few minutes..."
    Get-VMCheckpoint -VMName $script:VmName -Name $Name | Remove-VMCheckpoint -Confirm:$false
}

function Get-SwapState {
    # Where an interrupted refresh stopped: Published (both renames done), Interrupted (between them, the new image is complete), else NotPublished.
    $clean = Test-Checkpoint $script:CheckpointName
    $prev = Test-Checkpoint $OldCheckpointName
    $new = Test-Checkpoint $NewCheckpointName
    if ($clean -and $prev -and -not $new) { return 'Published' }
    if (-not $clean -and $prev -and $new) { return 'Interrupted' }
    return 'NotPublished'
}

function Repair-CheckpointNames {
    # No 'clean' between the two renames: 'clean-new' exists only after its read-back passed, so finish the swap; with only 'clean-prev', rename it back.
    $clean = $script:CheckpointName
    if (-not (Test-Checkpoint $clean) -and (Test-Checkpoint $OldCheckpointName)) {
        if (Test-Checkpoint $NewCheckpointName) {
            Write-Warning "An interrupted refresh left no '$clean': finishing it ('$NewCheckpointName' becomes '$clean')."
            Rename-VMCheckpoint -VMName $script:VmName -Name $NewCheckpointName -NewName $clean
        } else {
            Write-Warning "An interrupted refresh left no '$clean': renaming '$OldCheckpointName' back."
            Rename-VMCheckpoint -VMName $script:VmName -Name $OldCheckpointName -NewName $clean
        }
    }
    if (-not (Test-Checkpoint $clean)) { return }
    foreach ($stray in $NewCheckpointName, $OldCheckpointName) { Remove-StrayCheckpoint $stray }
}

function Assert-UpdateOnlyReady {
    # Fails before touching anything; first repairs the checkpoint names an interrupted run left behind.
    if (-not (Get-VM -Name $script:VmName -ErrorAction SilentlyContinue)) { throw "VM '$($script:VmName)' not found: run without -UpdateOnly." }
    Repair-CheckpointNames
    if (-not (Test-Checkpoint $script:CheckpointName)) { throw "Checkpoint '$($script:CheckpointName)' not found: run without -UpdateOnly." }
}

function Publish-NewCheckpoint {
    # The names swap first (instant), the slow disk merge of the old image comes last: there is always a usable 'clean'.
    Stop-VM -Name $script:VmName -TurnOff -Force
    Rename-VMCheckpoint -VMName $script:VmName -Name $script:CheckpointName -NewName $OldCheckpointName
    Rename-VMCheckpoint -VMName $script:VmName -Name $NewCheckpointName -NewName $script:CheckpointName
    try { Remove-StrayCheckpoint $OldCheckpointName }
    catch { Write-Warning "Could not remove '$OldCheckpointName' (the next -UpdateOnly removes it): $($_.Exception.Message)" }
}

function Restore-OldClean {
    # A failure here is only a warning: it must never hide the error (or Ctrl+C) that got us here. The next run repairs the names.
    try {
        $state = Get-SwapState
        Repair-CheckpointNames
        Restore-CleanCheckpoint -Quiet
        $name = $script:CheckpointName
        if ($state -eq 'NotPublished') { Write-Warning "Refresh not published: the VM was restored to the old '$name'." }
        else { Write-Warning "The refresh was already swapped in: the VM was restored to the NEW '$name' (state: $state); nothing to redo." }
    } catch { Write-Warning "Could not restore the old '$($script:CheckpointName)': $($_.Exception.Message)" }
}

function Read-RefreshInfo {
    # Best effort: the checkpoint is taken, so a guest that will not answer now must not stop the publish.
    param([pscredential] $Credential)
    $info = @{ Version = 'unknown'; License = [pscustomobject]@{ Days = $null; State = 'unknown' } }
    try {
        $info.Version = Get-GuestWindowsVersion -Credential $Credential
        $session = New-VmSession -Credential $Credential
        try { $info.License = Get-GuestLicense -Session $session } finally { Remove-PSSession $session -ErrorAction SilentlyContinue }
    } catch { Write-Warning "Could not read the Windows version or licence after the refresh: $($_.Exception.Message)" }
    return $info
}

function Update-CleanImage {
    # Monthly refresh: the old image plus this month's updates, frozen again. Anything short of a published 'clean' (error or Ctrl+C) restores the old one.
    param([pscredential] $Credential)
    Assert-UpdateOnlyReady
    $published = $false
    try {
        Restore-CleanCheckpoint -Start
        Wait-Desktop -Credential $Credential -BootedBefore ([datetime]::MinValue) -TimeoutMin $UpdateOnlyDesktopTimeoutMin -StallLimitMin $UpdateRebootStallMin
        Enable-GuestWindowsUpdate -Credential $Credential
        Invoke-OnlineBuild -Credential $Credential
        Disable-GuestWindowsUpdate -Credential $Credential
        Complete-Build -Credential $Credential -CheckpointName $NewCheckpointName
        $script:RefreshInfo = Read-RefreshInfo -Credential $Credential
        Publish-NewCheckpoint
        $published = $true
    } finally {
        if (-not $published) { Restore-OldClean }
    }
}

function Invoke-UpdateOnly {
    param([pscredential] $Credential)
    $started = Get-Date
    Update-CleanImage -Credential $Credential
    foreach ($w in $script:UpdateWarnings) { Write-Warning "Refresh published with a problem: $w" }
    Write-LicenseNote -License $script:RefreshInfo.License
    Write-Host ("Done. Checkpoint '{0}' replaced; the VM is stopped. Windows: {1}. Windows Update is off. Took {2:N0} minutes." -f $script:CheckpointName, $script:RefreshInfo.Version, ((Get-Date) - $started).TotalMinutes)
}

# ---- main ----
if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced for tests: functions only
if ($UpdateOnly -and ($Force -or $IsoPath -or $SkipUpdates)) { throw '-UpdateOnly cannot be combined with -Force, -IsoPath or -SkipUpdates (it never rebuilds or downloads, and exists to install updates).' }
if ($UpdateOnly) {
    $updateCredential = Get-VmCredential
    Enter-VmLock
    try { Invoke-UpdateOnly -Credential $updateCredential } finally { Exit-VmLock }
    return
}
$credential = Get-VmCredential
$iso = if ($IsoPath) { $IsoPath } else { Get-EvalIso -Dir (Join-Path $VmRoot 'media') }
$answerIso = Join-Path $VmRoot 'media\answer.iso'
Enter-VmLock
try {
    # Everything that can fail cheaply runs before the old VM is deleted. An interrupted build leaves the old answer ISO
    # in the VM's DVD drive, which locks the file, so it is ejected first.
    if (Get-VM -Name $script:VmName -ErrorAction SilentlyContinue) { Get-VMDvdDrive -VMName $script:VmName | Where-Object Path -eq $answerIso | Set-VMDvdDrive -Path $null }
    New-AnswerIso -Dir (Join-Path $VmRoot 'media') -IsoFile $answerIso -Credential $credential
    Remove-ExistingVm
    New-Item -ItemType Directory -Force -Path (Join-Path $VmRoot 'vm') | Out-Null
    New-VmShell -VhdPath (Join-Path $VmRoot 'vm\disk.vhdx') -InstallIso $iso -AnswerIso $answerIso

    Write-Host 'Starting the install (no window; takes 15-30 minutes; Ctrl+C stops waiting, the VM keeps installing)...'
    Start-VM -Name $script:VmName
    Send-BootKeys
    Wait-Provisioned -Credential $credential
    Complete-Guest -Credential $credential
    Invoke-OnlineBuild -Credential $credential
    Disable-GuestWindowsUpdate -Credential $credential
    Complete-Build -Credential $credential
    $windowsVersion = Get-GuestWindowsVersion -Credential $credential
    Stop-VM -Name $script:VmName -TurnOff -Force
    if (-not $IsoPath -and -not $KeepIso) { Remove-Item $iso -Force -ErrorAction SilentlyContinue; Write-Host "Deleted the downloaded ISO ($iso); -KeepIso keeps it." }
    Write-Host "Done. Checkpoint '$($script:CheckpointName)' taken; the VM is stopped. Windows: $windowsVersion. Windows Update is off; evaluation licence runs out in 90 days."
} finally {
    # The answer ISO holds the VM password: detach it (if the VM still holds it) and delete it on every path.
    if (Get-VM -Name $script:VmName -ErrorAction SilentlyContinue) { Get-VMDvdDrive -VMName $script:VmName -ErrorAction SilentlyContinue | Set-VMDvdDrive -Path $null -ErrorAction SilentlyContinue }
    Remove-Item $answerIso -Force -ErrorAction SilentlyContinue
    Exit-VmLock
}
