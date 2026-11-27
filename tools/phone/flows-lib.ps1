# Pure helpers of run-flows.ps1 (Slice 448): which Maestro flows a phone run takes, the Maestro command line, the verdict text. Dot-sourced, not run; no adb in here, so
# test-run-flows.ps1 needs no device. The emulator's Find-FlowFiles / Get-FlowName / Test-LogMarker (tools/emulator/common.ps1) are reused, never copied.

$script:FlowAppIds = @{ rtst = 'uk.sirous.readthestupidtext.dbg'; dino = 'uk.sirous.dinoreader.dbg' }
$script:FlowAliasAppIds = @{ rtst = 'uk.sirous.readthestupidtext'; dino = $null }   # an id the flows of an app may declare instead; the dino flows already declare the dev package
$script:FlowNetworkNeeds =@('cellular-only')   # a flow that needs the network cut is never run on the phone: the phone keeps its network (user rule 2026-11-13)

# The flow names the caller asked for (comma list or array, .yaml and paths reduced to the name), no empties, no duplicates, in the order given. Pure.
function ConvertTo-FlowNameList($Names) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($item in @($Names)) {
        foreach ($part in "$item".Split(',')) {
            $name = Get-FlowName $part
            if ($name -and -not $out.Contains($name)) { $out.Add($name) }
        }
    }
    return @($out)
}

# The files of the named flows (Find-FlowFiles finds a flow in the app's folder, else in needs-file/) and the names that matched nothing. Pure over the folder.
function Resolve-FlowRequest([string]$FlowDir, [string[]]$Names) {
    $files = @()
    $missing = @()
    foreach ($name in $Names) {
        $hit = @(Find-FlowFiles $FlowDir $name)
        if ($hit.Count -eq 0) { $missing += $name } else { $files += $hit[0] }
    }
    return [pscustomobject]@{ Files = $files; Missing = $missing }
}

# The `appId:` line of a flow (before the --- line), '' when there is none. Pure.
# Read like tools/emulator/lint-flows.ps1 does (review 2): a trailing YAML comment and surrounding quotes are not part of the id. Pure.
function Get-FlowAppId([string]$FlowText) {
    $m = [regex]::Match($FlowText, '(?m)^appId:[ \t]*(.*?)[ \t]*\r?$')
    if (-not $m.Success) { return '' }
    return ($m.Groups[1].Value -replace '\s+#.*$', '').Trim().Trim('"', "'")
}

# The flow text with its appId line set to $AppId (the line before ---). Pure.
function Set-FlowAppId([string]$FlowText, [string]$AppId) {
    # Review 1: the static [regex]::Replace has no count overload (a 1 there binds to RegexOptions.IgnoreCase), so the instance method sets the count.
    return ([regex]'(?m)^appId:[^\r\n]*').Replace($FlowText, "appId: $AppId", 1)
}

