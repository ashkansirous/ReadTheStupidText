# Pure logic for run-checks.ps1 and the check scripts (Slice 213). Dot-sourced, not run: no adb in here, so test-run-checks.ps1 needs no device.
. (Join-Path $PSScriptRoot '..\timing.ps1')     # ConvertFrom-TimingLog and the logcat line pattern
. (Join-Path $PSScriptRoot '..\deadline.ps1')   # Set-CheckDeadline, Get-ActiveDeadline

$script:DefaultBudgetMinutes = 10
$script:BuildMinutes = 7
$script:VariantAot = 'aot'
$script:VariantDebug = 'debug'   # the plain Debug build (no AOT)
# Exit codes of run-checks.ps1 (2 and 4 match connect.ps1 and lease.ps1).
$script:ExitAllPassed = 0
$script:ExitCheckFailed = 1
$script:ExitNoPhone = 2
$script:ExitSomeSkipped = 3
$script:ExitLeaseHeldByOther = 4
$script:ExitPreflightNoGo = 5
$script:ExitBuildFailed = 6
$script:ExitRunnerError = 7   # the runner itself broke (install, setup, a dropped adb link): not a failed check
# Preflight lines that stop the run; every other FAIL (dev app missing, stay awake, lease) is a warning here, because the runner sets those right itself.
# 'free space' is a FAIL only when there is no room at all (preflight.ps1); the runner never changes a phone setting except stay-awake (preflight.ps1 -Fix).
$script:FatalPreflightChecks = @('connection', 'charging', 'screen lock', 'free space')
$script:StatusPass = 'PASS'
$script:StatusFail = 'FAIL'
$script:StatusTimeout = 'TIMEOUT'
$script:StatusSkipped = 'SKIP'
$script:StatusPending = 'Pending'
$script:StatusMeasured = 'MEASURED'   # numbers without a pass criterion (cold-start): never PASS
# Waits before the checks start count against the budget and are capped (Slice 213 review).
$script:PreWaitCapMinutes = 3
$script:PreflightGoLine = 'PREFLIGHT GO'
$script:PreflightNoGoPrefix = 'PREFLIGHT NO-GO'
$script:PreflightFailPattern = '^\s*\[FAIL\]\s+([^:]+):\s*(.*)$'
$script:MaxLeaseMinutesAllowed = 1440
$script:PercentPattern = '\d+\s?%'

