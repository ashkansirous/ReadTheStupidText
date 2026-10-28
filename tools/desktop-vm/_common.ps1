# Shared by New-TestVm.ps1, Reset-TestVm.ps1 and Invoke-DesktopTest.ps1 (dot-source it).
# Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md

$script:DefaultVmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm'
$script:VmName = 'rtst-desktop-test'
$script:CheckpointName = 'clean'
$script:VmUser = 'Administrator'
$script:SecretsFile = 'D:\secrets\readthestupidtext\secrets.env'
$script:SecretKey = 'DESKTOP_VM_PASSWORD'
$script:GuestWorkDir = 'C:\desktop-test'
$script:SessionTimeoutSec = 600
# The guest clock can lag the host by up to the evaluation period after a restore; start the certificate that far back.
$script:CertBackdateDays = 90
$script:LockName = 'Global\rtst-desktop-test-vm'

function Get-VmCredential {
    $password = $env:RTST_VM_PASSWORD
    if (-not $password) {
        if (-not (Test-Path $script:SecretsFile)) { throw "Password not found: set RTST_VM_PASSWORD or restore $script:SecretsFile (manage-secrets skill)." }
        $line = Select-String -Path $script:SecretsFile -Pattern "^$($script:SecretKey)=(.+)$" | Select-Object -First 1
        if (-not $line) { throw "$($script:SecretKey) is not in $script:SecretsFile." }
        $password = $line.Matches[0].Groups[1].Value.Trim()
    }
    $secure = ConvertTo-SecureString $password -AsPlainText -Force
    return New-Object System.Management.Automation.PSCredential($script:VmUser, $secure)
}

function Get-VmPassword {
    $cred = Get-VmCredential
    return $cred.GetNetworkCredential().Password
}

function New-VmSession {
    # Opens a PowerShell Direct session, retrying until the guest is up (heartbeat, logon and WinRM-less channel).
    param([Parameter(Mandatory)] [pscredential] $Credential, [int] $TimeoutSec = $script:SessionTimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastError = $null
    $started = Get-Date
    $nextNote = $started.AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        try {
            return New-PSSession -VMName $script:VmName -Credential $Credential -ErrorAction Stop
        } catch {
            $lastError = $_.Exception.Message
            if ((Get-Date) -ge $nextNote) {
                Write-Host ("  waiting for the VM to accept a session, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds)
                $nextNote = (Get-Date).AddSeconds(30)
            }
            Start-Sleep -Seconds 5
        }
    }
    throw "No PowerShell Direct session to $($script:VmName) within $TimeoutSec s. Last error: $lastError"
}

function Restore-CleanCheckpoint {
    param([switch] $Start, [switch] $Quiet)
    $vm = Get-VM -Name $script:VmName -ErrorAction Stop
    if ($vm.State -notin 'Off', 'Saved') { Stop-VM -Name $script:VmName -TurnOff -Force }
    $snap = Get-VMCheckpoint -VMName $script:VmName -Name $script:CheckpointName -ErrorAction Stop
    Restore-VMCheckpoint -VMCheckpoint $snap -Confirm:$false
    if ($Start) { Start-VM -Name $script:VmName | Out-Null }
    if (-not $Quiet) { Write-Host "VM restored to checkpoint '$($script:CheckpointName)'." }
}

function Invoke-InteractiveScript {
    # Runs a script file already inside the guest in the signed-in user's desktop session (UIA, tray and hotkeys need it;
    # a PowerShell Direct session is a separate non-interactive logon). Returns the parsed result file.
    param(
        [Parameter(Mandatory)] $Session,
        [Parameter(Mandatory)] [string] $GuestScript,
        [string[]] $ScriptArgs = @(),
        [int] $TimeoutSec = 240
    )
    $result = "$($script:GuestWorkDir)\results\$([IO.Path]::GetFileNameWithoutExtension($GuestScript)).json"
    $user = $script:VmUser
    return Invoke-Command -Session $Session -ArgumentList $GuestScript, $result, $ScriptArgs, $user, $TimeoutSec -ScriptBlock {
        param($script, $resultFile, $scriptArgs, $user, $timeout)
        $ErrorActionPreference = 'Stop'
        New-Item -ItemType Directory -Force -Path (Split-Path $resultFile) | Out-Null
        Remove-Item $resultFile -ErrorAction SilentlyContinue
        $argText = ($scriptArgs | ForEach-Object { "'$_'" }) -join ' '
        $cmd = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command `"& '$script' -ResultFile '$resultFile' $argText`""
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $cmd
        $principal = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\$user" -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Seconds ($timeout + 30))
        $name = 'rtst-check'
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
        Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
        Start-ScheduledTask -TaskName $name
        $deadline = (Get-Date).AddSeconds($timeout)
        while ((Get-Date) -lt $deadline -and -not (Test-Path $resultFile)) { Start-Sleep -Milliseconds 500 }
        $lastResult = (Get-ScheduledTaskInfo -TaskName $name -ErrorAction SilentlyContinue).LastTaskResult
        Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
        if (-not (Test-Path $resultFile)) { return [pscustomobject]@{ pass = $false; message = "no result within $timeout s (script crashed or hung; its task was stopped; LastTaskResult=$lastResult)" } }
        return Get-Content $resultFile -Raw | ConvertFrom-Json
    }
}

function Get-SignTool {
    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools", $env:NUGET_PACKAGES, "$env:NUGET_PACKAGES\microsoft.windows.sdk.buildtools") | Where-Object { $_ -and (Test-Path $_) }
    $tool = $roots | ForEach-Object { Get-ChildItem $_ -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue } |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw 'signtool.exe not found (Windows SDK or the Microsoft.Windows.SDK.BuildTools NuGet package).' }
    return $tool.FullName
}

