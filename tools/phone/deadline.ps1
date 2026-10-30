# The check deadline (Slice 213), shared by common.ps1 (Invoke-Adb caps its timeout) and checks/_lib.ps1 (the wait loops). Dot-sourced, not run: no adb in here.
# It is a global because a check script runs in its own scope; $null removes it.
$script:DeadlineVariable = 'RtstCheckDeadline'
$script:MinimumAdbSeconds = 5   # an adb call never gets less, so a stop-read or cleanup right after the deadline still works

$script:DeadlineEnvVariable = 'RTST_CHECK_DEADLINE_UTC'   # child scripts (preflight.ps1, tap.ps1) read the deadline from here
$script:BudgetExceededPrefix = 'BUDGET: '                  # a throw that starts with this is a TIMEOUT, not a FAIL

function ConvertTo-DeadlineText([datetime]$Deadline) { return $Deadline.ToUniversalTime().ToString('o') }

# $null when the text is empty or not a date.
function ConvertFrom-DeadlineText([string]$Text) {
    $parsed = [datetime]::MinValue
    $styles = [Globalization.DateTimeStyles]::RoundtripKind
    if ($Text -and [datetime]::TryParse($Text, [Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$parsed)) { return $parsed.ToLocalTime() }
    return $null
}

function Set-CheckDeadline($Deadline) {
    if ($null -eq $Deadline) {
        Remove-Variable -Name $script:DeadlineVariable -Scope Global -ErrorAction SilentlyContinue
        [Environment]::SetEnvironmentVariable($script:DeadlineEnvVariable, $null)
        return
    }
    Set-Variable -Name $script:DeadlineVariable -Scope Global -Value $Deadline
    [Environment]::SetEnvironmentVariable($script:DeadlineEnvVariable, (ConvertTo-DeadlineText $Deadline))
}

# This process's deadline, else the one a parent run-checks passed down in the environment, else $null.
function Get-ActiveDeadline {
    $variable = Get-Variable -Name $script:DeadlineVariable -Scope Global -ErrorAction SilentlyContinue
    if ($variable) { return $variable.Value }
    return (ConvertFrom-DeadlineText ([Environment]::GetEnvironmentVariable($script:DeadlineEnvVariable)))
}

function Test-PastDeadline($Deadline, [datetime]$Now) { return [bool]($Deadline -and $Now -ge $Deadline) }

# The message of a call that ran into the end of the budget (reported as TIMEOUT).
function New-BudgetExceededMessage([string]$What) { return "$($script:BudgetExceededPrefix)the time budget ran out while $What." }

# The seconds a step may take: its own wish, or what is left; $null when less than the minimum is left (the caller then skips the step).
function Get-StepTimeout([int]$WantedSeconds, [double]$LeftSeconds, [int]$MinimumSeconds) {
    if ($LeftSeconds -lt $MinimumSeconds) { return $null }
    return [int][math]::Min($WantedSeconds, [math]::Floor($LeftSeconds))
}

# The timeout an adb call may have: its own, or what is left until the deadline (never under MinimumAdbSeconds).
function Limit-SecondsToDeadline([int]$Seconds, $Deadline, [datetime]$Now) {
    if (-not $Deadline) { return $Seconds }
    $left = [int][math]::Ceiling(($Deadline - $Now).TotalSeconds)
    return [math]::Max([math]::Min($Seconds, $left), $script:MinimumAdbSeconds)
}
