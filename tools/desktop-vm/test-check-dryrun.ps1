<#
.SYNOPSIS
  Self-test for the VM check dry run (_dryrun.ps1, Slice 253 item 3). Needs no VM and no package. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_dryrun.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}
function Count-Severity($Findings, [string]$Severity) { return @($Findings | Where-Object { $_.Severity -eq $Severity }).Count }

# A deliberately wrong expected size (the guest screen is 1024 x 768) fails in the dry run.
$wrongSize = "`$OpenWidth = 1280`n`$OpenHeight = 720`n"
$f = @(Get-CheckSourceFindings 'dino-bad' $wrongSize)
Assert-Equal 'wrong width fails' 1 (Count-Severity $f 'FAIL')
Assert-Equal 'wrong width is named' $true ($f[0].Message -like '*OpenWidth = 1280*')
Assert-Equal 'a size that fits the guest passes' 0 (Count-Severity @(Get-CheckSourceFindings 'dino-ok' "`$OpenWidth = 1000`n`$OpenHeight = 650`n") 'FAIL')
Assert-Equal 'a size clamped to the work area passes' 0 (Count-Severity @(Get-CheckSourceFindings 'dino-clamped' "`$OpenWidth = 1100`n`$work = Get-WorkArea`n") 'FAIL')

# A raw name lookup (the tooltip trap) fails; the library helpers pass.
$raw = '$e = $root.FindFirst($scope, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Next")))'
Assert-Equal 'raw NameProperty lookup fails' 1 (Count-Severity @(Get-CheckSourceFindings 'dino-raw' $raw) 'FAIL')
Assert-Equal 'Find-UiaExact passes' 0 (Count-Severity @(Get-CheckSourceFindings 'dino-exact' '$b = Find-UiaExact -Name "Next"') 'FAIL')

# Fixed waits: 3 s or more warns unless explained; shorter ones are quiet.
Assert-Equal 'a 5 s wait warns' 1 (Count-Severity @(Get-CheckSourceFindings 'x' 'Start-Sleep -Seconds 5') 'WARN')
Assert-Equal 'a 3000 ms wait warns' 1 (Count-Severity @(Get-CheckSourceFindings 'x' 'Start-Sleep -Milliseconds 3000') 'WARN')
Assert-Equal 'an explained wait is quiet' 0 (Count-Severity @(Get-CheckSourceFindings 'x' 'Start-Sleep -Seconds 5   # fixed-wait: the app saves 500 ms after a resize') 'WARN')
Assert-Equal 'a 1 s wait is quiet' 0 (Count-Severity @(Get-CheckSourceFindings 'x' 'Start-Sleep -Seconds 1') 'WARN')

# A parse error fails.
Assert-Equal 'a parse error fails' $true ((Count-Severity @(Get-CheckSourceFindings 'x' 'function {') 'FAIL') -ge 1)

