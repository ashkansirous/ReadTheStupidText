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

if ($failed -gt 0) { Write-Host "$failed failure(s)."; exit 1 }
Write-Host 'switch-param-assign self-test: all passed.'
