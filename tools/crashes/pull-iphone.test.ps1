# Slice 312: fixture test for pull-iphone.ps1 (no phone, no libimobiledevice).
#   pwsh -NoProfile -File tools/crashes/pull-iphone.test.ps1
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'pull-iphone.ps1'
$root = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'tmp/pull-iphone-test'
if (Test-Path $root) { [System.IO.Directory]::Delete($root, $true) }
New-Item -ItemType Directory -Force -Path $root | Out-Null
$failures = 0
function Check { param([string]$Name, [bool]$Ok) if ($Ok) { Write-Output "ok   $Name" } else { Write-Output "FAIL $Name"; $script:failures++ } }

function New-Ips {
    param([string]$Name, [string]$Bundle, [string]$Time, [string]$Type, [string]$Symbol)
    $header = @{ app_name = 'App'; bundleID = $Bundle; timestamp = $Time } | ConvertTo-Json -Compress
    $body = @{
        bundleID = $Bundle; captureTime = $Time; bundleInfo = @{ CFBundleShortVersionString = '1.2.3' }
        exception = @{ type = $Type; signal = 'SIGSEGV' }; faultingThread = 1
        usedImages = @(@{ name = 'dyld' }, @{ name = 'DinoReader' })
        threads = @(@{ frames = @() }, @{ frames = @(@{ imageIndex = 1; imageOffset = 4096; symbol = $Symbol }, @{ imageIndex = 1; imageOffset = 255 }) })
    } | ConvertTo-Json -Depth 8 -Compress
    Set-Content -LiteralPath (Join-Path $root $Name) -Value @($header, $body) -Encoding UTF8
}

New-Ips 'old.ips' 'uk.sirous.dinoreader' '2026-11-01 10:00:00.00 +0000' 'EXC_OLD' 'OldFrame'
New-Ips 'new.ips' 'uk.sirous.dinoreader' '2026-11-02 11:30:00.00 +0000' 'EXC_BAD_ACCESS' 'NewFrame'
New-Ips 'other.ips' 'com.apple.other' '2026-11-03 09:00:00.00 +0000' 'EXC_OTHER' 'Other'

$out = (& pwsh -NoProfile -File $script -InputDir $root) -join "`n"
Check 'reads all three files' ($out -match '3 report\(s\)')
Check 'newest Dino crash wins' ($out -match 'EXC_BAD_ACCESS' -and $out -notmatch 'EXC_OLD')
Check 'other apps filtered out' ($out -notmatch 'EXC_OTHER')
Check 'top frames printed with image and symbol' ($out -match 'DinoReader NewFrame' -and $out -match 'DinoReader 0xff')
Check 'RTST with no report says so' ($out -match 'RTST: no crash report')
Check 'missing dSYM is said' ($out -match 'not symbolicated')

$hangDir = Join-Path $root 'hang'
New-Item -ItemType Directory -Force -Path $hangDir | Out-Null
$hangHeader = @{ bug_type = '298'; bundleID = 'uk.sirous.dinoreader'; timestamp = '2026-11-09 10:00:00.00 +0000' } | ConvertTo-Json -Compress
$hangBody = @{ bundleID = 'uk.sirous.dinoreader'; captureTime = '2026-11-09 10:00:00.00 +0000' } | ConvertTo-Json -Compress
Set-Content -LiteralPath (Join-Path $hangDir 'hang.ips') -Value @($hangHeader, $hangBody) -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $root 'new.ips') -Destination $hangDir
$hang = (& pwsh -NoProfile -File $script -InputDir $hangDir) -join "`n"
Check 'a newer non-crash report does not hide the real crash' ($hang -match 'EXC_BAD_ACCESS')

$emptyDir = Join-Path $root 'empty'
New-Item -ItemType Directory -Force -Path $emptyDir | Out-Null
$empty = (& pwsh -NoProfile -File $script -InputDir $emptyDir 3>&1) -join "`n"
Check 'zero .ips files is warned about' ($empty -match 'no \.ips files found')

$nope = (& pwsh -NoProfile -File $script -InputDir (Join-Path $root 'does-not-exist')) -join "`n"
Check 'missing -InputDir gives a plain error and exit 4' (($LASTEXITCODE -eq 4) -and $nope -match 'is not a folder')

