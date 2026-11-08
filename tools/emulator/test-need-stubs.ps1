<#
.SYNOPSIS
  Self-test for need-stubs.ps1 (Slice 320 item 5): which `# needs:` values name a stub, that the real stub scripts exist, and that a stub is started on a free port,
  answers, is stopped, and that a stub already listening is left alone. Needs no device and no emulator; needs node on PATH (without it the start/stop leg is NOT RUN).
  Run it in both `powershell` (5.1) and `pwsh`.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'need-stubs.ps1')
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$failed = 0

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function Get-FreePort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}

Assert-Equal 'stub needs are picked out' 'report-stub,crash-stub' ((Get-StubNeeds @('cellular-only', 'report-stub', 'crash-stub')) -join ',')
Assert-Equal 'no stub need' 0 (@(Get-StubNeeds @('cellular-only')).Count)
Assert-Equal 'empty needs' 0 (@(Get-StubNeeds @()).Count)
Assert-Equal 'no needs header (null) needs no stub' 0 (@(Get-StubNeeds $null).Count)
Assert-Equal 'blank need needs no stub' 0 (@(Get-StubNeeds @('', $null)).Count)
foreach ($need in @('report-stub', 'crash-stub')) {
    Assert-Equal "script of $need exists" $true (Test-Path (Join-Path $repo (Get-NeedStubScript $need)))
}

$reportDir = Join-Path $PSScriptRoot ("tmp-reports-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reportDir | Out-Null
try {
    Set-Content -Path (Join-Path $reportDir 'report-1.json') -Value '{}'
    Set-Content -Path (Join-Path $reportDir 'report-12.json') -Value '{}'
    Set-Content -Path (Join-Path $reportDir 'keep.txt') -Value 'x'
    Clear-NeedStubReports 'crash-stub' $reportDir
    Assert-Equal 'old report-N.json files are cleared' 0 @(Get-ChildItem $reportDir -Filter 'report-*.json').Count
    Assert-Equal 'other files are left' $true (Test-Path (Join-Path $reportDir 'keep.txt'))
} finally { Remove-Item -Recurse -Force $reportDir }

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host 'NOT RUN: node is not on PATH, so the start/stop leg did not run.'
} else {
    $port = Get-FreePort
    $stub = Start-NeedStub 'crash-stub' $repo $port 20
    try {
        Assert-Equal 'a started stub listens' $true (Test-PortListening $port)
        Assert-Equal 'a started stub is ours' $true ($null -ne $stub.Process)
        $second = Start-NeedStub 'crash-stub' $repo $port 5
        Assert-Equal 'a stub already listening is not started again' $true ($null -eq $second.Process)
        Stop-NeedStub $second
        Assert-Equal 'stopping a stub that is not ours leaves it listening' $true (Test-PortListening $port)
        Assert-Equal 'the stub names itself' 'crash-stub' (Get-StubIdentity $port)
        $wrongKind = $null
        try { Start-NeedStub 'report-stub' $repo $port 5 | Out-Null } catch { $wrongKind = $_.Exception.Message }
        Assert-Equal 'a stub of the other kind on the port is refused' $true ("$wrongKind" -match "held by 'crash-stub'")
    } finally { Stop-NeedStub $stub }
    $stopped = $false
    for ($i = 0; $i -lt 20 -and -not $stopped; $i++) { $stopped = -not (Test-PortListening $port); if (-not $stopped) { Start-Sleep -Milliseconds 250 } }
    Assert-Equal 'Stop-NeedStub frees the port' $true $stopped
}

if ($failed) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All need-stubs checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
