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
$script:LicenseWarnDays = 14
# A wait with no sign of progress for this long is a stuck screen nobody can answer: screenshot and throw (see Step-StallWatch).
$script:StallMin = 10
$script:ActiveVmRoot = $script:DefaultVmRoot
$script:ScreenshotWidth = 1024
$script:ScreenshotHeight = 768
$script:ScreenshotCimClass = 'Msvm_VirtualSystemManagementService'
$script:VirtualizationNamespace = 'root\virtualization\v2'
$script:VhdProgressBucketBytes = 16MB
# A guest using more CPU than this (percent, from Hyper-V) is working even if the disk is not growing (offline servicing, "Working on updates").
$script:BusyCpuPercent = 5

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

function Get-VmThumbnail {
    # Asks the host (no guest session needed) for the VM's screen as raw RGB565 bytes.
    $cs = Get-CimInstance -Namespace $script:VirtualizationNamespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$($script:VmName)'"
    $svc = Get-CimInstance -Namespace $script:VirtualizationNamespace -ClassName $script:ScreenshotCimClass
    $out = Invoke-CimMethod -InputObject $svc -MethodName GetVirtualSystemThumbnailImage -Arguments @{
        TargetSystem = $cs; WidthPixels = [uint16]$script:ScreenshotWidth; HeightPixels = [uint16]$script:ScreenshotHeight }
    $pixelBytes = $script:ScreenshotWidth * $script:ScreenshotHeight * 2
    if (-not $out.ImageData -or $out.ImageData.Length -lt $pixelBytes) { throw 'No screen image returned (is the VM running?).' }
    # The data carries a short header (4 bytes seen) before the pixels; the pixels are the last width*height*2 bytes.
    return [byte[]]$out.ImageData[($out.ImageData.Length - $pixelBytes)..($out.ImageData.Length - 1)]
}

