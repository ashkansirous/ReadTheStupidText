<#
.SYNOPSIS
  Self-test for the per-run worktree mark (Slice 284 item 3): New-WorktreeRunMark / Remove-WorktreeRunMark as written in
  tools/emulator/run-flows.ps1 and tools/desktop-vm/Invoke-DesktopTest.ps1. Two runs on one worktree each get a differently named
  `device-test-running-*` file in the git dir and each removes only its own. Needs git, no device. Run it in both `powershell` (5.1) and `pwsh`.
#>
$ErrorActionPreference = 'Stop'
$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function Import-MarkFunctions([string]$ScriptPath) {
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$null, [ref]$null)
    $found = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -in 'New-WorktreeRunMark', 'Remove-WorktreeRunMark' }, $true)
    Assert-Equal "$ScriptPath defines both mark functions" 2 @($found).Count
    foreach ($f in $found) {
        $source = $f.Extent.Text -replace '^function\s+', 'function script:'
        . ([scriptblock]::Create($source))
    }
}

# Slice 284 design (two strikes): the try statement whose body holds `$runMark = New-WorktreeRunMark ...` as its first statement must have a finally that calls Remove-WorktreeRunMark on the same variable.
function Test-RemoveInFinally([string]$ScriptPath) {
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$null, [ref]$null)
    $creates = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -eq '$runMark' -and $n.Right.Extent.Text -match '^New-WorktreeRunMark\b' }, $true))
    if ($creates.Count -ne 1) { return $false }
    $create = $creates[0]
    for ($p = $create.Parent; $p; $p = $p.Parent) {
        if ($p -isnot [System.Management.Automation.Language.TryStatementAst]) { continue }
        if ($p.Body.Extent.StartOffset -gt $create.Extent.StartOffset -or $create.Extent.EndOffset -gt $p.Body.Extent.EndOffset) { continue }   # the create sits in a catch or finally, not the try body
        if ($p.Body.Statements[0] -ne $create -or -not $p.Finally) { return $false }
        $removes = @($p.Finally.FindAll({ param($n) $n -is [System.Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Remove-WorktreeRunMark' -and $n.Extent.Text -match '\$runMark\s*$' }, $true))
        return ($removes.Count -ge 1)
    }
    return $false
}

function Get-Marks([string]$GitDir) { return @(Get-ChildItem -LiteralPath $GitDir -Filter 'device-test-running*' -File -ErrorAction SilentlyContinue) }

$root = Join-Path ([IO.Path]::GetTempPath()) ('rtst-mark-test-' + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $root
    & git -C $root init -q
    $gitDir = (& git -C $root rev-parse --absolute-git-dir)

    $owners = [ordered]@{ 'tools\emulator\run-flows.ps1' = 'run-flows'; 'tools\desktop-vm\Invoke-DesktopTest.ps1' = 'desktop-test' }
    foreach ($script in $owners.Keys) {
        $owner = $owners[$script]
        $path = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')) $script
        Import-MarkFunctions $path
        $text = Get-Content -Raw $path
        Assert-Equal "$script creates a mark for its owner" $true ($text -match ("New-WorktreeRunMark '" + $owner + "'"))
        Assert-Equal "$script removes its mark from a finally block" $true (Test-RemoveInFinally $path)

        $a = New-WorktreeRunMark $owner $root
        $b = New-WorktreeRunMark $owner $root
        Assert-Equal "$script mark name carries the owner" $true ((Split-Path $a -Leaf).StartsWith("device-test-running-$owner-"))
        Assert-Equal "$script two marks are different files" $true ($a -ne $b)
        Assert-Equal "$script both marks exist" 2 (Get-Marks $gitDir).Count
        Assert-Equal "$script mark name starts with device-test-running" $true ((Split-Path $a -Leaf).StartsWith('device-test-running'))
        Remove-WorktreeRunMark $a
        Assert-Equal "$script run A removed only its own" $true ((-not (Test-Path $a)) -and (Test-Path $b))
        Remove-WorktreeRunMark $b
        Assert-Equal "$script none left" 0 (Get-Marks $gitDir).Count
        Remove-WorktreeRunMark $null   # a run that got no mark (not a git folder) removes nothing and does not throw

        # Review finding 3: a mark whose pid is dead is stale and the next run removes it; one whose pid is alive (this process) stays.
        $dead = Join-Path $gitDir "device-test-running-$owner-2147483000-deadbeef"
        Set-Content -LiteralPath $dead -Value 'stale'
        $live = New-WorktreeRunMark $owner $root
        Assert-Equal "$script stale mark (dead pid) removed by the next run" $false (Test-Path $dead)
        $second = New-WorktreeRunMark $owner $root
        Assert-Equal "$script live mark kept by the next run" $true (Test-Path $live)
        Remove-WorktreeRunMark $live
        Remove-WorktreeRunMark $second
        Assert-Equal "$script none left after the stale check" 0 (Get-Marks $gitDir).Count

        # An unreadable StartTime (pid 4, System, access denied) counts as live; a readable one is older than the mark. Either way the mark stays and our own mark is still written.
        $system = Join-Path $gitDir "device-test-running-$owner-4-feedface"
        Set-Content -LiteralPath $system -Value 'other-user process'
        $afterSystem = New-WorktreeRunMark $owner $root
        Assert-Equal "$script mark of a live pid with an unreadable StartTime kept, own mark written" $true ((Test-Path $system) -and $afterSystem -and (Test-Path $afterSystem))
        Remove-Item -LiteralPath $system -Force
        Remove-WorktreeRunMark $afterSystem

        # A live pid that started after the mark was written is a reused pid: the mark is stale too.
        $reused = Join-Path $gitDir "device-test-running-$owner-$PID-cafef00d"
        Set-Content -LiteralPath $reused -Value 'reused pid'
        (Get-Item -LiteralPath $reused).LastWriteTime = (Get-Process -Id $PID).StartTime.AddHours(-1)
        $third = New-WorktreeRunMark $owner $root
        Assert-Equal "$script mark of a reused pid (process started after the mark) removed" $false (Test-Path $reused)
        Remove-WorktreeRunMark $third
    }

    $outside = Join-Path ([IO.Path]::GetTempPath()) ('rtst-mark-nogit-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Force $outside
    try { Assert-Equal 'outside a git folder no mark is made' $null (New-WorktreeRunMark 'run-flows' $outside) }
    finally { Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue }
}
finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }

if ($failed -gt 0) { Write-Host "$failed check(s) FAILED"; exit 1 }
Write-Host 'worktree mark self-test: all passed'
