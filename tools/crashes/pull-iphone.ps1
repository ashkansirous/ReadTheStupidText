# Slice 312: read the iPhone's crash logs from this PC with libimobiledevice's `idevicecrashreport`, keep the .ips files
# under <worktree>/tmp/iphone-crashes, and print the newest crash of Dino and RTST (exception type, faulting thread's top frames).
#
#   pwsh -NoProfile -File tools/crashes/pull-iphone.ps1 [-ToolPath <folder with idevicecrashreport>] [-Udid <udid>] [-Network]
#                                                       [-OutDir <folder>] [-DsymDir <folder>] [-Frames 8]
#   pwsh -NoProfile -File tools/crashes/pull-iphone.ps1 -InputDir <folder of .ips files>     # no phone: read saved reports
#
# The pull uses `-k` (keep): the reports stay on the phone. Needs the phone unlocked, trusted ("Trust This Computer") and paired once
# over a cable (`idevicepair pair`); after that `-Network` works only if Wi-Fi sync is on for the phone (libimobiledevice pairs over
# the network for Apple TV only). When the tool is missing the script says what to install and exits 2.

[CmdletBinding()]
param(
    [string]$ToolPath,
    [string]$Udid,
    [switch]$Network,
    [string]$OutDir,
    [string]$InputDir,
    [string]$DsymDir,
    [int]$Frames = 8,
    [switch]$NoPathFallback
)

$ToolName = 'idevicecrashreport'
$ExitToolMissing = 2
$ExitPullFailed = 3
$ExitBadInput = 4
$CrashBugType = '309'
$BundleApps = [ordered]@{ 'uk.sirous.dinoreader' = 'Dino'; 'uk.sirous.readthestupidtext' = 'RTST' }
$InstallHint = @'
idevicecrashreport was not found (not on PATH, not under -ToolPath).
One-time step on this PC (nothing is installed by this script):
  1. Install Apple Devices (Microsoft Store) or iTunes: it provides the Apple Mobile Device Support driver the tool talks through.
  2. Download a Windows build of libimobiledevice (idevicecrashreport.exe, idevicepair.exe) and unzip it into a folder.
  3. Run: pwsh -NoProfile -File tools/crashes/pull-iphone.ps1 -ToolPath <that folder>
  First pairing needs the iPhone cable once: unlock the phone, tap Trust, then run `idevicepair pair`.
'@