function ConvertTo-Png {
    param([byte[]] $Rgb565, [string] $Path)
    Add-Type -AssemblyName System.Drawing
    $w = $script:ScreenshotWidth
    $h = $script:ScreenshotHeight
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
    try {
        $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
        $bits = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
        for ($y = 0; $y -lt $h; $y++) { [Runtime.InteropServices.Marshal]::Copy($Rgb565, $y * $w * 2, [IntPtr]($bits.Scan0.ToInt64() + $y * $bits.Stride), $w * 2) }
        $bmp.UnlockBits($bits)
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
}

function Save-VmScreenshot {
    # Saves the VM screen as <VmRoot>\screens\<time>-<reason>.png and returns the path; $null (with a warning) when it cannot.
    param([string] $Reason = 'screen')
    try {
        $dir = Join-Path $script:ActiveVmRoot 'screens'
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $safe = $Reason -replace '[^A-Za-z0-9_-]', '-'
        $path = Join-Path $dir ("{0}-{1}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $safe)
        ConvertTo-Png -Rgb565 (Get-VmThumbnail) -Path $path
        return $path
    } catch {
        Write-Warning "Could not save a screenshot: $($_.Exception.Message)"
        return $null
    }
}

function Get-ScreenText {
    # The screenshot path for a message, or a plain note when none could be saved.
    param([string] $Path)
    if ($Path) { return $Path }
    return '(screenshot unavailable)'
}

function Get-VmProgressToken {
    # Something that changes while the VM is working: power state, heartbeat, and the disk growing (an installing Windows writes to it).
    try {
        $vm = Get-VM -Name $script:VmName -ErrorAction Stop
        $disk = (Get-VMHardDiskDrive -VMName $script:VmName | Select-Object -First 1).Path
        $bucket = [long]((Get-Item $disk).Length / $script:VhdProgressBucketBytes)
        return "$($vm.State)|$($vm.Heartbeat)|$bucket"
    } catch { return 'unknown' }
}

function Test-VmBusy {
    try { return ((Get-VM -Name $script:VmName -ErrorAction Stop).CPUUsage -ge $script:BusyCpuPercent) } catch { return $false }
}

function New-StallWatch {
    # $LimitMin 0 means the shared $script:StallMin; reboot waits pass a longer limit.
    param([Parameter(Mandatory)] [string] $What, [int] $LimitMin = 0)
    $limit = if ($LimitMin -gt 0) { $LimitMin } else { $script:StallMin }
    return [pscustomobject]@{ What = $What; Token = $null; Since = Get-Date; LimitMin = $limit }
}

function Step-StallWatch {
    # Call on every poll with a token that changes when anything moves. No change for StallMin minutes: screenshot and throw.
    param([Parameter(Mandatory)] $Watch, [string] $Token = '')
    $full = "$(Get-VmProgressToken)|$Token"
    if ($full -ne $Watch.Token) { $Watch.Token = $full; $Watch.Since = Get-Date; return }
    if (Test-VmBusy) { $Watch.Since = Get-Date; return }
    if (((Get-Date) - $Watch.Since).TotalMinutes -lt $Watch.LimitMin) { return }
    $png = Save-VmScreenshot -Reason 'stalled'
    throw "stalled: $($Watch.What) (no progress for $($Watch.LimitMin) min); screen: $png"
}

function New-VmSession {
    # Opens a PowerShell Direct session, retrying until the guest is up (heartbeat, logon and WinRM-less channel).
    param([Parameter(Mandatory)] [pscredential] $Credential, [int] $TimeoutSec = $script:SessionTimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastError = $null
    $started = Get-Date
    $nextNote = $started.AddSeconds(30)
    $watch = New-StallWatch -What 'a PowerShell Direct session to the VM'
    while ((Get-Date) -lt $deadline) {
        try {
            return New-PSSession -VMName $script:VmName -Credential $Credential -ErrorAction Stop
        } catch {
            $lastError = $_.Exception.Message
            if ((Get-Date) -ge $nextNote) {
                Write-Host ("  waiting for the VM to accept a session, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds)
                $nextNote = (Get-Date).AddSeconds(30)
            }
            Step-StallWatch -Watch $watch
            Start-Sleep -Seconds 5
        }
    }
    $png = Save-VmScreenshot -Reason 'no-session'
    throw "No PowerShell Direct session to $($script:VmName) within $TimeoutSec s. Last error: $lastError; screen: $png"
}

function Restore-CleanCheckpoint {
    param([switch] $Start, [switch] $Quiet)
    $vm = Get-VM -Name $script:VmName -ErrorAction Stop
    if ($vm.State -notin 'Off', 'Saved') { Stop-VM -Name $script:VmName -TurnOff -Force }
    $snap = Get-VMCheckpoint -VMName $script:VmName -Name $script:CheckpointName -ErrorAction SilentlyContinue
    if (-not $snap) { throw "Checkpoint '$($script:CheckpointName)' not found. If a refresh was interrupted, run New-TestVm.ps1 -UpdateOnly: it repairs the checkpoint names. Otherwise rebuild with New-TestVm.ps1 -Force." }
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
        if (-not (Test-Path $resultFile)) { return [pscustomobject]@{ pass = $false; noResult = $true; message = "no result within $timeout s (script crashed or hung; its task was stopped; LastTaskResult=$lastResult)" } }
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

function Get-GuestLicense {
    # The Windows evaluation licence as the guest sees it (Slice 175): Days = whole days left (null when none is counting), State = a one-word summary.
    # SoftwareLicensingProduct.LicenseStatus: 1 licensed, 5 notification (an evaluation that never started, or ran out).
    param([Parameter(Mandatory)] $Session)
    try {
        return Invoke-Command -Session $Session -ErrorAction Stop -ScriptBlock {
        $product = Get-CimInstance SoftwareLicensingProduct -Filter "PartialProductKey IS NOT NULL AND ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f'" | Select-Object -First 1
        if (-not $product) { return [pscustomobject]@{ Status = $null; Days = $null; State = 'unknown' } }
        $days = if ($product -and $product.LicenseStatus -eq 1 -and $product.GracePeriodRemaining -gt 0) { [int][Math]::Floor($product.GracePeriodRemaining / 1440) } else { $null }
        $state = switch ($product.LicenseStatus) { 1 { if ($product.GracePeriodRemaining -eq 0) { 'licensed (no expiry)' } else { 'licensed' } } 5 { 'expired or never started (notification mode)' } default { "status $($product.LicenseStatus)" } }
        [pscustomobject]@{ Status = [int]$product.LicenseStatus; Days = $days; State = $state }
        }
    }
    catch {
        return [pscustomobject]@{ Status = $null; Days = $null; State = 'unknown' }
    }
}

function Write-LicenseNote {
    # Prints the remaining evaluation days, and a warning with the rebuild command under the limit or when the licence is not counting.
    param([Parameter(Mandatory)] $License, [int] $WarnDays = $script:LicenseWarnDays)
    $rebuild = 'tools\desktop-vm\New-TestVm.ps1 -Force'
    if ($License.State -eq 'unknown') { Write-Host 'VM licence: unknown (could not be read in the guest).'; return }
    if ($null -eq $License.Days -and $License.State -eq 'licensed (no expiry)') { Write-Host 'VM licence: licensed (no expiry).'; return }
    if ($null -eq $License.Days) { Write-Warning "VM licence: $($License.State). Short runs still work, but Windows shuts the VM down hourly once it has expired. Rebuild with $rebuild"; return }
    if ($License.Days -lt $WarnDays) { Write-Warning "VM licence: $($License.Days) day(s) left (warning under $WarnDays). Rebuild with $rebuild"; return }
    Write-Host "VM licence: $($License.Days) day(s) left."
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
