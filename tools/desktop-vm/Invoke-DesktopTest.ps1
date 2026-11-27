<#
.SYNOPSIS
  Desktop acceptance run in the headless test VM (Slice 163): restore 'clean', install the dev MSIX, run the checks,
  restore 'clean' again. Nothing is installed on, or shown by, the host.
.EXAMPLE
  .\Invoke-DesktopTest.ps1 -App rtst
  .\Invoke-DesktopTest.ps1 -App rtst -Check rtst-panel-screenshot -WithPack supertonic-3 -CopyOut D:\ReadTheStupidText-worktrees\_shots   # Slice 215: pack copied into the offline guest
  .\Invoke-DesktopTest.ps1 -App dino -Package D:\path\DinoReader.App_1.0.0.0_x64.msix
  .\Invoke-DesktopTest.ps1 -App auto      # only the apps the change touches (tools/test-targets.ps1), skipped ones are listed
.NOTES
  Slice 180: without -Package the MSIX is the one built for the current build key (Build-TestPackage.ps1; built first, outside
  the VM lock, when no build with this key exists), never "the newest file", so a stale package is not tested by accident.
  Exit code 0 when every check passes, 1 otherwise. -Sabotage KillApp stops the app after its launch check, so the
  remaining checks must fail (proof that a check can fail). Checks run in order and share the one running app.
  Slice 194 (opt-in): -Check <name> runs the app's first (launch) check, then only that check (a file in checks\, even one
  not in the default list); -CopyOut <host folder> copies the guest's results\*.png there before the VM is restored.
  Slice 207 (preflight): a check script may carry a header line `# needs: network, voice-pack`. The guest's network (an HTTPS request to the site, 10 s)
  is tested once the guest answers, before the package is copied, and only when a selected check declares `
  network` (the VM is offline by design, so declare it only for a check that truly needs the internet); the app's voice pack folder, which exists only once the package is installed,
  is tested right after the install and before the first check runs. Every declared probe runs before the decision; all missing items print in one `VM PREFLIGHT NO-GO: <items>` line and exits 3 without running any check. -SkipPreflight bypasses it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('rtst', 'dino', 'auto')] [string] $App,
    [string] $Package,
    [string] $VmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm',
    [ValidateSet('None', 'KillApp')] [string] $Sabotage = 'None',
    [switch] $KeepRunning,
    [string] $Check,
    [string] $CopyOut,
    [switch] $SkipPreflight,
    # Slice 215: copy a host copy of this voice pack (the packs release asset, cached once under <worktrees>\_builds\packs) into the guest app's LocalCache\packs after the install.
    [string] $WithPack,
    # Slice 434: host folder made by .github/scripts/fetch-shelf-packages.mjs (<id>\story-v<n>.dino per book; run it with --include-free, the VM is offline); each package is copied into the guest app's LocalState\full-shelf\<id>.dino after the install, for the dino-store-shots full shelf.
    [string] $WithShelfPackages,
    # Slice 225 item 20: the VM start needs this much free physical memory (GB); below it the script stops at once with a plain message, before the build and before Start-VM.
    # (Two starts failed late with 0x8007000E and 0x800705AA at about 6.9 GB free on 2026-10-31.)
    [double] $MinFreeMemoryGB = 8
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
$script:ActiveVmRoot = $VmRoot
. "$PSScriptRoot\..\emulator\build-cache.ps1"

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    if ($Package) { throw '-Package names one MSIX; it cannot be combined with -App auto.' }
    if ($Check -or $CopyOut) { throw '-Check and -CopyOut belong to one app; name it with -App rtst or -App dino, not -App auto.' }
    if ($WithPack) { throw '-WithPack belongs to one app (a pack id is per app); name it with -App rtst or -App dino, not -App auto.' }
    if ($WithShelfPackages) { throw '-WithShelfPackages belongs to the dino app; name it with -App dino, not -App auto.' }
    $touched = @(& "$PSScriptRoot\..\test-targets.ps1" -Platform windows -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO WINDOWS APP IS TOUCHED BY THIS CHANGE: no desktop test ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        & $PSCommandPath -App $name -VmRoot $VmRoot -Sabotage $Sabotage -KeepRunning:$KeepRunning -SkipPreflight:$SkipPreflight -MinFreeMemoryGB $MinFreeMemoryGB
        # A real failed check (1) outranks a NO-GO (3) from another app, whatever the order, so a caller that reads 3 as "environment, ask" never misses a failure.
        if ($LASTEXITCODE -eq $script:PreflightExitCode) { if ($worst -eq 0) { $worst = $script:PreflightExitCode } }
        elseif ($LASTEXITCODE -ne 0) { $worst = 1 }
    }
    exit $worst
}

