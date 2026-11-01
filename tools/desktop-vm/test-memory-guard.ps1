<#
.SYNOPSIS
  Self-test for the free-memory guard of Invoke-DesktopTest.ps1 (Slice 225 item 20). Needs no VM, no Hyper-V and no device: the function is read out of the script with the PowerShell parser.
  Run it in both `powershell` (5.1) and `pwsh`.
#>
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Invoke-DesktopTest.ps1'
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$null, [ref]$errors)
if (@($errors).Count -gt 0) { Write-Host "FAIL: Invoke-DesktopTest.ps1 does not parse: $($errors[0].Message)"; exit 1 }

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$definition = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-MemoryProblem' }, $true)
if (-not $definition) { Write-Host 'FAIL: Get-MemoryProblem is not defined in Invoke-DesktopTest.ps1'; exit 1 }
. ([scriptblock]::Create($definition.Extent.Text))

Assert-Equal 'enough memory: no problem' $null (Get-MemoryProblem 12.5 8)
Assert-Equal 'exactly the threshold: no problem' $null (Get-MemoryProblem 8 8)
$low = Get-MemoryProblem 6.9 8
Assert-Equal 'below the threshold: a plain message' $true ($low -like 'needs 8 GB free memory to start the VM, has 6.9 GB*')
Assert-Equal 'a threshold above the host''s memory always stops' $true ([bool](Get-MemoryProblem 6.9 99999))

# The check runs before the build and before the VM is touched: it must come first in the main part of the script.
$text = Get-Content $scriptPath -Raw
$guard = $text.IndexOf('Get-MemoryProblem (Get-FreeMemoryGB)')
Assert-Equal 'the guard is called' $true ($guard -gt 0)
foreach ($later in '$msix = Find-Package', 'Start-Activity', 'Enter-VmLock', 'Restore-CleanCheckpoint -Start') {
    Assert-Equal "the guard comes before $later" $true ($guard -lt $text.IndexOf($later, $guard))
}
Assert-Equal 'review 3: the guard is skipped, with a log line, when the VM is already running' $true ($text -match '(?s)if \(Test-VmRunning\) \{ Write-Host ''Free-memory guard skipped.*?\}\s*else \{ \$memoryProblem = Get-MemoryProblem \(Get-FreeMemoryGB\)')
Assert-Equal 'review 3: Test-VmRunning reads the VM state and treats an unreadable state as not running' $true ($text -match '(?s)function Test-VmRunning \{.*?\.State -eq ''Running''.*?catch \{ return \$false \}')
Assert-Equal 'a stop exits with the preflight NO-GO code' $true ($text -match '(?s)Get-MemoryProblem \(Get-FreeMemoryGB\).*?exit \$script:PreflightExitCode')
Assert-Equal 'the default threshold is 8 GB' $true ($text -match '\[double\] \$MinFreeMemoryGB = 8')
Assert-Equal '-App auto passes the threshold on' $true ($text -match '-MinFreeMemoryGB \$MinFreeMemoryGB')

if ($failed) { Write-Host "$failed memory-guard checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All memory-guard checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