function Get-MsixPublisher {
    param([string] $Msix)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Msix)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        $reader = New-Object IO.StreamReader($entry.Open())
        try { $xml = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        return $xml.Package.Identity.Publisher
    } finally { $zip.Dispose() }
}

function New-SignedCopy {
    # The CI MSIX is unsigned and the Store publisher is not in the "unsigned namespace", so Add-AppxPackage -AllowUnsigned
    # refuses it. Sign a COPY with a throw-away certificate whose subject is the manifest publisher; the public part is
    # trusted inside the VM only. The certificate and its private key are deleted from the host in the same call.
    param([Parameter(Mandatory)] [string] $Msix, [Parameter(Mandatory)] [string] $WorkDir)
    New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
    $signed = Join-Path $WorkDir 'app.msix'
    $cerPath = Join-Path $WorkDir 'signer.cer'
    Copy-Item $Msix $signed -Force
    $cert = New-SelfSignedCertificate -Type Custom -Subject (Get-MsixPublisher $Msix) -KeyUsage DigitalSignature -FriendlyName 'rtst-desktop-test (throw-away)' `
        -CertStoreLocation Cert:\CurrentUser\My -NotBefore (Get-Date).AddDays(-$script:CertBackdateDays) -NotAfter (Get-Date).AddDays(2) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    try {
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
        & (Get-SignTool) sign /fd SHA256 /sha1 $cert.Thumbprint $signed | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)." }
    } finally {
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey -Force -ErrorAction SilentlyContinue
    }
    return [pscustomobject]@{ Msix = $signed; Cer = $cerPath }
}

function Enter-VmLock {
    # One run at a time per host: the VM, its guest folder and its scheduled task are shared by every session.
    $script:VmLock = New-Object System.Threading.Mutex($false, $script:LockName)
    $started = Get-Date
    while ($true) {
        try { if ($script:VmLock.WaitOne(5000)) { return } } catch [System.Threading.AbandonedMutexException] { return }
        Write-Host ("  VM busy (another desktop test or build holds it), waiting {0:N0} s... (Ctrl+C to give up)" -f ((Get-Date) - $started).TotalSeconds)
    }
}

function Exit-VmLock {
    if ($script:VmLock) { try { $script:VmLock.ReleaseMutex() } catch { } $script:VmLock.Dispose(); $script:VmLock = $null }
}

function Invoke-FreshGuest {
    # Runs a script block in a brand-new session and closes it again; returns $null when the guest is not answering yet.
    # Never keep a session across a guest reboot: it goes Broken.
    param([Parameter(Mandatory)] [pscredential] $Credential, [Parameter(Mandatory)] [scriptblock] $Script)
    $session = $null
    try {
        $session = New-PSSession -VMName $script:VmName -Credential $Credential -ErrorAction Stop
        return Invoke-Command -Session $session -ScriptBlock $Script -ErrorAction Stop
    } catch {
        return $null
    } finally { if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue } }
}
