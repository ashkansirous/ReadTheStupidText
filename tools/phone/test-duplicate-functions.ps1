<#
.SYNOPSIS
  Self-test (Slice 315 items 27, review findings 3-4): no PowerShell function is defined in two files that one script loads together.
  A later dot-sourced definition silently replaces the earlier one: Slice 308's `Get-PackageUid` in checks/_lib.ps1 shadowed the one in common.ps1, the dev app's uid was never read, and
  earlier `paused-notification` PASS results may never have blocked the network.
  For every non-test script under tools/ the dot-sourced files are found with the PowerShell parser (any quoting: 'x.ps1', "$PSScriptRoot\x.ps1", unquoted), followed transitively
  (common.ps1 loads quoting.ps1, build-cache.ps1, lease-common.ps1, ...), and a name defined in two different files of that set is a failure.
  Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
$script:failed = 0

function Get-ParsedAst([string]$Path) {
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$parseErrors)
    if ($parseErrors -and $parseErrors.Count -gt 0) { throw "$Path does not parse: $($parseErrors[0].Message)" }
    return $ast
}

function Get-DefinedFunctionNames($Ast) {
    $found = $Ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)
    return @($found | ForEach-Object { $_.Name })
}

# The .ps1 paths named in the dot-source commands of a script (a command that starts with `.`), whatever the quoting; each is resolved against the script's own folder
# (every load in tools/ is relative to $PSScriptRoot) and kept when the file exists. A load through a variable (`. $Common`) names no file and is not followed.
function Get-DotSourcedFiles($Ast, [string]$ScriptDir) {
    $dots = $Ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $node.InvocationOperator -eq [System.Management.Automation.Language.TokenKind]::Dot }, $true)
    $files = New-Object Collections.Generic.List[string]
    foreach ($dot in $dots) {
        foreach ($match in [regex]::Matches($dot.Extent.Text, '[^\s''"()]+\.ps1')) {
            $candidate = Join-Path $ScriptDir ($match.Value -replace '^\$PSScriptRoot[\\/]', '')
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $files.Add((Resolve-Path -LiteralPath $candidate).Path) }
        }
    }
    return @($files | Select-Object -Unique)
}

# Slice 315 item 8: the dot-source commands whose file this check cannot resolve (a load through a variable `. $Common`, a scriptblock built from text, a path that does not exist),
# as `file:line text`, so a shadowed function behind one is never silently out of sight.
function Get-UnresolvedDotSources($Ast, [string]$ScriptDir, [string]$File) {
    $dots = $Ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $node.InvocationOperator -eq [System.Management.Automation.Language.TokenKind]::Dot }, $true)
    $unresolved = New-Object Collections.Generic.List[string]
    foreach ($dot in $dots) {
        if (@(Get-DotSourcedFiles $dot $ScriptDir).Count -eq 0) { $unresolved.Add("${File}:$($dot.Extent.StartLineNumber) $($dot.Extent.Text)") }
    }
    return @($unresolved)
}

# $Loads maps a script to the files it dot-sources directly. Returns the script plus everything it loads, directly or not.
function Get-LoadClosure([string]$Script, $Loads) {
    $seen = New-Object Collections.Generic.List[string]
    $queue = New-Object Collections.Generic.Queue[string]
    $queue.Enqueue($Script)
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if ($seen.Contains($current)) { continue }
        $seen.Add($current)
        foreach ($next in @($Loads[$current])) { if ($next) { $queue.Enqueue($next) } }
    }
    return $seen.ToArray()
}

