# Slice 253 item 3: a dry run of a VM check on the host, before the VM is started. Dot-sourced by Invoke-DesktopTest.ps1 and test-check-dryrun.ps1, not run.
# Slice 235 A and B needed 11 VM runs, most for harness bugs in a new checks\*.ps1: an expected size larger than the 1024 x 768 guest, a partial name lookup that took a tooltip for
# its button, fixed waits. Each of those can be seen in the source, so the builder stage reads the checks (and, when a built package exists, its manifest) first.
# A finding is a FAIL (the run stops before the VM) or a WARN (printed; a fixed wait of 3 s or more is explained with a `# fixed-wait: <why>` comment on its line).
$script:GuestWidth = 1024
$script:GuestHeight = 768
$script:FixedWaitWarnSeconds = 3
$script:SizeClampMarker = 'WorkingArea|Get-WorkArea|Set-ScreenResolution'

function New-DryRunFinding([string]$Severity, [string]$Line, [string]$Message) { return [pscustomobject]@{ Severity = $Severity; Line = $Line; Message = $Message } }

# Pure: findings for the text of one check file. $Name is only used in messages.
function Get-CheckSourceFindings([string]$Name, [string]$Text) {
    $findings = @()
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($Text, [ref]$null, [ref]$parseErrors)
    foreach ($e in @($parseErrors)) { $findings += New-DryRunFinding 'FAIL' $e.Extent.StartLineNumber "$Name does not parse: $($e.Message)" }
    $findings += @(Get-OversizedExpectations $Name $Text)
    $findings += @(Get-RawUiaLookups $Name $Text)
    $findings += @(Get-FixedWaits $Name $Text)
    return $findings
}

# Pure: a width or height constant larger than the guest screen, in a file that never clamps to the work area or sets a resolution.
function Get-OversizedExpectations([string]$Name, [string]$Text) {
    if ($Text -match $script:SizeClampMarker) { return @() }
    $found = @()
    $number = 0
    foreach ($line in ($Text -split "`r?`n")) {
        $number++
        $m = [regex]::Match($line, '^\s*\$(?<var>\w*(?<axis>Width|Height)\w*)\s*=\s*(?<value>\d{3,5})\s*(?:#.*)?$')
        if (-not $m.Success) { continue }
        $limit = if ($m.Groups['axis'].Value -eq 'Width') { $script:GuestWidth } else { $script:GuestHeight }
        if ([int]$m.Groups['value'].Value -gt $limit) {
            $found += New-DryRunFinding 'FAIL' $number "$Name expects `$$($m.Groups['var'].Value) = $($m.Groups['value'].Value), larger than the ${script:GuestWidth} x ${script:GuestHeight} guest screen, and never clamps it to the work area (Get-WorkArea / WorkingArea) or sets a resolution (Set-ScreenResolution)."
        }
    }
    return $found
}

# Pure: a hand-written FindFirst/FindAll in a check. WinUI tooltips sit in the UIA tree ahead of their control, so a lookup by name must go through Find-UiaExact / Find-UiaByName, which skip them.
function Get-RawUiaLookups([string]$Name, [string]$Text) {
    $found = @()
    $number = 0
    foreach ($line in ($Text -split "`r?`n")) {
        $number++
        if ($line -match '\.(FindFirst|FindAll)\(' -and $line -match 'NameProperty') {
            $found += New-DryRunFinding 'FAIL' $number "$Name looks an element up by NameProperty itself; a tooltip carries the button's name and comes first. Use Find-UiaExact or Find-UiaByName (checks\_lib.ps1)."
        }
    }
    return $found
}

# Pure: Start-Sleep of 3 s or more with no `# fixed-wait:` reason on the line. A check polls for the effect (Wait-Until, a log line) instead.
function Get-FixedWaits([string]$Name, [string]$Text) {
    $found = @()
    $number = 0
    foreach ($line in ($Text -split "`r?`n")) {
        $number++
        $m = [regex]::Match($line, 'Start-Sleep\s+(?:-Seconds\s+(?<s>\d+(?:\.\d+)?)|-Milliseconds\s+(?<ms>\d+))')
        if (-not $m.Success -or $line -match '#\s*fixed-wait:') { continue }
        $seconds = if ($m.Groups['s'].Success) { [double]$m.Groups['s'].Value } else { [double]$m.Groups['ms'].Value / 1000 }
        if ($seconds -ge $script:FixedWaitWarnSeconds) { $found += New-DryRunFinding 'WARN' $number "$Name waits a fixed $seconds s; poll for the effect (Wait-Until) or add a '# fixed-wait: <why>' comment." }
    }
    return $found
}

# The Executable of the first Application in an MSIX's AppxManifest.xml, without the extension ("DinoReader.App"); $null when the package or manifest cannot be read.
function Get-MsixExecutableName([string]$MsixPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($MsixPath)
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (-not $entry) { return $null }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $m = [regex]::Match($xml, '<Application\b[^>]*\bExecutable="(?<exe>[^"]+)"')
        if (-not $m.Success) { return $null }
        return [IO.Path]::GetFileNameWithoutExtension($m.Groups['exe'].Value)
    } catch { return $null }
    finally { if ($zip) { $zip.Dispose() } }
}

# Pure: the executable names a check looks up (Get-AppWindows 'X' / Get-Process -Name 'X') that are not the package's executable. Only names that look like an app executable (contain ".App") count.
function Get-WrongExecutableFindings([string]$Name, [string]$Text, [string]$ManifestExecutable) {
    if (-not $ManifestExecutable) { return @() }
    $found = @()
    $number = 0
    foreach ($line in ($Text -split "`r?`n")) {
        $number++
        foreach ($m in [regex]::Matches($line, "(?:Get-AppWindows|Get-Process\s+-Name)\s+'(?<exe>[\w.]+\.App)'")) {
            if ($m.Groups['exe'].Value -ne $ManifestExecutable) { $found += New-DryRunFinding 'FAIL' $number "$Name looks for the process '$($m.Groups['exe'].Value)' but the package's executable is '$ManifestExecutable'." }
        }
    }
    return $found
}

# Dry run of the named checks (files in $ChecksDir) against an optional built package; prints each finding and returns the number of FAILs.
function Invoke-CheckDryRun([string[]]$CheckNames, [string]$ChecksDir, [string]$MsixPath) {
    $manifestExe = if ($MsixPath -and (Test-Path $MsixPath)) { Get-MsixExecutableName $MsixPath } else { $null }
    Write-Host "DRYRUN: $(@($CheckNames).Count) check(s); package executable: $(if ($manifestExe) { $manifestExe } else { '(no package read)' })."
    $failures = 0
    foreach ($check in $CheckNames) {
        $path = Join-Path $ChecksDir "$check.ps1"
        if (-not (Test-Path $path)) {
            Write-Host "DRYRUN FAIL $check line 0: no check file named '$check' ($path does not exist)."
            $failures++
            continue
        }
        $text = Get-Content $path -Raw
        $findings = @(Get-CheckSourceFindings $check $text) + @(Get-WrongExecutableFindings $check $text $manifestExe)
        foreach ($f in $findings) { Write-Host "DRYRUN $($f.Severity) $check line $($f.Line): $($f.Message)" }
        $failures += @($findings | Where-Object { $_.Severity -eq 'FAIL' }).Count
    }
    Write-Host "DRYRUN: $failures failure(s)."
    return $failures
}