# The package manifest: the check must look for the package's own executable.
$work = Join-Path ([IO.Path]::GetTempPath()) ("rtst-dryrun-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force "$work\src"
    Set-Content "$work\src\AppxManifest.xml" '<Package><Applications><Application Id="App" Executable="DinoReader.App.exe" EntryPoint="Windows.FullTrustApplication" /></Applications></Package>'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory("$work\src", "$work\app.msix")
    $exe = Get-MsixExecutableName "$work\app.msix"
    Assert-Equal 'manifest executable is read from the msix' 'DinoReader.App' $exe
    Assert-Equal 'unreadable package gives $null' $null (Get-MsixExecutableName "$work\missing.msix")
    Assert-Equal 'the right process name passes' 0 @(Get-WrongExecutableFindings 'dino-x' "Get-AppWindows 'DinoReader.App'" $exe).Count
    Assert-Equal 'a wrong process name fails' 1 @(Get-WrongExecutableFindings 'dino-x' "Get-AppWindows 'ReadTheStupidText.App'" $exe).Count
    Assert-Equal 'no manifest: nothing to compare' 0 @(Get-WrongExecutableFindings 'dino-x' "Get-AppWindows 'Whatever.App'" $null).Count

    # Invoke-CheckDryRun end to end on a folder of checks: one bad file makes the count 1.
    $null = New-Item -ItemType Directory -Force "$work\checks"
    Set-Content "$work\checks\dino-bad.ps1" "`$ExpectedWidth = 1400"
    Set-Content "$work\checks\dino-good.ps1" "`$ExpectedWidth = 800"
    Assert-Equal 'dry run: a good check has no failure' 0 (Invoke-CheckDryRun @('dino-good') "$work\checks" "$work\app.msix" 6>$null)
    Assert-Equal 'dry run: a bad check has one failure' 1 (Invoke-CheckDryRun @('dino-good', 'dino-bad') "$work\checks" "$work\app.msix" 6>$null)
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

# Every check in the repo passes its own dry run (so the guard never blocks a run on the checks already there).
$checksDir = Join-Path $PSScriptRoot 'checks'
$names = @(Get-ChildItem $checksDir -Filter '*-*.ps1' | Where-Object { $_.Name -notlike '_*' } | ForEach-Object { $_.BaseName })
Assert-Equal 'the repo checks pass the dry run' 0 (Invoke-CheckDryRun $names $checksDir $null 6>$null)

# Slice 269: a trace string a check waits for must exist in src\.
$trace = "Wait-TraceAfter `$Package 'Dino shelf: a book is hovered' `$before 3`nCount-Trace `$Package 'gone line|real line'`nCount-Trace `$Package `"Dino keyboard: `$Key used`""
$traceFindings = @(Get-TraceStringFindings 'dino-x' $trace 'x Dino shelf: a book is hovered y REAL LINE z')
Assert-Equal 'a trace string present in src passes; one alternative is enough; a variable pattern is not read' 0 $traceFindings.Count
Assert-Equal 'a source interpolation stands for a word of the trace text' 0 @(Get-TraceStringFindings 'dino-x' "Count-Trace `$Package 'Dino keyboard: Space used'" 'W($"Dino keyboard: {e.Key} {(used ? "used" : "not used")} ({x})");').Count
Assert-Equal 'an interpolation does not make a different text match' 1 @(Get-TraceStringFindings 'dino-x' "Count-Trace `$Package 'Other thing: Space used'" 'W($"Dino keyboard: {e.Key} used");').Count
$regexFindings = @(Get-TraceStringFindings 'dino-x' "Count-Trace `$Package 'Dino shelf: \d+ books'" 'some source text')
Assert-Equal 'a regex pattern is warned about, never a false FAIL' 0 (Count-Severity $regexFindings 'FAIL')
Assert-Equal 'a regex pattern gives a WARN note' 1 (Count-Severity $regexFindings 'WARN')
Assert-Equal 'a doubled quote is read as one quote' 0 @(Get-TraceStringFindings 'dino-x' "Count-Trace `$Package 'it''s here'" "x it's here y").Count
$missing = @(Get-TraceStringFindings 'dino-x' "Count-Trace `$Package 'never written anywhere'" 'some source text')
Assert-Equal 'a trace string absent from src fails' 1 (Count-Severity $missing 'FAIL')
Assert-Equal 'the finding names the line' 1 $missing[0].Line

# Slice 269: the `# after:` header names checks that must run first; a missing file throws.
. (Join-Path $PSScriptRoot '_common.ps1')
$afterWork = Join-Path ([IO.Path]::GetTempPath()) ('rtst-after-' + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $afterWork
    Set-Content "$afterWork\dino-a.ps1" '# no header'
    Set-Content "$afterWork\dino-b.ps1" "# after: dino-a`n# body"
    Assert-Equal 'a check with no after header has no prerequisites' 0 @(Get-CheckAfter "$afterWork\dino-a.ps1" $afterWork).Count
    Assert-Equal 'an after header lists the named check' 'dino-a' (@(Get-CheckAfter "$afterWork\dino-b.ps1" $afterWork) -join ',')
    Set-Content "$afterWork\dino-c.ps1" '# after: dino-missing'
    $threw = $false
    try { Get-CheckAfter "$afterWork\dino-c.ps1" $afterWork | Out-Null } catch { $threw = $true }
    Assert-Equal 'an after header naming no check file throws' $true $threw
} finally { Remove-Item $afterWork -Recurse -Force -ErrorAction SilentlyContinue }
Assert-Equal 'dino-reorder declares the launch check first' 'dino-launch' (@(Get-CheckAfter (Join-Path $checksDir 'dino-reorder.ps1') $checksDir) -join ',')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-check-dryrun: all passed.'
exit 0