# One row per product: the package identity name, where a fresh MSIX is usually found and the ordered checks.
$Apps = @{
    rtst = @{ Name = 'AshkanSirous.ReadTheStupidText'; Exe = 'ReadTheStupidText.App'; Checks = @('rtst-language-rows', 'rtst-launch', 'rtst-tray-icon', 'rtst-hotkey-read', 'rtst-selection-hotkey', 'rtst-panel-speed', 'rtst-startup-task') }
    dino = @{ Name = 'AshkanSirous.DinoReader'; Exe = 'DinoReader.App'; Checks = @('dino-onboarding-keys', 'dino-onboarding', 'dino-launch', 'dino-open-book', 'dino-page-turn', 'dino-settings', 'dino-keyboard', 'dino-gate', 'dino-record', 'dino-window', 'dino-desktop-polish', 'dino-reorder') }
}
$AppProfile = $Apps[$App]
if ($Check) {
    if ($Check -notmatch "^$App-[a-z0-9-]+$") { throw "-Check ${Check}: the name must start with '$App-' (a check of this app) and use only lower-case letters, digits and dashes." }
    if (-not (Test-Path "$PSScriptRoot\checks\$Check.ps1")) { throw "-Check ${Check}: no such file in tools\desktop-vm\checks." }
    $prerequisites = @(Get-CheckAfter "$PSScriptRoot\checks\$Check.ps1" "$PSScriptRoot\checks")   # Slice 269: `# after:` header
    $AppProfile.Checks = @($AppProfile.Checks[0]) + @(@($prerequisites) + @($Check) | Where-Object { $_ -ne $AppProfile.Checks[0] } | Select-Object -Unique)
}

function Find-Package {
    if ($Package) { return (Resolve-Path $Package).Path }
    $pattern = if ($App -eq 'rtst') { 'ReadTheStupidText.App_*.msix' } else { 'DinoReader.App_*.msix' }
    $cached = Find-CachedBuild (Get-BuildDir $App 'windows' (Get-BuildKey)) $pattern
    if ($cached) { Write-Host "Using the cached $App MSIX for this build key: $($cached.FullName)"; return $cached.FullName }
    Write-Host "No $App MSIX built for this build key; building it (outside the VM lock)..."
    $built = @(& "$PSScriptRoot\Build-TestPackage.ps1" -App $App) | Select-Object -Last 1
    if (-not $built -or -not (Test-Path $built)) { throw "Build-TestPackage.ps1 -App $App did not return a package path." }
    return $built
}