# Name -> Apps it can run on, Variant ('aot' = needs the AOT build, e.g. every timing; 'debug' = any build will do, the Debug one when the AOT one is not ready), EstimateSeconds (what the budget is checked against; PerRunSeconds x -Runs when the check repeats), NeedsPack (a voice pack must be installed), Blocked (why it cannot run yet, else $null).
$script:Catalogue = [ordered]@{
    'cold-start'          = @{ Apps = @('rtst', 'dino'); EstimateSeconds = 240; PerRunSeconds = 80; NeedsPack = $true; Variant = 'aot'; Blocked = $null }
    'highlight'           = @{ Apps = @('rtst'); EstimateSeconds = 120; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'notification-text'   = @{ Apps = @('rtst'); EstimateSeconds = 150; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'short-lines'         = @{ Apps = @('dino'); EstimateSeconds = 200; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'book-end'            = @{ Apps = @('dino'); EstimateSeconds = 420; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'language'            = @{ Apps = @('rtst', 'dino'); EstimateSeconds = 180; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'translate'           = @{ Apps = @('rtst'); EstimateSeconds = 150; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'voice'               = @{ Apps = @('rtst'); EstimateSeconds = 90; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
    'book-update'         = @{ Apps = @('dino'); EstimateSeconds = 240; NeedsPack = $false; Variant = 'debug'; Blocked = $null }
    'paused-notification' = @{ Apps = @('rtst'); EstimateSeconds = 240; NeedsPack = $true; Variant = 'debug'; Blocked = $null }
}

# One row per requested check, in order, duplicates dropped: Status Pending (runs) or Skipped with the reason. An unknown name throws and lists the known ones.
function Resolve-CheckSelection([string[]]$Requested, [string]$App, $Catalogue, [int]$Runs = 3) {
    $seen = @{}
    foreach ($name in $Requested) {
        if ($seen.ContainsKey($name)) { continue }
        $seen[$name] = $true
        if (-not $Catalogue.Contains($name)) { throw "Unknown check '$name'. Known checks: $($Catalogue.Keys -join ', ')." }
        $entry = $Catalogue[$name]
        $reason = if ($entry.Blocked) { "blocked: $($entry.Blocked)" } elseif ($entry.Apps -notcontains $App) { "not for $App (runs on: $($entry.Apps -join ', '))" } else { $null }
        $estimate = if ($entry.PerRunSeconds) { $entry.PerRunSeconds * [math]::Max($Runs, 1) } else { $entry.EstimateSeconds }
        [pscustomobject]@{ Name = $name; Status = $(if ($reason) { $script:StatusSkipped } else { $script:StatusPending }); Reason = $reason; EstimateSeconds = $estimate; NeedsPack = $entry.NeedsPack; Variant = $entry.Variant }
    }
}

# 'aot' or 'debug' -> the platform part of the build cache name (the same one audio-timing.ps1 uses for the phone: dev package, ABI, AOT flag).
function Get-CachePlatform([string]$Variant) {
    return 'android-dbg-arm64' + $(if ($Variant -eq $script:VariantAot) { '-aot' } else { '' })
}


# Whether a check may start: it needs its estimate to fit into what is left of the budget. Returns Run and, when it does not run, the reason.
function Get-BudgetDecision([int]$EstimateSeconds, [double]$ElapsedSeconds, [int]$BudgetMinutes) {
    $left = [int][math]::Floor($BudgetMinutes * 60 - $ElapsedSeconds)
    if ($left -gt 0 -and $EstimateSeconds -le $left) { return [pscustomobject]@{ Run = $true; Reason = $null; LeftSeconds = $left } }
    return [pscustomobject]@{ Run = $false; LeftSeconds = [math]::Max($left, 0); Reason = "budget: needs ~$EstimateSeconds s, $([math]::Max($left, 0)) s left of $BudgetMinutes min" }
}

# While a check runs, its wait loops keep run-status.json fresh (the runner is busy inside the check): rewrites updatedUtc at most every HeartbeatSeconds. Never throws.
function Send-RunHeartbeat {
    $path = [Environment]::GetEnvironmentVariable($script:RunStatusEnv)
    $last = Get-Variable -Name RtstHeartbeatAt -Scope Global -ValueOnly -ErrorAction SilentlyContinue
    $now = Get-Date
    if (-not $path -or ($last -and ($now - $last).TotalSeconds -lt $script:HeartbeatSeconds)) { return }
    Set-Variable -Name RtstHeartbeatAt -Scope Global -Value $now
    try {
        $status = Get-Content $path -Raw | ConvertFrom-Json
        $status.updatedUtc = ConvertTo-DeadlineText $now
        [IO.File]::WriteAllText($path, ($status | ConvertTo-Json -Depth 6))
    } catch { Write-Verbose "heartbeat skipped: $($_.Exception.Message)" }
}

# Throws a BUDGET error once the deadline passed; every wait loop calls it.
function Assert-WithinBudget([string]$What) {
    Send-RunHeartbeat
    $deadline = Get-ActiveDeadline
    if (Test-PastDeadline $deadline (Get-Date)) { throw (New-BudgetExceededMessage $What) }
}

function New-CheckResult([bool]$Passed, [string]$Detail, [bool]$Measured = $false) {
    $result = [pscustomobject]@{ Passed = $Passed; Detail = $Detail; Measured = $Measured }
    $result.PSObject.TypeNames.Insert(0, 'RtstCheckResult')
    return $result
}

# New-CheckResult with $Measured = $true is a check with no pass criterion: it reports numbers and is MEASURED in the table, never PASS.

# A check script returns a result object (everything else it prints is progress); a throw is a FAIL, or a TIMEOUT when the budget ended it.
function Resolve-CheckOutcome($Output, [string]$ErrorMessage, [bool]$PastDeadline = $false) {
    if ($ErrorMessage) {
        if ($PastDeadline -and -not $ErrorMessage.StartsWith($script:BudgetExceededPrefix)) { $ErrorMessage = $script:BudgetExceededPrefix + $ErrorMessage }
        if ($ErrorMessage.StartsWith($script:BudgetExceededPrefix)) { return [pscustomobject]@{ Status = $script:StatusTimeout; Detail = $ErrorMessage.Substring($script:BudgetExceededPrefix.Length) } }
        return [pscustomobject]@{ Status = $script:StatusFail; Detail = $ErrorMessage }
    }
    $result = @($Output | Where-Object { $_.PSObject.TypeNames -contains 'RtstCheckResult' }) | Select-Object -Last 1
    if (-not $result) { return [pscustomobject]@{ Status = $script:StatusFail; Detail = 'the check returned no result' } }
    $status = if (-not $result.Passed) { $script:StatusFail } elseif ($result.Measured) { $script:StatusMeasured } else { $script:StatusPass }
    return [pscustomobject]@{ Status = $status; Detail = $result.Detail }
}

# Whether the lease record on the phone's key is this owner's (a missing record or another owner is not).
function Test-OwnsLease($Held, [string]$Owner) { return [bool]($Held -and $Held.Owner -ceq $Owner) }

function Get-RunExitCode($Results) {
    $statuses = @($Results | ForEach-Object { $_.Status })
    if ($statuses -contains $script:StatusFail -or $statuses -contains $script:StatusTimeout) { return $script:ExitCheckFailed }
    if ($statuses -contains $script:StatusSkipped) { return $script:ExitSomeSkipped }
    return $script:ExitAllPassed
}

# The one table at the end. Seconds is empty for a skipped check.
function Format-CheckSummary($Results) {
    $lines = @('{0,-26} {1,-8} {2,7}  {3}' -f 'check', 'result', 'seconds', 'detail')
    foreach ($r in $Results) {
        $seconds = if ($null -ne $r.Seconds) { [string][int]$r.Seconds } else { '' }
        $name = if ($r.Variant) { "$($r.Name) [$($r.Variant)]" } else { $r.Name }
        $lines += ('{0,-26} {1,-8} {2,7}  {3}' -f $name, $r.Status, $seconds, $r.Detail)
    }
    return $lines
}

# Reads preflight.ps1's output and its exit code. Go only on its explicit `PREFLIGHT GO` last line (exit 0). Its `PREFLIGHT NO-GO` last line is also a Go when it
# printed [FAIL] lines and every one is a non-fatal check (the runner sets those right itself: stay awake, dev app, lease; they print as warnings). Anything else
# (no verdict line, an `adb:` reason with no [FAIL] line, the pairing ask, a crash, a fatal FAIL) is NO-GO. Fatal = FAIL lines named in FatalPreflightChecks.
function Resolve-PreflightVerdict([string[]]$Lines, [int]$ExitCode = 0) {
    $fatal = @(); $other = @()
    foreach ($line in $Lines) {
        if ("$line" -notmatch $script:PreflightFailPattern) { continue }
        $entry = "$($Matches[1].Trim()): $($Matches[2])"
        if ($script:FatalPreflightChecks -contains $Matches[1].Trim()) { $fatal += $entry } else { $other += $entry }
    }
    $last = @($Lines | Where-Object { "$_".Trim() -ne '' } | ForEach-Object { "$_".Trim() }) | Select-Object -Last 1
    $go = $false
    if ($last -ceq $script:PreflightGoLine -and $ExitCode -eq 0) { $go = $true }
    elseif ("$last".StartsWith($script:PreflightNoGoPrefix) -and $ExitCode -eq 1 -and $fatal.Count -eq 0 -and $other.Count -gt 0) { $go = $true }
    if (-not $go -and $fatal.Count -eq 0) { $fatal = @("no usable verdict from preflight (last line '$last', exit $ExitCode)") }
    return [pscustomobject]@{ Go = $go; Fatal = $fatal; Other = $other }
}

# The one line the runner prints for the verdict: GO (with the warnings it carries on with) or STOP with the reasons. preflight's own PREFLIGHT NO-GO line is never echoed.
function Format-PreflightLine($Verdict) {
    if (-not $Verdict.Go) { return "preflight: STOP - $($Verdict.Fatal -join '; ')" }
    if ($Verdict.Other.Count -eq 0) { return 'preflight: GO' }
    return "preflight: GO (warnings, carrying on: $($Verdict.Other -join '; '))"
}

# ---- cold-start first-read text (Slice 214): the three sizes of the first chunk. The reader cuts the head of a read into chunks of up to 2 sentences, so each preset is one first chunk.
$script:ColdStartPresets = [ordered]@{
    '1w' = 'Hello.'
    '1s' = 'The little dinosaur walked down to the river every morning.'
    '2s' = 'The little dinosaur walked down to the river every morning. She watched the water run over the smooth grey stones.'
}
$script:ColdStartPresetDefault = 'passage'   # no preset: the fixed passage (ReaderPassage), as before

# A preset name (1w, 1s, 2s, passage) or free text -> [pscustomobject]@{ Label; Text } (Text $null = the fixed passage). -Text wins over -Preset.
function Resolve-ColdStartText([string]$Preset, [string]$Text) {
    if ($Text) { return [pscustomobject]@{ Label = 'custom'; Text = $Text } }
    if (-not $Preset -or $Preset -eq $script:ColdStartPresetDefault) { return [pscustomobject]@{ Label = $script:ColdStartPresetDefault; Text = $null } }
    if (-not $script:ColdStartPresets.Contains($Preset)) { throw "Unknown first-read preset '$Preset'. Known: $($script:ColdStartPresets.Keys -join ', '), $($script:ColdStartPresetDefault)." }
    return [pscustomobject]@{ Label = $Preset; Text = $script:ColdStartPresets[$Preset] }
}

# ---- cold-start: the numbers of one fresh-process read, from the log lines of that process. $TapTime is the phone time of the `HOOK read start` line.
function Get-ColdStartNumbers($Events, [double]$TapTime) {
    $chunk = $Events | Where-Object Kind -eq 'chunk' | Select-Object -First 1
    $engine = $Events | Where-Object Kind -eq 'engine' | Select-Object -First 1
    $synth = $Events | Where-Object { $_.Kind -eq 'synth-end' -and $_.Index -eq 0 } | Select-Object -First 1
    $audio = $Events | Where-Object { $_.Kind -eq 'audio' -and $_.Index -eq 0 } | Select-Object -First 1
    return [pscustomobject]@{
        FirstAudioMs = if ($chunk) { [int](($chunk.Time - $TapTime) * 1000) } else { $null }
        EngineMs     = if ($engine -and $engine.Ms -ge 0) { $engine.Ms } else { $null }
        FirstSynthMs = if ($synth) { $synth.Ms } else { $null }
        FirstAudioLengthMs = if ($audio) { $audio.Ms } else { $null }   # length of chunk 0's audio: the size of the first chunk, since the log never holds the text
    }
}

function Get-Median([double[]]$Values) {
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    $mid = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$mid] }
    return ($sorted[$mid - 1] + $sorted[$mid]) / 2
}

# ---- highlight: `SENTENCE <chunk> <n>` (ReaderPage, Slice 190) is written when the highlight moves to sentence n (n >= 1) of a chunk that is playing.
function ConvertFrom-SentenceLog([string[]]$Lines) {
    foreach ($line in $Lines) {
        if ($line -notmatch $script:LogLine) { continue }
        $time = [double]::Parse($Matches.t, [Globalization.CultureInfo]::InvariantCulture)
        $msg = $Matches.msg -replace '^RTST-TEST\s+', ''
        if ($msg -match '^SENTENCE (\d+) (\d+)$') { [pscustomobject]@{ Time = $time; Kind = 'sentence'; Chunk = [int]$Matches[1]; Sentence = [int]$Matches[2] } }
        elseif ($msg -match '^READ_END$') { [pscustomobject]@{ Time = $time; Kind = 'end'; Chunk = 0; Sentence = 0 } }
    }
}

# True when some chunk's highlight moved to a later sentence before the read ended (or while it still runs): proof the highlight moves inside a playing chunk.
function Test-HighlightMoved($Events) {
    $end = $Events | Where-Object Kind -eq 'end' | Select-Object -First 1
    $moves = @($Events | Where-Object { $_.Kind -eq 'sentence' -and $_.Sentence -ge 1 -and (-not $end -or $_.Time -lt $end.Time) })
    return ($moves.Count -gt 0)
}

# ---- notification-text: the pack download's notification in `dumpsys notification --noredact`.
$script:NotificationRecordStart = 'NotificationRecord\('
$script:NotificationTimePattern = '(?:mUpdateTimeMs|mCreationTimeMs|\bwhen)=(\d+)'   # the dump's post time; the exact field is not confirmed on the S21, so ties keep the dump order

# Every record of $Package in the dump (a record starts at "NotificationRecord(" and holds `pkg=<package>`): Title, Text, PostTime (0 when the dump has none),
# Order (position in the dump) and Raw (all its lines).
function Get-NotificationRecords([string[]]$Dump, [string]$Package) {
    $records = New-Object System.Collections.ArrayList
    $current = $null; $order = 0
    foreach ($line in $Dump) {
        if ("$line" -match $script:NotificationRecordStart) {
            $current = $null
            if ("$line" -match ('\bpkg=' + [regex]::Escape($Package) + '\b')) {
                $current = [pscustomobject]@{ Title = ''; Text = ''; PostTime = [int64]0; Order = $order++; Raw = "$line" }
                [void]$records.Add($current)
            }
            continue
        }
        if (-not $current) { continue }
        $current.Raw += "`n$line"
        if ("$line" -match 'android\.title=\S+ \((.*)\)\s*$') { $current.Title = $Matches[1] }
        elseif ("$line" -match 'android\.text=\S+ \((.*)\)\s*$') { $current.Text = $Matches[1] }
        elseif (-not $current.PostTime -and "$line" -match $script:NotificationTimePattern) { $current.PostTime = [int64]$Matches[1] }
    }
    return @($records)
}

# The download's notification: only a record that names the pack (its id or language code, as a whole word) counts, the newest by post time (later in the dump wins a tie);
# $null when none does, so an unrelated notification of the dev app never passes. With no names at all, the newest of all the dev package's records.
function Select-DownloadNotification($Records, $PackNames) {
    $all = @($Records)
    $names = @($PackNames | Where-Object { $_ })
    $pool = $all
    if ($names.Count -gt 0) { $pool = @($all | Where-Object { Test-RecordNamesPack $_.Raw $names }) }
    return ($pool | Sort-Object -Property @{ Expression = 'PostTime'; Descending = $true }, @{ Expression = 'Order'; Descending = $true } | Select-Object -First 1)
}

function Test-RecordNamesPack([string]$Raw, $Names) {
    foreach ($name in $Names) {
        if ($Raw -match ('(?i)(?<![A-Za-z0-9])' + [regex]::Escape($name) + '(?![A-Za-z0-9])')) { return $true }
    }
    return $false
}

# Slice 208: the download text names the pack's language and size ("Downloading the English voice (63 MB)... 12%"), so the PASS rule also requires it.
# The names to look for are the language names the check passes in (not the pack id), and the size is "<n> MB".
$script:ExpectPackNameInNotification = $true
$script:SizePattern = '\(\d+ MB\)'

# The notification the download just posted: a record of the dev package posted at or after $SinceMs (phone epoch ms; a record with no post time in the dump never counts: it
# could be left over from an earlier run) whose title or text has a percentage. Matched by package and post time, not by pack name, unless ExpectPackNameInNotification (Slice 208) is on.
# The newest by post time wins (later in the dump wins a tie); $null when none.
function Select-PostedProgressNotification($Records, [int64]$SinceMs, $PackNames = @(), [bool]$ExpectPackName = $script:ExpectPackNameInNotification) {
    $pool = @(@($Records) | Where-Object {
        $_.PostTime -gt 0 -and $_.PostTime -ge $SinceMs -and ("$($_.Title) $($_.Text)" -match $script:PercentPattern)
    })
    if ($ExpectPackName) { $pool = @($pool | Where-Object { ($_.Raw -match $script:SizePattern) -and (Test-RecordNamesPack $_.Raw @($PackNames | Where-Object { $_ })) }) }
    return ($pool | Sort-Object -Property @{ Expression = 'PostTime'; Descending = $true }, @{ Expression = 'Order'; Descending = $true } | Select-Object -First 1)
}

# True when a record of the dev package shows a percentage but has no readable post time (the dump format lacks it).
function Test-PostTimeMissing($Records) {
    return [bool](@($Records) | Where-Object { $_.PostTime -le 0 -and ("$($_.Title) $($_.Text)" -match $script:PercentPattern) } | Select-Object -First 1)
}

# The PASS rule: a progress notification was shown. Detail quotes title and text verbatim.
function Resolve-NotificationVerdict($Note, [bool]$PostTimeUnknown = $false) {
    if (-not $Note -and $PostTimeUnknown) { return [pscustomobject]@{ Passed = $false; Detail = 'the dump gives no post time for the dev app notifications, so a fresh one cannot be told from an old one (not an app bug: check the notification by hand, or the post-time pattern for this phone)' } }
    if (-not $Note) { return [pscustomobject]@{ Passed = $false; Detail = 'no download notification with a percentage was posted after the download started (notifications off for the dev app, or only other notifications of it appeared)' } }
    return [pscustomobject]@{ Passed = $true; Detail = "notification title '$($Note.Title)', text '$($Note.Text)'" }
}

# The newest record of $Package (any notification of it), or $null when it has none.
function Get-NotificationText([string[]]$Dump, [string]$Package) {
    $records = Get-NotificationRecords $Dump $Package
    return (Select-DownloadNotification $records '')
}

# $null when the title reads as a download status with a percentage; otherwise why not (an empty title, a raw resource key, an unfilled {0}).
function Get-NotificationTitleProblem([string]$Title) {
    if ($Title.Trim() -eq '') { return 'the title is empty' }
    if ($Title -match '\{\d+\}|PackStatus_|StoryLanguages_') { return "the title shows a raw placeholder: '$Title'" }
    if ($Title -notmatch $script:PercentPattern) { return "the title has no percentage: '$Title'" }
    return $null
}

# ---- book-end: can the phone doze? A held wake lock of the app's uid, or its foreground service, keeps it awake.
function Get-PackageUid([string]$PackageDump) {
    if ("$PackageDump" -match '(?:userId|appId)=(\d+)') { return $Matches[1] }
    return $null
}

# The locks held right now: the lines of the "Wake Locks: size=N" block of dumpsys power. The block ends at the first line that is not a lock; the wakelock history
# further down (lines starting [n], marked DISABLED) keeps old locks forever and is not live.
function Get-LiveWakeLocks([string]$PowerDump) {
    $live = @(); $inBlock = $false
    foreach ($line in ("$PowerDump" -split "`r?`n")) {
        if (-not $inBlock) { $inBlock = ($line -match '^\s*Wake Locks:\s*size=\d+'); continue }
        if ($line -notmatch '^\s*[A-Z_]*WAKE_LOCK\b') { break }
        $live += $line
    }
    return $live
}

# True when a live lock is the app's: its uid or its WorkSource (Dino's keep-screen-on is a SCREEN_BRIGHT_WAKE_LOCK of uid 1000 with ws={app uid}), or the app runs a foreground service.
function Test-AppHoldsWake([string]$PowerDump, [string]$ServicesDump, [string]$Uid) {
    $lock = $false
    if ($Uid) { $lock = [bool](Get-LiveWakeLocks $PowerDump | Where-Object { $_ -match ('\buid=' + $Uid + '\b|WorkSource\{[^}]*\b' + $Uid + '\b') }) }
    return ($lock -or ("$ServicesDump" -match 'isForeground=true'))
}

# ---- budget and waits before the checks (Slice 213 review: the clock starts when the runner starts)
# When the budget clock effectively started: the run start moved later by the time spent only waiting for a build (that time is not budget).
function Get-BudgetStart([datetime]$RunStart, [double]$WaitedSeconds) { return $RunStart.AddSeconds($WaitedSeconds) }

# A fresh budget clock (Slice 216 review): the run start and the waited seconds are one pair, so they restart together and can never disagree.
function New-BudgetClock([datetime]$Now) {
    return [pscustomobject]@{ RunStart = $Now; WaitedSeconds = [double]0 }
}

# Every wait before or during the checks (the phone lease, the quiet machine) measures from the one budget start, never from the raw run start.
function Get-BudgetPreWaitMinutes([datetime]$RunStart, [double]$WaitedSeconds, [datetime]$Now, [int]$BudgetMinutes) {
    return (Get-PreWaitMinutes (Get-BudgetStart $RunStart $WaitedSeconds) $Now $BudgetMinutes)
}

# Seconds of budget used at $Now: measured from the same budget start.
function Get-BudgetElapsedSecondsAt([datetime]$RunStart, [double]$WaitedSeconds, [datetime]$Now) {
    return ($Now - (Get-BudgetStart $RunStart $WaitedSeconds)).TotalSeconds
}

# Minutes a pre-check wait (the phone lease, the quiet machine) may take: the cap, or what is left of the budget when that is less. Never negative.
function Get-PreWaitMinutes([datetime]$RunStart, [datetime]$Now, [int]$BudgetMinutes) {
    $leftMinutes = $BudgetMinutes - ($Now - $RunStart).TotalMinutes
    return [math]::Max([math]::Min($script:PreWaitCapMinutes, $leftMinutes), 0)
}

# The exit code for an error that stopped the device stage: a lost phone lease is exit 4 (as on the other paths), anything else is a runner error.
function Get-StageErrorExitCode([string]$Message, [string]$LeaseLostReason) {
    if ($Message -ceq $LeaseLostReason) { return $script:ExitLeaseHeldByOther }
    return $script:ExitRunnerError
}

# One row per plan, in order: the row a check produced, else the plan's own skip row, else SKIP with $Reason. Used on every path that ends early, so the table always names every check.
function Get-FinalRows($Plans, $DoneRows, [string]$Reason) {
    foreach ($plan in $Plans) {
        $done = @($DoneRows | Where-Object { $_.Name -ceq $plan.Name }) | Select-Object -First 1
        if ($done) { $done; continue }
        $why = if ($plan.Status -ceq $script:StatusSkipped -and $plan.Reason) { $plan.Reason } else { $Reason }
        [pscustomobject]@{ Name = $plan.Name; Status = $script:StatusSkipped; Detail = $why; Seconds = $null; Variant = $null }
    }
}

# ---- the phone lease (Slice 213 review): never shorten a lease the caller holds
# Minutes to write: the budget plus its margin, or what the caller's own lease still has when that is more; 1 to MaxLeaseMinutesAllowed.
function Get-LeaseMinutes([int64]$ExistingExpires, [int64]$Now, [int]$WantedMinutes) {
    $remaining = if ($ExistingExpires -gt $Now) { [int][math]::Ceiling(($ExistingExpires - $Now) / 60.0) } else { 0 }
    return [math]::Min([math]::Max([math]::Max($remaining, $WantedMinutes), 1), $script:MaxLeaseMinutesAllowed)
}

# ---- the installed dev build (Slice 213 review): a different variant is never replaced silently
$script:InstallMarkerPrefix = '/sdcard/Download/rtst-run-checks-install-'   # + the dev package
$script:InstallMarkerFields = 3
$script:PlatformPrefix = '^android-'
$script:ActionInstall = 'Install'
$script:ActionKeep = 'Keep'
$script:ActionSkip = 'Skip'

# What run-checks wrote after its own install: "<platform> <key> <lastUpdateTime with _ for spaces>". $null for anything else.
function ConvertFrom-InstallMarker([string]$Text) {
    $fields = "$Text".Trim() -split '\s+'
    if ($fields.Count -ne $script:InstallMarkerFields -or $fields[0] -notmatch $script:PlatformPrefix) { return $null }
    return [pscustomobject]@{ Platform = $fields[0]; Key = $fields[1]; Updated = $fields[2] }
}

function ConvertTo-InstallMarker([string]$Platform, [string]$Key, [string]$LastUpdate) {
    $updated = $LastUpdate.Trim() -replace '\s+', '_'
    return "$Platform $Key $updated"
}

function Get-VariantOfPlatform([string]$Platform) {
    if ($Platform -like '*-aot') { return $script:VariantAot }
    return $script:VariantDebug
}

# `lastUpdateTime=` of `dumpsys package`, spaces as _; $null when the package is not installed.
function Get-PackageLastUpdate([string]$PackageDump) {
    if ("$PackageDump" -match 'lastUpdateTime=([^\r\n]+)') { return ($Matches[1].Trim() -replace '\s+', '_') }
    return $null
}

function New-InstallPlan([string]$Action, [string]$Reason) {
    $plan = [pscustomobject]@{ Action = $Action; Reason = $Reason }
    return $plan
}

# What to do about the phone's dev app: Install (not installed, or another build or variant over one run-checks itself installed: adb install -r keeps data), Keep (same build), Skip (an install run-checks did not make).
function Resolve-InstallPlan($Marker, $InstalledUpdate, [string]$Platform, [string]$Key, [bool]$Reinstall) {
    $wanted = Get-VariantOfPlatform $Platform
    if (-not $InstalledUpdate) { return (New-InstallPlan $script:ActionInstall 'not installed') }
    $known = $Marker -and $Marker.Updated -ceq $InstalledUpdate
    if (-not $known -and $Reinstall) { return (New-InstallPlan $script:ActionInstall 'installed build not placed; -Reinstall') }
    if (-not $known) { return (New-InstallPlan $script:ActionSkip 'installed variant unknown (not installed by run-checks): run with -Reinstall once, knowingly, to put the cached build on') }
    $installed = Get-VariantOfPlatform $Marker.Platform
    if ($installed -ceq $wanted -and $Marker.Key -ceq $Key -and $Reinstall) { return (New-InstallPlan $script:ActionInstall "installing the cached $wanted build again (-Reinstall; adb install -r keeps data)") }
    if ($installed -ceq $wanted -and $Marker.Key -ceq $Key) { return (New-InstallPlan $script:ActionKeep "already the cached $wanted build") }
    return (New-InstallPlan $script:ActionInstall "installing the cached $wanted build over $installed (adb install -r keeps data)")
}

# adb install's proof: exit code 0, `Success` in its output and a lastUpdateTime that moved. $null when all three hold, else why not (with the adb output).
function Resolve-InstallProof([int]$ExitCode, [string]$AdbOutput, $UpdateBefore, $UpdateAfter) {
    $text = "$AdbOutput".Trim()
    if ($ExitCode -ne 0) { return "adb install exited ${ExitCode}: $text" }
    if ($text -notmatch 'Success') { return "adb install did not say Success: $text" }
    if (-not $UpdateAfter) { return 'the package is not visible to dumpsys package after adb install' }
    if ($UpdateBefore -and $UpdateAfter -ceq $UpdateBefore) { return "adb install said Success but lastUpdateTime did not change ($UpdateAfter): the build was not replaced. adb said: $text" }
    return $null
}

# ---- -Prepare: the runner downloads a missing pack OUTSIDE the budget (run-checks.ps1 -Prepare); a timed run never downloads
$script:PackStallSeconds = 120          # the .partial file not growing for this long is a stall (FAIL)
$script:PackProgressSeconds = 30
$script:PackPollSeconds = 5
$script:PackStartSeconds = 60           # the app must log PACKDL start within this long of the launch
$script:PrepareMaxSeconds = 1800        # a prepare that has not finished a download in 30 minutes is a FAIL
$script:AppLogTailLines = 10
$script:AppLogMarker = 'RTST-TEST'
$script:PackStartPattern = 'PACKDL start pack=(\S+)'
$script:PackDonePattern = 'PACKDL done pack=(\S+)'
$script:PackFailedPattern = 'PACKDL failed pack=(\S+)'
$script:PartialFileGlob = 'files/packs/*.partial'
$script:NoPackReason = 'no pack: run -Prepare first'

# 'Failed' (a PACKDL failed line for one of the ids), 'Done', 'Started' or 'Idle' (none yet); the line that decided it comes with it. Failed outranks Done outranks Started.
function Resolve-PackDownloadLog([string[]]$Lines, [string[]]$PackIds) {
    $best = [pscustomobject]@{ State = 'Idle'; Line = $null }
    $rank = @{ Idle = 0; Started = 1; Done = 2; Failed = 3 }
    $patterns = @{ Started = $script:PackStartPattern; Done = $script:PackDonePattern; Failed = $script:PackFailedPattern }
    foreach ($line in $Lines) {
        foreach ($state in $patterns.Keys) {
            if ("$line" -match $patterns[$state] -and $PackIds -contains $Matches[1] -and $rank[$state] -gt $rank[$best.State]) { $best = [pscustomobject]@{ State = $state; Line = "$line".Trim() } }
        }
    }
    return $best
}

# The last $Count lines that carry the RTST-TEST marker (what the app said about itself), oldest first.
function Get-AppLogTail([string[]]$Lines, [int]$Count) {
    $marked = @($Lines | Where-Object { "$_" -like "*$($script:AppLogMarker)*" })
    return @($marked | Select-Object -Last $Count)
}

# The reason when the app never logged PACKDL start: quotes the app's last lines so the cause is visible.
function Get-NoPackStartReason([string[]]$Lines, [string[]]$PackIds) {
    $tail = @(Get-AppLogTail $Lines $script:AppLogTailLines)
    $quoted = if ($tail.Count -gt 0) { ($tail | ForEach-Object { "$_".Trim() }) -join ' | ' } else { "(no $($script:AppLogMarker) lines at all)" }
    return "no PACKDL start for $($PackIds -join '/') within $($script:PackStartSeconds) s of the launch. The app's last $($script:AppLogTailLines) $($script:AppLogMarker) lines: $quoted"
}

# The largest size in `stat -c %s` output (several .partial files, or none: 0).
function Get-PartialBytes([string[]]$StatLines) {
    $sizes = @($StatLines | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [long]$_ })
    if ($sizes.Count -eq 0) { return [long]0 }
    return ($sizes | Measure-Object -Maximum).Maximum
}

# True when the size has not grown for StallSeconds. LastGrowth is when it last grew (or the download started).
function Test-PackStalled([datetime]$LastGrowth, [datetime]$Now, [int]$StallSeconds) {
    return (($Now - $LastGrowth).TotalSeconds -ge $StallSeconds)
}

function Format-PackProgress([long]$Bytes, $TotalBytes = $null) {
    $mb = [math]::Round($Bytes / 1MB, 1)
    if ($TotalBytes) { return "$mb MB of $([math]::Round($TotalBytes / 1MB, 1)) MB" }
    return "$mb MB (total size not known)"
}

# Pack ids the neural reader of each app needs (PackConstants.SupertonicPackId; Dino's storyteller is kokoro-en); any one of them is enough.
$script:RequiredPacks = @{ rtst = @('supertonic-3'); dino = @('supertonic-3', 'kokoro-en') }

# ---- cold-start report
function Format-ColdStartRuns($Runs) {
    $index = 0
    $parts = foreach ($run in $Runs) { $index++; 'run {0}: first audio {1} ms (engine {2}, synthesis {3}, chunk 0 audio {4} ms)' -f $index, $run.FirstAudioMs, $run.EngineMs, $run.FirstSynthMs, $run.FirstAudioLengthMs }
    return ($parts -join '; ')
}

# ---- logcat read incrementally
# The newest epoch time in log lines (or $Current when none is newer): the next read asks `logcat -T` from there.
function Get-LogCursor([string[]]$Lines, [double]$Current) {
    $newest = $Current
    foreach ($line in $Lines) {
        if ("$line" -notmatch '^\s*(\d+\.\d+)') { continue }
        $time = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
        if ($time -gt $newest) { $newest = $time }
    }
    return $newest
}

# ---- book-end proof that the book played, and that the wake dumps could be read
$script:BookOpenedPattern = 'HOOK open-book opened'
$script:MinimumBookChunks = 2
function Resolve-BookPlayProof([string[]]$Lines) {
    $events = @(ConvertFrom-TimingLog $Lines)
    $chunks = @($events | Where-Object Kind -eq 'chunk').Count
    $opened = [bool]($Lines | Where-Object { "$_" -match $script:BookOpenedPattern })
    $ended = [bool]($events | Where-Object Kind -eq 'end')
    $problem = $null
    if (-not $opened) { $problem = "no '$($script:BookOpenedPattern)' line (the book did not open)" }
    elseif ($chunks -lt $script:MinimumBookChunks) { $problem = "only $chunks CHUNK line(s), need $($script:MinimumBookChunks): the book did not play" }
    elseif (-not $ended) { $problem = 'no READ_END line' }
    return [pscustomobject]@{ Played = ($null -eq $problem); Chunks = $chunks; Problem = $problem }
}

# $null when both dumps look like the real thing; otherwise why not (an empty or error dump would read as "nothing is held").
function Get-WakeDumpProblem([string]$PowerDump, [string]$ServicesDump) {
    if ("$PowerDump" -notmatch 'Wake Locks:|mWakefulness') { return 'dumpsys power did not return the power state' }
    if ("$ServicesDump" -notmatch 'ACTIVITY MANAGER SERVICES|ServiceRecord|\(nothing\)') { return 'dumpsys activity services did not return a service list' }
    return $null
}

# ---- highlight: the editor shows exactly the typed text (tap.ps1 -List prints "text | id | clickable")
$script:ListSeparator = ' | '
function Test-EditorHoldsText([string[]]$ListLines, [string]$EditorId, [string]$Expected) {
    foreach ($line in $ListLines) {
        $parts = "$line".Trim() -split [regex]::Escape($script:ListSeparator)
        if ($parts.Count -lt 3 -or -not $parts[-2].Trim().EndsWith($EditorId)) { continue }
        return (($parts[0..($parts.Count - 3)] -join $script:ListSeparator).Trim() -ceq $Expected)
    }
    return $false
}

# ---- builds in the background and which build a check uses (Slice 213)
# At the start the runner starts the Debug and the AOT build in the background (each skipped when cached). A check that needs AOT (cold-start, any timing) waits for it;
# the others start on whichever build is ready first (AOT preferred when both are). At most one swap on the phone, Debug -> AOT, never back.
$script:StateQueued = 'queued'
$script:StateBuilding = 'building'
$script:StateOk = 'ok'
$script:StateFailed = 'failed'
$script:BuildStallMinutes = 5            # a build whose log has not grown for this long is failed (stalled)
$script:BuildLogTailLines = 20
$script:HeartbeatSeconds = 30            # build status files and run-status.json are rewritten at least this often
$script:ActionRun = 'Run'
$script:ActionWait = 'Wait'
$script:ActionFail = 'Fail'
$script:RunStatusEnv = 'RTST_RUN_STATUS_FILE'

# A time read back from a status file: pwsh's ConvertFrom-Json turns ISO strings into DateTime, Windows PowerShell 5.1 leaves strings.
function ConvertFrom-StatusTime($Value) {
    if ($Value -is [datetime]) { return $Value.ToLocalTime() }
    return (ConvertFrom-DeadlineText "$Value")
}

# The state of one background build, from its status file ($null when none yet), whether its process has exited, when its log last grew, and whether the APK is in the cache.
# Returns State (queued/building/ok/failed) and Reason (why failed, else $null).
function Resolve-BuildState($Status, [bool]$ProcessExited, $LogWrite, [datetime]$Now, [bool]$Cached) {
    if ($Cached) { return [pscustomobject]@{ State = $script:StateOk; Reason = $null } }
    $failed = { param($why) [pscustomobject]@{ State = $script:StateFailed; Reason = $why } }
    if (-not $Status) { if ($ProcessExited) { return (& $failed 'the build process exited without writing a status') }; return [pscustomobject]@{ State = $script:StateQueued; Reason = $null } }
    if ($Status.state -ceq $script:StateFailed) { return (& $failed "$($Status.error)") }
    if ($Status.state -ceq $script:StateOk) { return (& $failed 'the build said ok but the cache has no APK for the current inputs (the files changed during the build?)') }
    if ($ProcessExited) { return (& $failed 'the build process exited without a result') }
    if ($Status.state -ceq $script:StateBuilding) {
        $since = if ($LogWrite) { $LogWrite } else { ConvertFrom-StatusTime $Status.startedUtc }
        if ($since -and ($Now - $since).TotalMinutes -ge $script:BuildStallMinutes) { return (& $failed "stalled: the build log has not grown for $($script:BuildStallMinutes) minutes") }
    }
    return [pscustomobject]@{ State = "$($Status.state)"; Reason = $null }
}

# What the build's failure says in a table row: the reason and the last lines of its log.
function Format-BuildFailure([string]$Variant, [string]$Reason, [string[]]$LogTail) {
    $tail = @($LogTail | Where-Object { "$_".Trim() -ne '' } | Select-Object -Last $script:BuildLogTailLines)
    $lines = if ($tail.Count -gt 0) { " Last $($tail.Count) log lines: " + (($tail | ForEach-Object { "$_".Trim() }) -join ' | ') } else { ' (the build log is empty)' }
    return "the $Variant build failed: $Reason.$lines"
}

# One plan against the builds: Run (with the variant to run on), Wait (with what for) or Fail (with why). $Builds maps 'debug'/'aot' to {State; Failure}.
# $Installed is the variant on the phone now, or $null. A check that needs AOT never falls back to Debug; one that does not keeps what is installed (no swap back).
function Resolve-PlanStep($Plan, $Builds, $Installed) {
    $aot = $Builds[$script:VariantAot]; $debug = $Builds[$script:VariantDebug]
    $step = { param($action, $variant, $detail) [pscustomobject]@{ Plan = $Plan; Action = $action; Variant = $variant; Detail = $detail } }
    if ($Plan.Variant -ceq $script:VariantAot) {
        if ($aot.State -ceq $script:StateOk) { return (& $step $script:ActionRun $script:VariantAot $null) }
        if ($aot.State -ceq $script:StateFailed) { return (& $step $script:ActionFail $script:VariantAot $aot.Failure) }
        return (& $step $script:ActionWait $null "waiting for the aot build ($($aot.State)) for $($Plan.Name)")
    }
    if ($Installed) { return (& $step $script:ActionRun $Installed $null) }
    if ($aot.State -ceq $script:StateOk) { return (& $step $script:ActionRun $script:VariantAot $null) }
    if ($debug.State -ceq $script:StateOk) { return (& $step $script:ActionRun $script:VariantDebug $null) }
    if ($debug.State -ceq $script:StateFailed -and $aot.State -ceq $script:StateFailed) { return (& $step $script:ActionFail $script:VariantDebug "$($debug.Failure) The aot build failed too: $($aot.Failure)") }
    if ($debug.State -ceq $script:StateFailed) { return (& $step $script:ActionWait $null "the debug build failed; waiting for the aot build ($($aot.State)) for $($Plan.Name)") }
    return (& $step $script:ActionWait $null "waiting for the debug or aot build ($($debug.State)/$($aot.State)) for $($Plan.Name)")
}

# The next thing to do: a failure first (it costs no time and shows at once), then the first check that can run, else Wait with everything it waits for.
function Get-NextStep($Pending, $Builds, $Installed) {
    $steps = @($Pending | ForEach-Object { Resolve-PlanStep $_ $Builds $Installed })
    $first = { param($action) $steps | Where-Object { $_.Action -ceq $action } | Select-Object -First 1 }
    $fail = & $first $script:ActionFail
    if ($fail) { return $fail }
    $run = & $first $script:ActionRun
    if ($run) { return $run }
    return [pscustomobject]@{ Plan = $null; Action = $script:ActionWait; Variant = $null; Detail = (($steps | ForEach-Object { $_.Detail }) -join '; ') }
}

# The run-status.json the coordinator reads from outside: each build's state, each check's state, when it was last updated. $Rows are the finished rows; $Running names the check running now.
# The one line that names every build this run starts (not cached) and why: the checks of this run that wait for it, or that nothing in this run does.
function Format-BuildsStartLine($Builds, $Plans) {
    $parts = foreach ($variant in @($script:VariantDebug, $script:VariantAot)) {
        if (-not $Builds[$variant].Process) { continue }
        $names = @($Plans | Where-Object { $_.Status -ceq $script:StatusPending -and $_.Variant -ceq $variant } | ForEach-Object { $_.Name })
        $why = if ($names.Count -gt 0) { "for $($names -join ', ')" } else { 'for later runs; nothing in this run waits for it' }
        "$App $variant ($why)"
    }
    if (-not $parts) { return 'starting builds: none (both are cached)' }
    return "starting builds: $(@($parts) -join '; ')"
}

function New-RunStatus([string]$App, [datetime]$Now, $Builds, $Plans, $Rows, [string]$Running, [string]$Waiting, [string]$Installed, [bool]$Finished = $false) {
    $checks = foreach ($plan in $Plans) {
        $row = @($Rows | Where-Object { $_.Name -ceq $plan.Name }) | Select-Object -First 1
        $state = if ($row) { $row.Status } elseif ($plan.Status -ceq $script:StatusSkipped) { $script:StatusSkipped } elseif ($plan.Name -ceq $Running) { 'running' } elseif ($Waiting -and $Waiting -like "*$($plan.Name)*") { 'waiting' } else { 'pending' }
        $ranOn = if ($row -and $row.Variant) { $row.Variant } else { $plan.Variant }   # a check that ran records the variant it ran on (the installed one), not the declared one
        [ordered]@{ name = $plan.Name; variant = $ranOn; state = $state; detail = $(if ($row) { $row.Detail } else { $null }) }
    }
    $buildStates = [ordered]@{}
    foreach ($variant in @($script:VariantDebug, $script:VariantAot)) { $buildStates[$variant] = [ordered]@{ state = $Builds[$variant].State; failure = $Builds[$variant].Failure } }
    return [ordered]@{ app = $App; updatedUtc = (ConvertTo-DeadlineText $Now); installedVariant = $Installed; finished = $Finished; waiting = $Waiting; builds = $buildStates; checks = @($checks) }
}

# ---- short-lines (Slice 220): the guard's Debug probe line and the retry / rescue / gave-up lines of one read (no text in any of them).
# `short line probe (voice kokoro:x, 4 chars): plain [samples 0, peak 0.000, voiced ms 0]; dots [samples 9600, peak 0.310, voiced ms 380]; commas [...]`
function ConvertFrom-ShortLineLog([string[]]$Lines) {
    $probe = $null; $gaveUp = $false; $rescue = $null; $retries = @()
    foreach ($line in $Lines) {
        if ($line -match 'short line probe \(voice [^,]+, (\d+) chars\): plain \[([^\]]*)\]; dots \[([^\]]*)\]; commas \[([^\]]*)\]') {
            $probe = [pscustomobject]@{ Plain = (ConvertFrom-TakeFacts $Matches[2]); Dots = (ConvertFrom-TakeFacts $Matches[3]); Commas = (ConvertFrom-TakeFacts $Matches[4]) }
        }
        elseif ($line -match 'gave up \(voice') { $gaveUp = $true }
        elseif ($line -match 'silent voice output rescued by (padded )?(take \d|the sibling voice)') { $rescue = ($Matches[1] + $Matches[2]).Trim() }
        elseif ($line -match 'retry take (\d) \((plain|padded),') { $retries += "$($Matches[1]):$($Matches[2])" }
    }
    return [pscustomobject]@{ Probe = $probe; GaveUp = $gaveUp; Rescue = $rescue; Retries = $retries }
}

# `samples 9600, peak 0.310, voiced ms 380` -> Samples, Peak, VoicedMs; Silent is true when voiced ms is 0 (zero samples, or every sample under the voiced amplitude 0.01).
function ConvertFrom-TakeFacts([string]$Text) {
    if ($Text -notmatch 'samples (\d+), peak ([\d.]+), voiced ms (\d+)(?:, density ([\d.]+))?') { return $null }
    $peak = [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)
    $density = if ($Matches[4]) { [double]::Parse($Matches[4], [Globalization.CultureInfo]::InvariantCulture) } else { $null }
    return [pscustomobject]@{ Samples = [int]$Matches[1]; Peak = $peak; VoicedMs = [int]$Matches[3]; Density = $density; Silent = ([int]$Matches[3] -eq 0) }
}

function Format-TakeFacts($Facts) {
    if (-not $Facts) { return 'n/a' }
    $kind = if ($Facts.Samples -eq 0) { 'ZERO SAMPLES' } elseif ($Facts.Silent) { 'ALL-ZERO' } else { 'voiced' }
    $density = if ($null -ne $Facts.Density) { ", density $($Facts.Density)" } else { '' }
    return "$($Facts.Samples) smp, peak $($Facts.Peak), $($Facts.VoicedMs) ms$density ($kind)"
}

function Format-ShortLineRow($Row) {
    $probe = if ($Row.Probe) { 'plain {0} | dots {1} | commas {2}' -f (Format-TakeFacts $Row.Probe.Plain), (Format-TakeFacts $Row.Probe.Dots), (Format-TakeFacts $Row.Probe.Commas) } else { 'no probe line' }
    $guard = if ($Row.GaveUp) { 'GAVE UP' } elseif ($Row.Rescue) { "rescued by $($Row.Rescue)" } else { 'first take ok' }
    return "$probe; $guard; retries $(@($Row.Retries) -join ',')"
}

# Per take kind: how many probes were silent (voiced ms 0), and how many of those had zero samples. Plain silent and padded voiced together confirm the padding fix.
function Get-ShortLineSummary($Rows) {
    $probes = @($Rows | Where-Object { $_.Probe } | ForEach-Object { $_.Probe })
    if ($probes.Count -eq 0) { return 'no probes' }
    $parts = foreach ($kind in 'Plain', 'Dots', 'Commas') {
        $takes = @($probes | ForEach-Object { $_.$kind } | Where-Object { $_ })
        $silent = @($takes | Where-Object { $_.Silent })
        "$kind silent $($silent.Count)/$($takes.Count) (zero samples $(@($silent | Where-Object { $_.Samples -eq 0 }).Count))"
    }
    return ($parts -join '; ') + "; gave up $(@($Rows | Where-Object { $_.GaveUp }).Count)/$($Rows.Count)"
}