$missing = (& pwsh -NoProfile -File $script -ToolPath (Join-Path $root 'nothing-here') -NoPathFallback) -join "`n"
Check 'missing tool exits 2 with install steps' (($LASTEXITCODE -eq 2) -and ($missing -match 'Apple Devices' -and $missing -match 'libimobiledevice'))

$tzDir = Join-Path $root 'tz'
New-Item -ItemType Directory -Force -Path $tzDir | Out-Null
$rootSaved = $root; $root = $tzDir
New-Ips 'a.ips' 'uk.sirous.dinoreader' '2026-11-01 01:30:00.00 +0100' 'EXC_EARLIER' 'A'
New-Ips 'b.ips' 'uk.sirous.dinoreader' '2026-11-01 01:10:00.00 +0000' 'EXC_LATER' 'B'
$root = $rootSaved
$tz = (& pwsh -NoProfile -File $script -InputDir $tzDir) -join "`n"
Check 'newest crash accounts for the time zone offset' ($tz -match 'EXC_LATER' -and $tz -notmatch 'EXC_EARLIER')

$badDir = Join-Path $root 'bad'
New-Item -ItemType Directory -Force -Path $badDir | Out-Null
Set-Content -LiteralPath (Join-Path $badDir 'broken.ips') -Value 'not json' -Encoding UTF8
$bad = (& pwsh -NoProfile -File $script -InputDir $badDir 3>&1) -join "`n"
Check 'unreadable .ips is warned about and counted' ($bad -match 'broken\.ips' -and $bad -match '1 \.ips file\(s\) could not be read')

$dsym = (& pwsh -NoProfile -File $script -InputDir $root -DsymDir $root) -join "`n"
Check '-DsymDir keeps the not symbolicated warning' ($dsym -match 'not symbolicated' -and $dsym -match 'is ignored')

# Pull path: a stub tool (given as a file via -ToolPath) that records its arguments, writes a report and exits with a chosen code.
$stub = Join-Path $root 'stub-idevicecrashreport.ps1'
$stubText = @'
param([Parameter(ValueFromRemainingArguments)]$All)
Write-Output 'stub: copying crash logs'
$target = $All[-1]
Set-Content -LiteralPath (Join-Path $target 'args.txt') -Value ($All -join ' ')
if ($env:STUB_EXIT -eq '0') { Copy-Item -LiteralPath (Join-Path $env:STUB_SRC 'new.ips') -Destination $target }
exit ([int]$env:STUB_EXIT)
'@
Set-Content -LiteralPath $stub -Value $stubText -Encoding UTF8
$env:STUB_SRC = $root

$env:STUB_EXIT = '0'
$pullOut = Join-Path $root 'pull-ok'
$ok = (& pwsh -NoProfile -File $script -ToolPath $stub -OutDir $pullOut -Udid U123 -Network) -join "`n"
Check 'pull success: exit 0 and the pulled crash is shown' (($LASTEXITCODE -eq 0) -and $ok -match 'EXC_BAD_ACCESS' -and $ok -notmatch 'failed')
$runDir = @(Get-ChildItem -LiteralPath $pullOut -Directory)
Check 'each pull goes into a fresh timestamped subfolder' ($runDir.Count -eq 1 -and $runDir[0].Name -match '^\d{8}-\d{6}$')
Check 'pull passes -k, -u, -n and the target folder last' ($runDir.Count -eq 1 -and (Get-Content (Join-Path $runDir[0].FullName 'args.txt') -Raw).Trim() -eq "-k -u U123 -n $($runDir[0].FullName)")

$env:STUB_EXIT = '3'
$fail = (& pwsh -NoProfile -File $script -ToolPath $stub -OutDir (Join-Path $root 'pull-fail')) -join "`n"
Check 'pull failure: exit 3 with the trust hint' (($LASTEXITCODE -eq 3) -and $fail -match 'failed \(exit 3\)')
Remove-Item Env:STUB_EXIT, Env:STUB_SRC

if ($failures -gt 0) { Write-Output "$failures check(s) failed"; exit 1 }
Write-Output 'all passed'