function Copy-ToGuest {
    param($Session, [string] $Msix)
    Invoke-Command -Session $Session -ScriptBlock {
        param($dir)
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path "$dir\pkg\deps", "$dir\checks", "$dir\results" | Out-Null
    } -ArgumentList $script:GuestWorkDir
    Write-Host 'Signing a copy of the package with a throw-away certificate...'
    $signDir = Join-Path $VmRoot 'sign'
    try {
        $signed = New-SignedCopy -Msix $Msix -WorkDir $signDir
        Write-Host 'Copying the package and checks into the VM...'
        Copy-Item -ToSession $Session -Path $signed.Msix -Destination "$($script:GuestWorkDir)\pkg\app.msix"
        Copy-Item -ToSession $Session -Path $signed.Cer -Destination "$($script:GuestWorkDir)\pkg\signer.cer"
    } finally { Remove-Item $signDir -Recurse -Force -ErrorAction SilentlyContinue }
    $deps = Join-Path (Split-Path $Msix) 'Dependencies\x64'
    if (Test-Path $deps) { Copy-Item -ToSession $Session -Path "$deps\*.msix" -Destination "$($script:GuestWorkDir)\pkg\deps\" }
    Copy-Item -ToSession $Session -Path "$PSScriptRoot\checks\*" -Destination "$($script:GuestWorkDir)\checks\" -Recurse
}

function Install-InGuest {
    param($Session, [string] $Name)
    return Invoke-Command -Session $Session -ArgumentList $script:GuestWorkDir, $Name -ScriptBlock {
        param($dir, $name)
        $deps = @(Get-ChildItem "$dir\pkg\deps" -Filter *.msix | ForEach-Object FullName)
        Import-Certificate -FilePath "$dir\pkg\signer.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        $install = @{ Path = "$dir\pkg\app.msix"; ErrorAction = 'Stop' }
        if ($deps.Count) { $install.DependencyPath = $deps }
        Add-AppxPackage @install
        # Slice 179: counting off for every harness run (UsageEligibility.NoUsageVariable); a task started later reads the machine value.
        [Environment]::SetEnvironmentVariable('READTHESTUPIDTEXT_NO_USAGE', '1', 'Machine')
        $pkg = Get-AppxPackage -Name $name
        if (-not $pkg) { throw "Package $name is not installed after Add-AppxPackage." }
        return $pkg.Version
    }
}

function Get-LatestShelfPackages {
    # The highest story-v<n>.dino of each <id> folder under $HostDir (fetch-shelf-packages.mjs writes <id>\story-v<n>.dino); throws when none is found or a package sits directly in $HostDir (its folder name is no book id).
    param([string] $HostDir)
    $root = (Resolve-Path $HostDir -ErrorAction Stop).Path.TrimEnd('\')
    $files = @(Get-ChildItem $root -Recurse -Filter *.dino -File)
    if (-not $files.Count) { throw "-WithShelfPackages ${HostDir}: no .dino package in it (run .github/scripts/fetch-shelf-packages.mjs first)." }
    $loose = @($files | Where-Object { $_.Directory.FullName.TrimEnd('\') -eq $root })
    if ($loose.Count) { throw "-WithShelfPackages ${HostDir}: $($loose[0].Name) sits directly in the folder; each package must be in its own <book id> folder." }
    $version = { if ($_.BaseName -match '-v(\d+)$') { [int]$Matches[1] } else { 0 } }
    return @($files | Group-Object { $_.Directory.FullName } | ForEach-Object { $_.Group | Sort-Object $version -Descending | Select-Object -First 1 })
}

function Copy-ShelfPackagesToGuest {
    # Slice 434: all eight books' packages for the full-shelf hook (it imports them from <app data>\full-shelf\<id>.dino; the VM has no network, so no book is downloaded). Never builds a package.
    param($Session, [string] $PackageName, [string] $HostDir)
    $files = @(Get-LatestShelfPackages $HostDir)
    $stage = "$($script:GuestWorkDir)\shelf-packages"
    Invoke-Command -Session $Session -ArgumentList $stage -ScriptBlock { param($dir) Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path $dir | Out-Null }   # no leftover of an earlier run
    foreach ($f in $files) { Copy-Item -ToSession $Session -Path $f.FullName -Destination "$stage\$($f.Directory.Name).dino" -Force }
    $listing = Join-Path $HostDir 'shelf-listing.json'   # id -> ages and sale fields from the live list (fetch-shelf-packages.mjs); the guest's test store reads it for the Store tab's "Ages x-y" and each card's price or Free
    if (Test-Path $listing) { Copy-Item -ToSession $Session -Path $listing -Destination "$stage\shelf-listing.json" -Force }
    Invoke-Command -Session $Session -ArgumentList $PackageName, $stage -ScriptBlock {
        param($name, $stage)
        $pkgDir = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter "${name}_*" -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $pkgDir) { throw "no Packages\${name}_* folder in the guest: the package is not installed for this user" }
        $target = Join-Path $pkgDir.FullName 'LocalState\full-shelf'
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        Move-Item "$stage\*" $target -Force
    }
    Write-Host "Copied $($files.Count) shelf packages into the VM."
}

function Copy-ResultFiles {
    # Slice 194: pulls the PNGs the checks left in the guest's results folder out to a host folder (before the reset to clean).
    param($Session, [string] $HostDir)
    New-Item -ItemType Directory -Force -Path $HostDir | Out-Null
    $files = @(Invoke-Command -Session $Session -ArgumentList $script:GuestWorkDir -ScriptBlock { param($dir) Get-ChildItem "$dir\results" -Filter *.png -ErrorAction SilentlyContinue | ForEach-Object FullName })
    $frames = @($files | Where-Object { (Split-Path $_ -Leaf) -match $script:FootageFramePattern })   # Slice 434: footage frames become one mp4 per clip, never all on the host disk at once
    foreach ($f in @($files | Where-Object { $frames -notcontains $_ })) { Copy-Item -FromSession $Session -Path $f -Destination $HostDir -Force; Write-Host "Copied $f to $HostDir" }
    $failedClips = @()
    foreach ($clip in @($frames | ForEach-Object { ((Split-Path $_ -Leaf) -replace $script:FootageFramePattern, '$1') } | Select-Object -Unique)) {
        try { Copy-FootageClipToMp4 -Session $Session -HostDir $HostDir -Clip $clip -Frames @($frames | Where-Object { (Split-Path $_ -Leaf) -like "footage-$clip-*" }) }
        catch { $failedClips += "$clip ($($_.Exception.Message))" }   # one failed clip must not drop the others before the reset to clean
    }
    if (-not $files.Count) { Write-Warning 'CopyOut: the checks left no PNG in the guest results folder.' }
    if ($failedClips.Count -gt 0) { throw "CopyOut: $($failedClips.Count) footage clip(s) were not encoded: $($failedClips -join '; ')" }
}

$script:FootageFramePattern = '^footage-([a-z]+)-\d{4}\.png$'
$script:FootageFps = 10   # the check's frame rate (dino-store-shots.ps1 $FootageFps)
$script:FootageOutputFps = 30

function Copy-FootageClipToMp4 {
    # Slice 434: copies one clip's frames to a scratch folder, encodes <clip>.mp4 (H.264, 1920 x 1080, yuv420p, 30 fps from the 10 fps frames) with the host's ffmpeg and deletes the frames, so the host
    # never holds more than one clip of PNGs. Throws when ffmpeg is missing or fails (the frames are then left in the scratch folder for a look).
    param($Session, [string] $HostDir, [string] $Clip, [string[]] $Frames)
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if (-not $ffmpeg) { throw 'CopyOut: ffmpeg is not on the host PATH, so the footage frames cannot be encoded (winget install Gyan.FFmpeg).' }
    $scratch = Join-Path $HostDir "frames-$Clip"
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    foreach ($f in $Frames) { Copy-Item -FromSession $Session -Path $f -Destination $scratch -Force }
    $out = Join-Path $HostDir "$Clip.mp4"
    & $ffmpeg.Source -y -loglevel error -framerate $script:FootageFps -i (Join-Path $scratch "footage-$Clip-%04d.png") -vf "fps=$script:FootageOutputFps,format=yuv420p" -c:v libx264 -crf 18 $out
    if ($LASTEXITCODE -ne 0) { throw "CopyOut: ffmpeg failed for the $Clip clip (exit $LASTEXITCODE); frames kept in $scratch" }
    Remove-Item $scratch -Recurse -Force
    Write-Host "Encoded $($Frames.Count) frames of $Clip to $out"
}

function Assert-GuestPreflight {
    # Slice 207: collects what the guest lacks among the needs the selected checks declare (see the header of their scripts); each probe runs only when some check declares it.
    # -Only limits it to some needs: the network is tested before the install, the voice pack after it (its folder exists only once the package is installed).
    # The missing items of an earlier call are kept, and -Final throws one NO-GO line listing all of them.
    param($Session, [string[]] $Only = $script:KnownNeeds, [switch] $Final)
    # Slice 215: the checks' headers are read once per run (not once per call), and the skip line is printed once, by the first call.
    if ($null -eq $script:SelectedNeeds) { $script:SelectedNeeds = @($AppProfile.Checks | ForEach-Object { Get-CheckNeeds "$PSScriptRoot\checks\$_.ps1" } | Select-Object -Unique) }
    $needs = @($script:SelectedNeeds | Where-Object { $Only -contains $_ })
    $skipped = $SkipPreflight -or $script:SelectedNeeds.Count -eq 0
    if ($skipped -and -not $script:PreflightSkipSaid) { Write-Host "Preflight: $(if ($SkipPreflight) { 'skipped (-SkipPreflight)' } else { 'no check declares a need' })."; $script:PreflightSkipSaid = $true }
    if (-not $skipped -and $needs.Count -gt 0) { $script:PreflightMissing += @(Get-PreflightMissing -Session $Session -Needs $needs -PackageName $AppProfile.Name -PackId $script:VoicePackIds[$App]) }
    if (-not $Final) { return }
    if ($script:PreflightMissing.Count -gt 0) { throw "$($script:PreflightNoGo): $($script:PreflightMissing -join ', ')" }
    Write-Host 'Preflight: ok.'
}

function Write-Results {
    param([object[]] $Results)
    foreach ($r in $Results) {
        $mark = if ($r.pass) { 'PASS' } else { 'FAIL' }
        Write-Host ("{0}  {1,-20} {2}" -f $mark, $r.check, $r.message)
    }
}

# Slice 225 item 20: pure, so the message is tested without a VM.
function Get-MemoryProblem([double] $FreeGB, [double] $NeedGB) {
    if ($FreeGB -ge $NeedGB) { return $null }
    return "needs $NeedGB GB free memory to start the VM, has $FreeGB GB. Close something (a second emulator, a build) or wait, then run it again."
}

# Review 3: a VM left running (-KeepRunning) already holds its RAM, so the free-memory guard would stop a start that needs no new memory. Unreadable state counts as not running.
function Test-VmRunning {
    try { return ((Get-VM -Name $script:VmName -ErrorAction Stop).State -eq 'Running') } catch { return $false }
}

# Slice 271 item 3 (Slice 269 item 17: the worktree's tmp\ was deleted during a run, cause not found): the listing is printed before the run, after each check and after the run, so the next
# deletion names its step. Never throws and never changes anything.
function Get-TmpListing {
    param([string] $TmpDir)
    if (-not (Test-Path -LiteralPath $TmpDir)) { return "tmp\ does not exist at $TmpDir" }
    try {
        $items = @(Get-ChildItem -LiteralPath $TmpDir -Force -ErrorAction Stop | Sort-Object Name | ForEach-Object { $_.Name })
        $stamp = (Get-Item -LiteralPath $TmpDir -Force).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
        return "tmp\ ($TmpDir, last written $stamp) holds $($items.Count) item(s): $(if ($items.Count -gt 0) { $items -join ', ' } else { '(empty)' })"
    } catch { return "tmp\ could not be listed: $($_.Exception.Message)" }
}

function Write-TmpListing {
    param([string] $When)
    Write-Host "TMPLIST ${When}: $(Get-TmpListing (Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'tmp'))"
}

function Get-FreeMemoryGB { return [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1) }   # FreePhysicalMemory is in KB

# ---- main ----
$memoryProblem = $null
if (Test-VmRunning) { Write-Host 'Free-memory guard skipped: the test VM is already running (it already holds its memory).' }
else { $memoryProblem = Get-MemoryProblem (Get-FreeMemoryGB) $MinFreeMemoryGB }
if ($memoryProblem) { Write-Host "$($script:PreflightNoGo): $memoryProblem"; exit $script:PreflightExitCode }
$msix = Find-Package
# Slice 434: the footage clips are encoded on the host at CopyOut, after a run of 10+ minutes; a missing ffmpeg is found now, before the VM starts, not when the results are about to be lost.
if ($CopyOut -and @($AppProfile.Checks) -contains 'dino-store-shots' -and -not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'dino-store-shots with -CopyOut needs ffmpeg on the host PATH to encode the footage clips (winget install Gyan.FFmpeg); the VM was not started.' }
# Slice 253 item 3: the selected checks are read against the guest screen and the built package before the VM is touched; a FAIL stops here (exit 1), a WARN is printed.
. "$PSScriptRoot\_dryrun.ps1"
if ((Invoke-CheckDryRun @($AppProfile.Checks) "$PSScriptRoot\checks" $msix) -gt 0) { Write-Host 'DRYRUN stopped the run: fix the check(s) above; the VM was not started.'; exit 1 }
$credential = Get-VmCredential
$results = @()
$noGo = $null
# Slice 284 item 3: this run marks its worktree as busy with its own uniquely named file in the git dir (the agents' `device-test-running*` marks), removed when the run ends;
# two runs on one worktree each remove only their own. Same two functions in tools/emulator/run-flows.ps1 (checked by tools/emulator/test-worktree-mark.ps1).
function New-WorktreeRunMark([string]$Owner, [string]$Root) {
    try {
        $gitDir = (& git -C $Root rev-parse --absolute-git-dir 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $gitDir) { return $null }
        # Slice 284 design (two strikes): a mark is stale only if its pid is dead, or its StartTime is readable and later than the mark's write time (a reused pid).
        # An unreadable StartTime (access denied) counts as live. Each mark is judged in its own try, and our own mark never depends on reading other processes.
        foreach ($old in @(Get-ChildItem -LiteralPath $gitDir -Filter 'device-test-running-*-*-*' -File -ErrorAction SilentlyContinue)) {
            try {
                if ($old.Name -notmatch '^device-test-running-.+-(\d+)-[0-9a-f]{8}$') { continue }
                $markOwner = Get-Process -Id ([int]$Matches[1]) -ErrorAction SilentlyContinue
                $stale = -not $markOwner
                if ($markOwner) { try { $stale = $markOwner.StartTime -gt $old.LastWriteTime } catch { $stale = $false } }
                if ($stale) { Remove-Item -LiteralPath $old.FullName -Force -ErrorAction SilentlyContinue }
            } catch { continue }
        }
    } catch { return $null }
    $path = Join-Path $gitDir ("device-test-running-$Owner-$PID-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    try { Set-Content -LiteralPath $path -Value "$Owner pid $PID started $((Get-Date).ToString('o'))" } catch { Write-Warning "Run mark not written ($($_.Exception.Message)); the run goes on without one."; return $null }
    return $path
}
function Remove-WorktreeRunMark([string]$Path) {
    if ($Path) { Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue }
}
# Slice 284 check pass 2: the dino-record key-R stage finds the app's microphone alerts by their text, read here from this worktree's English resx (the keys RecordPage.cs shows),
# so a reworded alert never silently breaks the stage. The guest has no repo: the texts go over as one base64 JSON argument; a missing key is passed as an error the stage fails with.
function Get-RecordAlertArgument {
    $resx = Join-Path $PSScriptRoot '..\..\src\Dino\DinoReader.App\Resources\Strings\AppResources.resx'
    $keys = @{ NoMic = 'Rec_NoMicTitle'; Failed = 'Rec_Failed' }
    $payload = @{ Error = $null; NoMic = $null; Failed = $null }
    try {
        $data = ([xml](Get-Content -LiteralPath $resx -Raw -Encoding UTF8)).root.data
        foreach ($name in $keys.Keys) {
            $entry = @($data | Where-Object { $_.name -eq $keys[$name] }) | Select-Object -First 1
            if (-not $entry -or -not $entry.value) { throw "resx key $($keys[$name]) is missing in $resx" }
            $payload[$name] = [string]$entry.value
        }
    } catch { $payload.Error = "cannot read the microphone alert texts: $($_.Exception.Message)" }
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress)))
}
$runMark = $null   # created below, once the quiet-machine registration is done and the try that owns its removal is next
$script:PreflightMissing = @()
$session = $null
$guestStart = $null
# Register (and wait for a quiet-machine timing run) before taking the VM lock, so a long wait never holds every other VM run up.
try { Write-TmpListing 'before the run' } catch { Write-Warning "Tmp listing failed: $($_.Exception.Message)" }   # before Start-Activity: a throw here must not leave the quiet-machine registration held
$activity = Start-Activity "vm-$App"
try { Enter-VmLock } catch { Stop-Activity $activity; throw }
try {
    $runMark = New-WorktreeRunMark 'desktop-test' $PSScriptRoot   # first statement of the try whose finally removes it (Slice 284 design, two strikes)
    Write-Host "Restoring '$($script:CheckpointName)' and starting the VM..."
    Restore-CleanCheckpoint -Start -Quiet
    $session = New-VmSession -Credential $credential
    $guestStart = Invoke-Command -Session $session -ScriptBlock { (Get-Date).AddMinutes(-2) }   # Slice 259 item 4: the guest clock, for the crash evidence below
    Assert-GuestPreflight -Session $session -Only @('network')
    Copy-ToGuest -Session $session -Msix $msix
    Write-Host 'Installing the package in the VM...'
    Write-LicenseNote -License (Get-GuestLicense -Session $session)
    $version = Install-InGuest -Session $session -Name $AppProfile.Name
    Write-Host "Installed $($AppProfile.Name) $version in the VM from $msix"
    if ($WithPack) { Copy-PackToGuest -Session $session -PackageName $AppProfile.Name -PackId $WithPack }  # Slice 215: after the install, before the pack probe
    if ($WithShelfPackages) { Copy-ShelfPackagesToGuest -Session $session -PackageName $AppProfile.Name -HostDir $WithShelfPackages }   # Slice 434
    Assert-GuestPreflight -Session $session -Only @('voice-pack') -Final
    foreach ($check in $AppProfile.Checks) {
        if ($Sabotage -eq 'KillApp' -and $check -ne $AppProfile.Checks[0]) {
            Invoke-Command -Session $session -ArgumentList $AppProfile.Exe -ScriptBlock { param($exe) Stop-Process -Name $exe -Force -ErrorAction SilentlyContinue }
        }
        Write-Host "Running check $check..."
        $checkArgs = @($AppProfile.Name)
        if ($check -eq 'dino-record') { $checkArgs += Get-RecordAlertArgument }
        $r = Invoke-InteractiveScript -Session $session -GuestScript "$($script:GuestWorkDir)\checks\$check.ps1" -ScriptArgs $checkArgs
        $message = if ($r.noResult) { "$($r.message); screen: $(Get-ScreenText (Save-VmScreenshot -Reason "check-$check"))" } else { $r.message }
        $results += [pscustomobject]@{ check = $check; pass = [bool]$r.pass; message = $message }
        Write-TmpListing "after check $check"
    }
    if ($CopyOut) { Copy-ResultFiles -Session $session -HostDir $CopyOut }
} catch {
    $failure = $_.Exception.Message
    if ($failure.StartsWith($script:PreflightNoGo)) { $noGo = $failure; Write-Host $noGo }
    else {
        $hasShot = $failure -match 'screen: \S'
        $shot = if ($hasShot) { $null } else { Save-VmScreenshot -Reason 'harness' }
        $results += [pscustomobject]@{ check = 'harness'; pass = $false; message = $(if ($hasShot) { $failure } else { "$failure; screen: $(Get-ScreenText $shot)" }) }
    }
} finally {
    # Slice 259 item 4: a failed check or harness error may be a dead app; keep what the guest knows (event log, WER, app trace tail) before the reset to clean wipes it.
    if ($session -and $guestStart -and -not $noGo -and $results.Where({ -not $_.pass }).Count -gt 0) {
        $evidenceDir = if ($CopyOut) { Join-Path $CopyOut 'crash-evidence' } else { Join-Path $VmRoot "crash-evidence\$App-$(Get-Date -Format 'yyyyMMdd-HHmmss')" }
        try { Copy-CrashEvidence -Session $session -HostDir $evidenceDir -ExeName $AppProfile.Exe -PackageName $AppProfile.Name -Since $guestStart } catch { Write-Warning "Crash evidence copy failed: $($_.Exception.Message)" }
    }
    if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
    if (-not $KeepRunning) {
        Write-Host "Restoring '$($script:CheckpointName)'..."
        try { Restore-CleanCheckpoint -Quiet } catch { Write-Warning "Reset to clean failed: $($_.Exception.Message)" }
    }
    try { Stop-Activity $activity } catch { Write-Warning "Stop-Activity failed: $($_.Exception.Message)" }
    try { Remove-WorktreeRunMark $runMark } catch { Write-Warning "Run mark removal failed: $($_.Exception.Message)" }
    try { Exit-VmLock } catch { Write-Warning "Exit-VmLock failed: $($_.Exception.Message)" }
    try { Write-TmpListing 'after the run' } catch { Write-Warning "Tmp listing failed: $($_.Exception.Message)" }   # a listing error must not mask the run's own failure
}
if ($noGo) { exit $script:PreflightExitCode }
Write-Results -Results $results
if ($results.Where({ -not $_.pass }).Count -gt 0) { exit 1 }
exit 0