# $Definitions maps a file to its function names. One line per name defined in two files that a single script loads together.
function Find-ShadowedFunctions($Definitions, $Loads, [string[]]$Roots) {
    $clashes = New-Object Collections.Generic.HashSet[string]
    foreach ($root in $Roots) {
        $owners = @{}
        foreach ($file in (Get-LoadClosure $root $Loads)) {
            foreach ($name in @($Definitions[$file] | Select-Object -Unique)) { $owners[$name] = @($owners[$name]) + @($file) | Where-Object { $_ } }
        }
        foreach ($name in $owners.Keys) {
            if (@($owners[$name]).Count -gt 1) { $null = $clashes.Add("function $name is defined in both $((@($owners[$name]) | Sort-Object) -join ' and ') (loaded together by $root)") }
        }
    }
    return @($clashes | Sort-Object)
}

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# The finder: a name defined in a library and in a script, or in a library and in a library it loads transitively, is found; check-vs-check repeats and unloaded files are not.
$fake = @{ 'common' = @('Get-A', 'Get-B'); 'sub' = @('Get-B'); 'one' = @('Get-A', 'Get-Z'); 'two' = @('Get-Z'); 'solo' = @('Get-B') }
$fakeLoads = @{ 'one' = @('common'); 'two' = @('common'); 'common' = @('sub'); 'solo' = @() }
$clashes = @(Find-ShadowedFunctions $fake $fakeLoads @('one', 'two', 'solo'))
Assert-Equal 'a transitive clash is found' $true ([bool]($clashes -match 'Get-B is defined in both common and sub'))
Assert-Equal 'a script repeating a library name is found' $true ([bool]($clashes -match 'Get-A is defined in both common and one'))
Assert-Equal 'a repeat between two scripts that do not load each other is allowed' 0 @($clashes -match 'Get-Z').Count
Assert-Equal 'a script that loads nothing has no clash' 0 @($clashes -match 'loaded together by solo').Count

# The loader finder reads every quoting (the review's shapes) and skips a non-dot call.
$probeDir = Join-Path $PSScriptRoot 'checks'
$probe = [System.Management.Automation.Language.Parser]::ParseInput(". `"`$PSScriptRoot\..\common.ps1`"`r`n  . ../common.ps1`r`n. (Join-Path `$PSScriptRoot '..\common.ps1')`r`n& ../timing.ps1`r`n", [ref]$null, [ref]$null)
Assert-Equal 'a double-quoted, an unquoted and a Join-Path load all resolve; a & call does not' 1 @(Get-DotSourcedFiles $probe $probeDir).Count

$probeUnresolved = [System.Management.Automation.Language.Parser]::ParseInput(". `$Common`r`n. (Join-Path `$PSScriptRoot '..\common.ps1')`r`n", [ref]$null, [ref]$null)
Assert-Equal 'a load through a variable is reported unresolved, a Join-Path load is not' 1 @(Get-UnresolvedDotSources $probeUnresolved $probeDir 'probe.ps1').Count

# The real files: every non-test script of tools/.
$toolsDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$definitions = @{}
$loads = @{}
$roots = New-Object Collections.Generic.List[string]
$unresolvedLoads = New-Object Collections.Generic.List[string]
foreach ($file in (Get-ChildItem $toolsDir -Recurse -Filter *.ps1 -File)) {
    $ast = Get-ParsedAst $file.FullName
    $definitions[$file.FullName] = @(Get-DefinedFunctionNames $ast)
    $loads[$file.FullName] = @(Get-DotSourcedFiles $ast $file.DirectoryName)
    foreach ($line in @(Get-UnresolvedDotSources $ast $file.DirectoryName $file.FullName)) { $unresolvedLoads.Add($line) }
    if ($file.Name -notlike 'test-*.ps1') { $roots.Add($file.FullName) }
}
Write-Host "$($unresolvedLoads.Count) dot-source(s) in tools/ could not be resolved to a file (a shadowed function behind one is not seen):"
foreach ($line in $unresolvedLoads) { Write-Host "  $line" }
$real = @(Find-ShadowedFunctions $definitions $loads $roots.ToArray())
$runChecks = (Join-Path $toolsDir 'phone\run-checks.ps1')
$closure = @(Get-LoadClosure $runChecks $loads | ForEach-Object { Split-Path $_ -Leaf })
Assert-Equal 'run-checks.ps1 is seen loading common.ps1 and checks/_device.ps1' $true (($closure -contains 'common.ps1') -and ($closure -contains '_device.ps1'))
Assert-Equal 'run-checks.ps1 is seen loading build-cache.ps1 and lease-common.ps1 through the libraries' $true (($closure -contains 'build-cache.ps1') -and ($closure -contains 'lease-common.ps1'))
foreach ($line in $real) { Write-Host "FAIL: $line" }
Assert-Equal 'duplicate function definitions among files loaded together in tools/' 0 $real.Count

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "No duplicate function names among files loaded together in tools/ (PowerShell $($PSVersionTable.PSVersion))."
exit 0
