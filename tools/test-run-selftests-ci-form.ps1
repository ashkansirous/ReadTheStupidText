<#
.SYNOPSIS
  Self-test for run-selftests-ci-form.ps1 (Slice 315 item 7): the workflow parser, the CI script text, and one real child shell run with a stale $LASTEXITCODE (the form `-File` hides).
  Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'run-selftests-ci-form.ps1') -LibraryOnly
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$yaml = @'
jobs:
  scope:
    steps:
      - name: Not a selftest
        run: echo hi
  selftests:
    steps:
      - name: First (${{ matrix.shell }})
        run: ./tools/a.ps1

      - name: Lint
        run: |
          Write-Host block
      - name: Second
        run: ./tools/b.ps1 -Quick

      - name: Conditional
        if: matrix.shell == 'pwsh'
        run: ./tools/c.ps1

      - name: With env
        run: ./tools/d.ps1
        env:
          X: '1'

      - name: Own shell
        shell: bash
        run: ./tools/e.sh

      - name: Checkout
        uses: actions/checkout@v4
'@
$steps = @(Get-CiRunLines $yaml)
Assert-Equal 'six run steps of the selftests job (uses: is not a run step)' 6 $steps.Count
Assert-Equal 'first command' './tools/a.ps1' $steps[0].Command
Assert-Equal 'first step is runnable' '' $steps[0].SkipReason
Assert-Equal 'a run: | block is skipped with a reason' $true ($steps[1].SkipReason -like '*multi-line*')
Assert-Equal 'a step with arguments keeps them and runs' './tools/b.ps1 -Quick' $steps[2].Command
Assert-Equal 'a step with arguments is runnable' '' $steps[2].SkipReason
Assert-Equal 'second name' 'Second' $steps[2].Name
Assert-Equal 'an if: step is skipped with a reason' $true ($steps[3].SkipReason -like '*if:*')
Assert-Equal 'an env: step is skipped with a reason' $true ($steps[4].SkipReason -like '*env:*')
Assert-Equal 'a shell: step is skipped with a reason' $true ($steps[5].SkipReason -like '*shell:*')

$real = @(Get-CiRunLines ([IO.File]::ReadAllText($Workflow)))
$runnable = @($real | Where-Object { -not $_.SkipReason })
Assert-Equal 'the real workflow has self-test steps' $true ($runnable.Count -gt 10)
Assert-Equal 'every runnable real step is a ./tools script' 0 @($runnable | Where-Object { $_.Command -notmatch '^\./tools/\S+\.ps1(\s.*)?$' }).Count
Assert-Equal 'the PSScriptAnalyzer lint block is listed as skipped, not dropped' $true (@($real | Where-Object { $_.SkipReason -like '*multi-line*' }).Count -ge 1)

$text = New-CiScriptText './x.ps1'
Assert-Equal 'script text starts with the stop preference' $true $text.StartsWith("`$ErrorActionPreference = 'stop'")
Assert-Equal 'script text ends with the exit forward' $true $text.Contains('exit $LASTEXITCODE')

# Item 4: a NOT RUN shell, or no executed step, is never exit 0.
Assert-Equal 'all steps ran and passed: 0' 0 (Get-CiFormExitCode 0 0 3)
Assert-Equal 'a failing step: 1' 1 (Get-CiFormExitCode 1 0 3)
Assert-Equal 'a shell NOT RUN: 1' 1 (Get-CiFormExitCode 0 1 3)
Assert-Equal 'nothing executed: 1' 1 (Get-CiFormExitCode 0 0 0)

# The CI form fails a script that ends with a stale non-zero $LASTEXITCODE even though it printed "all passed": this is the Slice 284 / test-layout-check failure.
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('rtst-ci-form-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force $tmp
try {
    $stale = Join-Path $tmp 'stale.ps1'
    Set-Content -LiteralPath $stale -Value "cmd /c exit 3`r`nWrite-Host 'all passed'"
    $explicit = Join-Path $tmp 'explicit.ps1'
    Set-Content -LiteralPath $explicit -Value "cmd /c exit 3`r`nWrite-Host 'all passed'`r`nexit 0"
    $shellName = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }
    # `-command` reports a script's non-zero exit as 1 (seen on pwsh 7.6 and 5.1), so only "not 0" is asserted.
    Assert-Equal 'stale LASTEXITCODE fails in the CI form' $true ((Invoke-CiStep $shellName "& '$stale'" $tmp) -ne 0)
    Assert-Equal 'an explicit exit 0 passes in the CI form' 0 (Invoke-CiStep $shellName "& '$explicit'" $tmp)
    # Item 6: on a timeout the child process of the step dies too, not only the shell.
    $pidFile = Join-Path $tmp 'child.pid'
    $spawn = "`$c = Start-Process $shellName -ArgumentList '-NoProfile','-command','Start-Sleep 120' -PassThru; Set-Content -LiteralPath '$pidFile' -Value `$c.Id; Start-Sleep 120"
    $script:StepTimeoutSeconds = 8
    Assert-Equal 'a timed-out step returns 124' 124 (Invoke-CiStep $shellName $spawn $tmp)
    $script:StepTimeoutSeconds = 600
    $childPid = if (Test-Path -LiteralPath $pidFile) { [int](Get-Content -LiteralPath $pidFile) } else { 0 }
    Assert-Equal 'the step started a child' $true ($childPid -gt 0)
    Start-Sleep -Seconds 1
    Assert-Equal 'the child of a timed-out step is killed too' $false (($childPid -gt 0) -and [bool](Get-Process -Id $childPid -ErrorAction SilentlyContinue))
    # Finding 7: the way CI starts a script (`-command ". '<script>'"`) must run it, not return at once: -List prints the steps and exits 0.
    $info = New-Object Diagnostics.ProcessStartInfo $shellName
    $info.Arguments = "-NoProfile -NonInteractive -command `". '$(Join-Path $PSScriptRoot 'run-selftests-ci-form.ps1')' -List -Shell $shellName`""
    $info.RedirectStandardOutput = $true
    $info.UseShellExecute = $false
    $listProc = [Diagnostics.Process]::Start($info)
    $listOut = $listProc.StandardOutput.ReadToEnd()
    $listProc.WaitForExit()
    Assert-Equal 'dot-sourced the CI way the script still runs (-List exit)' 0 $listProc.ExitCode
    Assert-Equal 'dot-sourced the CI way the script lists the steps' $true ($listOut -match 'SKIPPED \[' -and $listOut -match "\[$shellName\] \./tools/")
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-run-selftests-ci-form: all passed.'
exit 0
