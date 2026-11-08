<#
.SYNOPSIS
  Self-test (Slice 241): no script under tools/ assigns to its own [switch] parameter. PowerShell names are case-insensitive, so
  `[switch]$Split` plus a later `$split = Split-FlowNames ...` tries to convert the object into a switch and crashes (run-flows.ps1).
  Parses every .ps1 with the PowerShell AST. Run in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
$failed = 0
$toolsRoot = $PSScriptRoot

function Get-ParseTree([string]$Text) {
    $errors = $null
    $tree = [System.Management.Automation.Language.Parser]::ParseInput($Text, [ref]$null, [ref]$errors)
    if ($errors -and $errors.Count) { throw "parse error: $($errors[0].Message)" }
    return $tree
}

function Get-SwitchParamNames($Tree) {
    if (-not $Tree.ParamBlock) { return @() }
    $switches = $Tree.ParamBlock.Parameters | Where-Object {
        $_.Attributes | Where-Object { $_ -is [System.Management.Automation.Language.TypeConstraintAst] -and $_.TypeName.Name -eq 'switch' }
    }
    return @($switches | ForEach-Object { $_.Name.VariablePath.UserPath })
}

function Test-InsideFunction($Node) {
    for ($p = $Node.Parent; $p; $p = $p.Parent) { if ($p -is [System.Management.Automation.Language.FunctionDefinitionAst]) { return $true } }
    return $false
}

function Test-BoolLiteral($Right) {
    $text = $Right.Extent.Text.Trim()
    return $text -in @('$true', '$false')
}

# Returns 'line: text' for each assignment (outside functions, which have their own scope) to a script [switch] parameter.
function Find-SwitchAssignments($Tree) {
    $names = Get-SwitchParamNames $Tree
    if ($names.Count -eq 0) { return @() }
    $assigns = $Tree.FindAll({ param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true)
    $found = foreach ($a in $assigns) {
        $target = $a.Left
        if ($target -is [System.Management.Automation.Language.ConvertExpressionAst]) { $target = $target.Child }
        if ($target -isnot [System.Management.Automation.Language.VariableExpressionAst]) { continue }
        $name = $target.VariablePath.UserPath -replace '^(script|local|private):', ''
        if ($names -notcontains $name) { continue }   # -notcontains is case-insensitive, like PowerShell
        if (Test-InsideFunction $a) { continue }
        if (Test-BoolLiteral $a.Right) { continue }
        "$($a.Extent.StartLineNumber): $($a.Extent.Text)"
    }
    return @($found)
}

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# Fixtures: the real bug must be caught, the safe shapes must not be.
$bug = "param([switch]`$Split)`n`$split = Split-FlowNames @('a','b') `$true`n"
Assert-Equal 'fixture: object assigned to switch (the run-flows bug)' 1 (Find-SwitchAssignments (Get-ParseTree $bug)).Count
Assert-Equal 'fixture: other case spelling still caught' 1 (Find-SwitchAssignments (Get-ParseTree "param([switch]`$Split)`n`$SPLIT = 5`n")).Count
Assert-Equal 'fixture: script: scope caught' 1 (Find-SwitchAssignments (Get-ParseTree "param([switch]`$Split)`n`$script:Split = 'x'`n")).Count
Assert-Equal 'fixture: rename is fine' 0 (Find-SwitchAssignments (Get-ParseTree "param([switch]`$Split)`n`$splitPlan = Split-FlowNames 1 2`n")).Count
Assert-Equal 'fixture: boolean literal is fine' 0 (Find-SwitchAssignments (Get-ParseTree "param([switch]`$Split)`n`$Split = `$false`n")).Count
Assert-Equal 'fixture: function-local variable is fine' 0 (Find-SwitchAssignments (Get-ParseTree "param([switch]`$Split)`nfunction F { `$split = 1 }`n")).Count
Assert-Equal 'fixture: non-switch param is fine' 0 (Find-SwitchAssignments (Get-ParseTree "param([string]`$Split)`n`$split = 1`n")).Count

# The real tree.
foreach ($file in Get-ChildItem $toolsRoot -Recurse -Filter *.ps1) {
    $errors = $null
    $tree = [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$errors)
    if ($errors -and $errors.Count) { continue }   # parse errors are the lint step's job
    foreach ($hit in Find-SwitchAssignments $tree) {
        $failed++
        Write-Host "FAIL: $($file.FullName) line ${hit} assigns to a [switch] script parameter; rename the local variable."
    }
}

# Slice 315 item 8 (review finding 8): every self-test that tools-selftests.yml runs ends with an explicit `exit`. CI's pwsh/powershell shells forward a stale $LASTEXITCODE from a native
# call or child script at the end of the step, so a test that prints "all passed" and ends on such a call fails the step (test-layout-check.ps1, 898359dc).
function Get-LastCodeLine([string]$Path) {
    $lines = @(Get-Content -LiteralPath $Path | Where-Object { $_.Trim() })
    if ($lines.Count -eq 0) { return '' }
    return $lines[-1].Trim()
}
Assert-Equal 'fixture: a last line that is a native call is not an exit' $false ('cmd /c exit 3' -match '^exit\b')
$workflowText = [IO.File]::ReadAllText((Join-Path $toolsRoot '..\.github\workflows\tools-selftests.yml'))
$listed = @([regex]::Matches($workflowText, '(?m)^\s+run:\s*\./(tools/(?:\S+/)?test-[\w-]+\.ps1)\s*$') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
Assert-Equal 'tools-selftests.yml lists self-tests' $true ($listed.Count -gt 10)
Assert-Equal 'the explicit-exit scan also covers a test directly under tools/' $true ($listed -contains 'tools/test-switch-param-assign.ps1')
Assert-Equal 'the explicit-exit scan also covers a test in a subfolder' $true ($listed -contains 'tools/desktop-vm/test-check-dryrun.ps1')
foreach ($relative in $listed) {
    $path = Join-Path $toolsRoot ('..\' + ($relative -replace '/', '\'))
    if (-not (Test-Path -LiteralPath $path)) { $failed++; Write-Host "FAIL: tools-selftests.yml runs $relative, which does not exist."; continue }
    $last = Get-LastCodeLine $path
    if ($last -notmatch '^exit\b') { $failed++; Write-Host "FAIL: $relative ends with [$last], not an explicit exit (CI would forward a stale `$LASTEXITCODE)." }
}

if ($failed -gt 0) { Write-Host "$failed failure(s)."; exit 1 }
Write-Host 'switch-param-assign self-test: all passed.'
exit 0
