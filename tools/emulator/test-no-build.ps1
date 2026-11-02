<#
.SYNOPSIS
  Self-test for Slice 242 items 3 and 5: -NoBuild on install-debug.ps1 / run-flows.ps1 (a cache miss exits 6 at once with the "cache missed" line, no build, no emulator),
  and the tablet instance (-Instance tablet is instance 3, AVD RTST_Tablet, port 5558). Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }   # common.ps1 only needs it to build paths
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# item 5: the tablet instance
Assert-Equal 'tablet alias is instance 3' 3 (Resolve-EmulatorInstance 'tablet')
Assert-Equal 'a number stays a number' 2 (Resolve-EmulatorInstance '2')
Set-EmulatorInstance (Resolve-EmulatorInstance 'tablet')
Assert-Equal 'tablet AVD' 'RTST_Tablet' $script:AvdName
Assert-Equal 'tablet port' 5558 $script:Port
Assert-Equal 'tablet serial' 'emulator-5558' $script:Serial
Set-EmulatorInstance 2
Assert-Equal 'instance 2 keeps RTST_Test2' 'RTST_Test2' $script:AvdName
Set-EmulatorInstance 1
Assert-Equal 'instance 1 keeps RTST_Test' 'RTST_Test' $script:AvdName
$rejected = try { Set-EmulatorInstance 4; $false } catch { $true }
Assert-Equal 'instance 4 is refused' $true $rejected

# item 3: the pure parts
Assert-Equal 'switch asks for no build' $true (Test-NoBuildRequested $true)
Assert-Equal 'no switch, no variable: builds' $false (Test-NoBuildRequested $false)
Assert-Equal 'miss line' 'cache missed for dino, key abc123: run install-debug.ps1 -BuildOnly first' (Format-EmulatorNoBuildMiss 'dino' 'abc123')

# item 3: the scripts exit at once on a miss (the lookup root is an empty folder, so the cache always misses)
$engine = (Get-Process -Id $PID).Path
$emptyRoot = Join-Path ([IO.Path]::GetTempPath()) ('rtst-nobuild-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force $emptyRoot
$prior = $env:RTST_BUILD_CACHE_LOOKUP_ROOT
$env:RTST_BUILD_CACHE_LOOKUP_ROOT = $emptyRoot
try {
    foreach ($case in @(
            @{ Name = 'install-debug -NoBuild'; Script = 'install-debug.ps1'; Args = @('-App', 'dino', '-NoBuild') },
            @{ Name = 'run-flows -NoBuild'; Script = 'run-flows.ps1'; Args = @('-App', 'dino', '-NoBuild', '-Flow', 'no-such-flow') })) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $out = (& $engine -NoProfile -File (Join-Path $PSScriptRoot $case.Script) @($case.Args) 2>&1 | Out-String)
        Assert-Equal "$($case.Name): exit code" $script:EmulatorExitCacheMissed $LASTEXITCODE
        Assert-Equal "$($case.Name): prints the miss line" $true ($out -match 'cache missed for dino, key [0-9a-f]{12}: run install-debug\.ps1 -BuildOnly first')
        Assert-Equal "$($case.Name): quick (no build)" $true ($sw.Elapsed.TotalSeconds -lt 60)
    }
    # -Rebuild with -NoBuild is a contradiction: refused at once with exit 6, even when the cache holds the build.
    $out = (& $engine -NoProfile -File (Join-Path $PSScriptRoot 'install-debug.ps1') -App dino -Rebuild -NoBuild 2>&1 | Out-String)
    Assert-Equal 'install-debug -Rebuild -NoBuild: exit code' $script:EmulatorExitCacheMissed $LASTEXITCODE
    Assert-Equal 'install-debug -Rebuild -NoBuild: says why' $true ($out -match 'contradict')
} finally {
    $env:RTST_BUILD_CACHE_LOOKUP_ROOT = $prior
    Remove-Item $emptyRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host "All no-build and tablet checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