function Find-CrashTool {
    param([string]$Folder)
    if ($Folder) {
        if (Test-Path -LiteralPath $Folder -PathType Leaf) { return $Folder }
        foreach ($name in @("$ToolName.exe", $ToolName)) {
            $candidate = Join-Path $Folder $name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    if ($NoPathFallback) { return $null }
    $found = Get-Command $ToolName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.Source }
    return $null
}

# An .ips file is a one-line JSON header, then the JSON body of the report.
function Read-Ips {
    param([string]$Path)
    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($lines.Count -lt 2) { Write-Warning "skipped $(Split-Path -Leaf $Path): not a header line plus a JSON body"; return $null }
    $header = $lines[0]
    $body = ($lines | Select-Object -Skip 1) -join "`n"
    try {
        $meta = $header | ConvertFrom-Json
        $report = $body | ConvertFrom-Json
    } catch { Write-Warning "skipped $(Split-Path -Leaf $Path): $($_.Exception.Message)"; return $null }
    [pscustomobject]@{ Path = $Path; Meta = $meta; Report = $report }
}

function Get-BundleId {
    param($Ips)
    if ($Ips.Report.bundleID) { return [string]$Ips.Report.bundleID }
    return [string]$Ips.Meta.bundleID
}

function Get-CrashTime {
    param($Ips)
    $stamp = if ($Ips.Report.captureTime) { $Ips.Report.captureTime } else { $Ips.Meta.timestamp }
    $parsed = [datetimeoffset]::MinValue
    $text = ([string]$stamp) -replace ' ?([+-]\d{2})(\d{2})$', ' $1:$2'
    $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal
    if ([datetimeoffset]::TryParse($text, [cultureinfo]::InvariantCulture, $styles, [ref]$parsed)) { return $parsed }
    return [datetimeoffset]::MinValue
}

function Get-FaultingFrames {
    param($Ips, [int]$Count)
    $report = $Ips.Report
    $thread = $null
    if ($null -ne $report.faultingThread -and $report.threads) { $thread = $report.threads[[int]$report.faultingThread] }
    if (-not $thread -and $report.threads) { $thread = @($report.threads | Where-Object { $_.triggered }) | Select-Object -First 1 }
    if (-not $thread) { return @('no faulting thread in report') }
    $images = @($report.usedImages)
    $frames = @($thread.frames | Select-Object -First $Count)
    if ($frames.Count -eq 0) { return @('no frames') }
    $frames | ForEach-Object {
        $image = if ($null -ne $_.imageIndex -and $_.imageIndex -lt $images.Count -and $images[$_.imageIndex].name) { [string]$images[$_.imageIndex].name } else { '?' }
        $symbol = if ($_.symbol) { [string]$_.symbol } elseif ($null -ne $_.imageOffset) { '0x{0:x}' -f [int64]$_.imageOffset } else { '?' }
        '{0} {1}' -f $image, $symbol
    }
}

function Get-ExceptionText {
    param($Ips)
    $exception = $Ips.Report.exception
    if (-not $exception) { return 'no exception block' }
    $text = [string]$exception.type
    if ($exception.signal) { $text += " ($($exception.signal))" }
    if ($Ips.Report.termination.indicator) { $text += "; terminated: $($Ips.Report.termination.indicator)" }
    return $text
}

function Test-IsCrash {
    param($Ips)
    return ([string]$Ips.Meta.bug_type -eq $CrashBugType) -or [bool]$Ips.Report.exception
}

function Get-NewestPerApp {
    param($Reports)
    $result = [ordered]@{}
    foreach ($bundle in $BundleApps.Keys) {
        $mine = @($Reports | Where-Object { (Get-BundleId $_) -eq $bundle -and (Test-IsCrash $_) })
        if ($mine.Count -gt 0) { $result[$BundleApps[$bundle]] = $mine | Sort-Object { Get-CrashTime $_ } -Descending | Select-Object -First 1 }
        else { $result[$BundleApps[$bundle]] = $null }
    }
    return $result
}

function Write-AppCrash {
    param([string]$App, $Ips, [int]$Count, [string]$DsymFolder)
    if (-not $Ips) { Write-Output "${App}: no crash report on the phone"; return }
    Write-Output ("{0}: {1}  {2}  v{3}" -f $App, (Get-CrashTime $Ips).ToString('yyyy-MM-dd HH:mm:ss'), (Split-Path -Leaf $Ips.Path), $Ips.Report.bundleInfo.CFBundleShortVersionString)
    Write-Output ("  exception: {0}" -f (Get-ExceptionText $Ips))
    Write-Output "  faulting thread, top $Count frames:"
    Get-FaultingFrames $Ips $Count | ForEach-Object { Write-Output "    $_" }
    $why = if ($DsymFolder) { "-DsymDir '$DsymFolder' is ignored, this script does not symbolicate" } else { 'no dSYM folder given (Slice 243 keeps none yet)' }
    Write-Output "  not symbolicated: $why; frames show the names in the report or raw offsets."
}

function Invoke-Pull {
    param([string]$Tool, [string]$Target)
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    $toolArgs = @('-k')
    if ($Udid) { $toolArgs += @('-u', $Udid) }
    if ($Network) { $toolArgs += '-n' }
    $toolArgs += $Target
    & $Tool @toolArgs | Out-Host
    return $LASTEXITCODE
}

function Invoke-Main {
    $source = $InputDir
    if ($source) {
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { Write-Output "-InputDir '$source' is not a folder."; exit $ExitBadInput }
        if ($Udid -or $Network) { Write-Warning '-Udid and -Network are ignored with -InputDir (nothing is pulled).' }
    } else {
        $tool = Find-CrashTool $ToolPath
        if (-not $tool) { Write-Output $InstallHint; exit $ExitToolMissing }
        $base = if ($OutDir) { $OutDir } else { Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'tmp/iphone-crashes' }
        $source = Join-Path $base (Get-Date -Format 'yyyyMMdd-HHmmss')
        $code = Invoke-Pull $tool $source
        if ($code -ne 0) { Write-Output "$ToolName failed (exit $code): unlock the phone, tap Trust, run idevicepair pair once over the cable; for -Network turn on Wi-Fi sync."; exit $ExitPullFailed }
    }
    $files = @(Get-ChildItem -LiteralPath $source -Filter *.ips -Recurse -File)
    if ($files.Count -eq 0) { Write-Warning "no .ips files found in ${source}: nothing was pulled (phone has no reports, or the tool copied nothing)." }
    $reports = @($files | ForEach-Object { Read-Ips $_.FullName } | Where-Object { $_ })
    Write-Output ("{0} report(s) read from {1}" -f $reports.Count, $source)
    if ($reports.Count -lt $files.Count) { Write-Output ("{0} .ips file(s) could not be read and were skipped (see warnings): the newest crash may be among them." -f ($files.Count - $reports.Count)) }
    $newest = Get-NewestPerApp $reports
    foreach ($app in $newest.Keys) { Write-AppCrash $app $newest[$app] $Frames $DsymDir }
}

if ($MyInvocation.InvocationName -ne '.') { Invoke-Main }
