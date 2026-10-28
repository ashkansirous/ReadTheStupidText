<#
.SYNOPSIS
  Builds the headless Windows 11 test VM from scratch (Slice 163) and takes the 'clean' checkpoint.
.DESCRIPTION
  Downloads the current Windows 11 Enterprise evaluation ISO (90-day licence), builds an answer-file DVD, creates a
  Gen2 VM (4 vCPU, 6 GB, no network adapter, no window), installs unattended, signs the VM user in automatically,
  turns Developer Mode on and checkpoints the running desktop as 'clean'. Needs Hyper-V Administrators, not elevation.
  Rebuild with -Force when the evaluation expires (about every 90 days) or the VM is damaged.
#>
[CmdletBinding()]
param(
    [string] $VmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm',
    [string] $IsoPath,
    [switch] $Force,
    [int] $InstallTimeoutMin = 75
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$EvalCenterPage = 'https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise'
$EvalIsoPattern = 'Windows 11 Enterprise ISO 64-bit \(en-US\)[^>]*href="(https://go\.microsoft\.com/fwlink/[^"]+)"'
$VhdSizeBytes = 64GB

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
    # "Press any key to boot from CD or DVD": tap Space through the virtual keyboard for the first minute.
    $cs = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$($script:VmName)'"
    $kb = Get-CimAssociatedInstance -InputObject $cs -ResultClassName Msvm_Keyboard
    1..20 | ForEach-Object { Invoke-CimMethod -InputObject $kb -MethodName TypeKey -Arguments @{ keyCode = 32 } | Out-Null; Start-Sleep -Seconds 3 }
}

function Wait-Provisioned {
    # A fresh session per poll: Windows may reboot again after first logon, and a session held across that goes Broken.
    param([pscredential] $Credential)
    $started = Get-Date
    $deadline = $started.AddMinutes($InstallTimeoutMin + 15)
    $nextNote = $started
    while ((Get-Date) -lt $deadline) {
        $ready = Invoke-FreshGuest -Credential $Credential -Script { (Test-Path C:\Windows\Temp\rtst-provisioned.txt) -and [bool](Get-Process explorer -ErrorAction SilentlyContinue) }
        if ($ready) { return }
        if ((Get-Date) -ge $nextNote) {
            Write-Host ("  waiting for Windows Setup and first logon, {0:N0} min" -f ((Get-Date) - $started).TotalMinutes)
            $nextNote = (Get-Date).AddMinutes(1)
        }
        Start-Sleep -Seconds 10
    }
    throw 'First-logon provisioning did not finish.'
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
    param([pscredential] $Credential, [datetime] $BootedBefore, [int] $StableSec = 60)
    $started = Get-Date
    $deadline = $started.AddMinutes(10)
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
        Write-Host ("  waiting for the rebooted desktop, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds)
        Start-Sleep -Seconds 10
    }
    throw 'The VM desktop did not come up (after the reboot) and stay up.'
}

function Complete-Build {
    # Detach the media, reboot once so the desktop is settled, and checkpoint it.
    param([pscredential] $Credential)
    Write-Host 'Detaching the media and rebooting once to settle...'
    Get-VMDvdDrive -VMName $script:VmName | Set-VMDvdDrive -Path $null
    Set-VMFirmware -VMName $script:VmName -FirstBootDevice (Get-VMHardDiskDrive -VMName $script:VmName)
    $bootedBefore = Get-GuestBootTime -Credential $Credential
    Restart-VM -Name $script:VmName -Force
    Wait-Desktop -Credential $Credential -BootedBefore $bootedBefore
    Checkpoint-VM -Name $script:VmName -SnapshotName $script:CheckpointName
}

# ---- main ----
if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced for tests: functions only
$credential = Get-VmCredential
$iso = if ($IsoPath) { $IsoPath } else { Get-EvalIso -Dir (Join-Path $VmRoot 'media') }
$answerIso = Join-Path $VmRoot 'media\answer.iso'
Enter-VmLock
try {
    # Everything that can fail cheaply runs before the old VM is deleted.
    New-AnswerIso -Dir (Join-Path $VmRoot 'media') -IsoFile $answerIso -Credential $credential
    Remove-ExistingVm
    New-Item -ItemType Directory -Force -Path (Join-Path $VmRoot 'vm') | Out-Null
    New-VmShell -VhdPath (Join-Path $VmRoot 'vm\disk.vhdx') -InstallIso $iso -AnswerIso $answerIso

    Write-Host 'Starting the install (no window; takes 15-30 minutes; Ctrl+C stops waiting, the VM keeps installing)...'
    Start-VM -Name $script:VmName
    Send-BootKeys
    Wait-Provisioned -Credential $credential
    Complete-Guest -Credential $credential
    Complete-Build -Credential $credential
    Stop-VM -Name $script:VmName -TurnOff -Force
    Write-Host "Done. Checkpoint '$($script:CheckpointName)' taken; the VM is stopped. Evaluation licence runs out in 90 days."
} finally {
    # The answer ISO holds the VM password: detach it (if the VM still holds it) and delete it on every path.
    if (Get-VM -Name $script:VmName -ErrorAction SilentlyContinue) { Get-VMDvdDrive -VMName $script:VmName -ErrorAction SilentlyContinue | Set-VMDvdDrive -Path $null -ErrorAction SilentlyContinue }
    Remove-Item $answerIso -Force -ErrorAction SilentlyContinue
    Exit-VmLock
}