# Check4 1 + 2: `adb forward --list` as an array of lines, one per forward (Invoke-Adb already returns one element per line; never joined into one string).
# $null when adb cannot answer (an exception or a non-zero exit): the caller then removes no forward at all. An empty array is a real, empty list.
function Get-AdbForwardLines([string]$Phone) {
    try { $lines = @(Invoke-Adb $Phone @('forward', '--list')) } catch { return $null }
    if ($LASTEXITCODE -ne 0) { return $null }
    return , @($lines | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
}

# The parsed forwards (Serial, Local, Remote) of `adb forward --list` lines; a line that is not `<serial> <local> <remote>` is dropped. Pure.
function ConvertTo-ForwardSpecs([string[]]$Lines) {
    $specs = @()
    foreach ($line in @($Lines)) {
        $parts = @("$line".Trim() -split '\s+')
        if ($parts.Count -ge 3) { $specs += [pscustomobject]@{ Serial = $parts[0]; Local = $parts[1]; Remote = $parts[2] } }
    }
    return $specs
}

# The local specs (such as tcp:51234) of $Serial that are in $After and not in $Before: the forwards one run made. Check4 2: when either list could not be read ($null),
# nothing is known to be new, so nothing is returned and no forward is ever removed. Pure.
function Get-NewForwardSpecs([string[]]$Before, [string[]]$After, [string]$Serial) {
    if ($null -eq $Before -or $null -eq $After) { return @() }
    $known = @{}
    foreach ($spec in (ConvertTo-ForwardSpecs $Before)) { $known["$($spec.Serial) $($spec.Local) $($spec.Remote)"] = $true }
    $made = @()
    foreach ($spec in (ConvertTo-ForwardSpecs $After)) {
        if ($spec.Serial -ceq $Serial -and -not $known.ContainsKey("$($spec.Serial) $($spec.Local) $($spec.Remote)")) { $made += $spec.Local }
    }
    return $made
}

# Review check2 1 + check3 3 + check4 4: what switches the radios, whatever the header says: Maestro's airplane commands (any word holding "airplane", any case), and in an adb call or a script
# `svc data`, `svc wifi`, `cmd wifi`, `cmd phone`, the settings keys `wifi_on` and `mobile_data`.
$script:FlowNetworkSteps = '[\w-]*(?i:airplane)[\w-]*|(?i:svc\s+(?:data|wifi)|cmd\s+(?:wifi|phone)|wifi_on|mobile_data)'
$script:FlowOrientationStep = 'setOrientation'   # review check3 1: a flow with this changes the phone's rotation settings

# The first text matching $Pattern in the flow text or in any file it includes (runFlow, runScript, file: <name>.yaml / .yml / .js), else ''. Comment lines do not count.
# $FlowPath is the flow's own path (its include targets are relative to its folder); '' skips the includes. $Seen guards a loop of includes. Reads files only.
function Get-FlowStepHit([string]$FlowText, [string]$FlowPath, [string]$Pattern, [string[]]$Seen) {
    $hit = [regex]::Match($FlowText, "(?m)^[^#\r\n]*\b($Pattern)")
    if ($hit.Success) { return $hit.Groups[1].Value }
    if (-not $FlowPath) { return '' }
    $dir = Split-Path -Parent $FlowPath
    foreach ($value in (Get-FlowIncludeValues $FlowText)) {
        $target = Resolve-FlowIncludePath $dir $value
        if (-not $target -or ($Seen -contains $target)) { continue }   # an unresolved include is refused by Get-FlowUnresolvedInclude, never silently run
        $inner = Get-FlowStepHit (Get-Content -Raw -LiteralPath $target) $target $Pattern (@($Seen) + $target)
        if ($inner) { return $inner }
    }
    return ''
}

# Check4 3: the include targets a flow names (runFlow, runScript, file: as `key: value`, `key: {file: value}` or a nested `file:` line), as written; comment lines do not count. Pure.
function Get-FlowIncludeValues([string]$FlowText) {
    $values = @()
    foreach ($m in [regex]::Matches($FlowText, '(?m)^[^#\r\n]*?\b(?:runFlow|runScript|file):[ \t]*([^\r\n]*)\r?$')) {
        $raw = ($m.Groups[1].Value -replace '\s+#.*$', '').Trim()
        if ($raw.StartsWith('{')) {
            $inline = [regex]::Match($raw, '\bfile:[ \t]*["'']?([^,}"'']+)')
            if (-not $inline.Success) { continue }
            $raw = $inline.Groups[1].Value.Trim()
        }
        $raw = $raw.Trim('"', "'")
        if ($raw) { $values += $raw }
    }
    return $values
}

# Check4 3: the file an include value names, relative to the flow's folder; $null when it cannot be resolved here (a ${VARIABLE} or wildcard path, a missing file or folder). Reads the file system only.
function Resolve-FlowIncludePath([string]$Dir, [string]$Value) {
    if ($Value -match '[\$\{\*\?]') { return $null }
    try {
        $target = if ([IO.Path]::IsPathRooted($Value)) { $Value } else { Join-Path $Dir $Value }
        if (Test-Path -LiteralPath $target -PathType Leaf) { return $target }
    } catch { return $null }   # no-fallback: an unparsable path is an include that cannot be checked, which the caller refuses
    return $null
}

# Check4 3: the first include (in the flow, its sub-flows, never inside a .js) whose target cannot be resolved, as written, else ''. A flow with one cannot be checked for network cuts, so it is refused. Reads files only.
function Get-FlowUnresolvedInclude([string]$FlowText, [string]$FlowPath, [string[]]$Seen) {
    $dir = Split-Path -Parent $FlowPath
    foreach ($value in (Get-FlowIncludeValues $FlowText)) {
        $target = Resolve-FlowIncludePath $dir $value
        if (-not $target) { return $value }
        if (($Seen -contains $target) -or $target -notmatch '\.ya?ml$') { continue }
        $inner = Get-FlowUnresolvedInclude (Get-Content -Raw -LiteralPath $target) $target (@($Seen) + $target)
        if ($inner) { return $inner }
    }
    return ''
}

# Review check2 1: the name of the first network step (see $script:FlowNetworkSteps) in the flow, its runFlow sub-flows and any runScript file, else ''.
function Get-FlowNetworkStep([string]$FlowText, [string]$FlowPath, [string[]]$Seen) {
    return Get-FlowStepHit $FlowText $FlowPath $script:FlowNetworkSteps $Seen
}

# Review check3 1: true when the flow (or a sub-flow / script it names) runs setOrientation. Reads files only.
function Test-FlowChangesOrientation([string]$FlowText, [string]$FlowPath) {
    return [bool](Get-FlowStepHit $FlowText $FlowPath $script:FlowOrientationStep @())
}

# Review check2 2: the `adb shell settings` keys the flows' setOrientation changes (persistent system settings: auto-rotate and the fixed rotation).
$script:RotationSettings = @('accelerometer_rotation', 'user_rotation')

# The saved value of one setting from `settings get` output: the digits, or $null when the phone says null / nothing readable. Pure.
function ConvertTo-SettingValue([string]$Output) {
    $m = [regex]::Match("$Output", '^\s*(-?\d+)\s*$')
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

# Review check3 1: what `settings get` said, as State 'value' (Value = the digits), 'unset' (exactly "null": the phone has no value, so a restore deletes the key) or 'unreadable'
# (adb failed, an error text, or a non-zero exit: nothing is known, so nothing is ever written or deleted). Pure.
function Get-SettingReading([string]$Output, [int]$ExitCode) {
    if ($ExitCode -ne 0) { return [pscustomobject]@{ State = 'unreadable'; Value = $null } }
    if ("$Output".Trim() -ceq 'null') { return [pscustomobject]@{ State = 'unset'; Value = $null } }
    $m = [regex]::Match("$Output", '^\s*(-?\d+)\s*$')
    if ($m.Success) { return [pscustomobject]@{ State = 'value'; Value = $m.Groups[1].Value } }
    return [pscustomobject]@{ State = 'unreadable'; Value = $null }
}

# The WARN line for a restore command (as Get-RotationRestoreCommands makes it) that failed: `WARN rotation not restored: <key>=<value>`, value `null` for a delete. Pure.
function Format-RotationWarn([string[]]$Command) {
    $isPut = $Command -contains 'put'
    $value = if ($isPut) { $Command[-1] } else { 'null' }
    $key = if ($isPut) { $Command[-2] } else { $Command[-1] }
    return "WARN rotation not restored: $key=$value"
}

# A clean run whose rotation could not be put back is not green: 0 becomes 1, any other code stays. Pure.
function Get-RunExitCodeAfterRestore([int]$Code, [bool]$NotRestored) {
    if ($NotRestored -and $Code -eq 0) { return 1 }
    return $Code
}

# The adb argument lists that put the saved rotation settings back: `settings put system <key> <value>`, or `settings delete system <key>` for one that had no value. $Saved is key -> value-or-$null. Pure.
function Get-RotationRestoreCommands($Saved) {
    $commands = @()
    foreach ($key in $script:RotationSettings) {
        if (-not $Saved.ContainsKey($key)) { continue }
        if ($null -eq $Saved[$key]) { $commands += , @('shell', 'settings', 'delete', 'system', $key) }
        else { $commands += , @('shell', 'settings', 'put', 'system', $key, "$($Saved[$key])") }
    }
    return $commands
}

# $null when the flow may run on the phone's dev app of this app, else the reason it may not. The store package is never a flow target.
# Review 3: the RTST flows declare the package without .dbg (the lint accepts that), so that id is accepted for rtst and run-flows.ps1 runs a copy with the phone's dev package. Pure.
function Get-FlowRefusal([string]$FlowText, [string]$App, [string]$FlowPath = '') {
    $appId = Get-FlowAppId $FlowText
    $expected = $script:FlowAppIds[$App]
    if ($appId -ne $expected -and $appId -cne $script:FlowAliasAppIds[$App]) { return "its appId is '$appId', the phone's dev app of $App is '$expected'" }
    $networkStep = Get-FlowNetworkStep $FlowText $FlowPath @()
    if ($networkStep) { return "it runs '$networkStep', which would cut the phone's network; run it on the emulator" }
    $unresolved = if ($FlowPath) { Get-FlowUnresolvedInclude $FlowText $FlowPath @() } else { '' }
    if ($unresolved) { return "unresolved include '$unresolved': it cannot be checked for steps that cut the phone's network, so it is not run" }
    foreach ($m in [regex]::Matches($FlowText, '(?m)^#\s*needs:\s*(.+)$')) {
        foreach ($need in $m.Groups[1].Value.Split(',')) {
            if ($script:FlowNetworkNeeds -contains $need.Trim()) { return "it needs '$($need.Trim())', which would cut the phone's network; run it on the emulator" }
        }
    }
    return $null
}

# The `# expect-log:` texts of a flow. Pure.
function Get-FlowExpectedLogs([string]$FlowText) {
    return @([regex]::Matches($FlowText, '(?m)^#\s*expect-log:\s*(.+?)\s*$') | ForEach-Object { $_.Groups[1].Value })
}

# The Maestro arguments for one flow on one device: --device goes before `test` (context7 /mobile-dev-inc/maestro-docs, specify-and-start-devices). --no-reinstall-driver only skips a
# reinstall: Maestro still installs its driver when the phone has none. Pure.
function Get-MaestroFlowArguments([string]$Serial, [string]$DebugOutput, [string]$FlowPath) {
    return "--device $Serial test --no-reinstall-driver --debug-output `"$DebugOutput`" `"$FlowPath`""
}

# True when `dumpsys package <pkg>` says the install is the debuggable build (the only one that carries the test hooks the flows start with). Pure.
function Test-DebuggableInstall([string]$DumpsysText) {
    return [bool]($DumpsysText -match '(?m)\bflags=\[[^\]]*\bDEBUGGABLE\b')
}

# Check4 (device run): the lines of Maestro's own output that say what failed. JVM `WARNING:` lines (sun.misc.Unsafe and the like) and colour codes are dropped; the lines that name a failing
# step (Assertion is false, Element not found, a failure or error word, the cross mark) are kept, else the last lines that remain. At most $Count lines, joined with ' | '. Pure.
function Get-MaestroFailureLines([string]$Text, [int]$Count) {
    $clean = @(("$Text" -replace "\x1B\[[0-9;]*[A-Za-z]", '') -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notmatch '^(?i:WARNING|WARN)\b[: ]' -and $_ -notmatch 'sun\.misc\.Unsafe' })
    $steps = @($clean | Where-Object { $_ -match '(?i)assertion is false|element not found|not found|fail|error|exception|timed out|❌|✗' })
    $pick = if ($steps.Count -gt 0) { $steps } else { $clean }
    return (@($pick) | Select-Object -Last $Count) -join ' | '
}

# Seconds left of the budget at $Now, never below 0. Pure.
function Get-FlowSecondsLeft([datetime]$Start, [datetime]$Now, [int]$BudgetMinutes) {
    return [int][math]::Max(0, [math]::Floor($Start.AddMinutes($BudgetMinutes).Subtract($Now).TotalSeconds))
}

# One PASS / FAIL / SKIP / TIMEOUT line per flow. Pure.
function Format-FlowResultLine([string]$Outcome, [string]$Name, [double]$Seconds, [string]$Detail = '') {
    $line = '{0,-7} {1} ({2:n1} s)' -f $Outcome, $Name, $Seconds
    if ($Detail) { $line += ": $Detail" }
    return $line
}

# The exit code of a run from its outcomes: 0 all passed, 1 a flow failed or timed out, 3 some flows did not run (skipped or out of budget) and none failed. Pure.
function Get-FlowRunExitCode([string[]]$Outcomes) {
    if (@($Outcomes).Count -eq 0) { return 3 }   # review 6: nothing ran (an empty plan) is never green
    if (@($Outcomes | Where-Object { $_ -in 'FAIL', 'TIMEOUT' }).Count -gt 0) { return 1 }
    if (@($Outcomes | Where-Object { $_ -eq 'SKIP' }).Count -gt 0) { return 3 }
    return 0
}
