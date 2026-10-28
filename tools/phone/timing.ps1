# Pure parsing and table logic for audio-timing.ps1 (Slice 165). Dot-sourced, not run: no adb in here.

# A marker line of `adb logcat -v epoch`: `<seconds.millis> <pid> <tid> <level> <tag>: <message>`.
$script:LogLine = '^\s*(?<t>\d+\.\d+)\s+\d+\s+\d+\s+\w\s+(?<tag>\S+)\s*:\s*(?<msg>.*?)\s*$'

# Turns logcat lines into events: HOOK read start/failed (the read command was accepted; `start` is logged just before the read begins), READ_START, READ_END, CHUNK n (playing), CHUNK_AUDIO n <ms> (audio generated).
function ConvertFrom-TimingLog([string[]]$Lines) {
    foreach ($line in $Lines) {
        if ($line -notmatch $script:LogLine) { continue }
        $time = [double]::Parse($Matches.t, [Globalization.CultureInfo]::InvariantCulture)
        $msg = $Matches.msg -replace '^RTST-TEST\s+', ''
        if ($msg -match '^HOOK read (\w+)$') { [pscustomobject]@{ Time = $time; Kind = 'hook'; Index = 0; Ms = 0; Outcome = $Matches[1] } }
        elseif ($msg -match '^READ_START$') { [pscustomobject]@{ Time = $time; Kind = 'start'; Index = 0; Ms = 0 } }
        elseif ($msg -match '^READ_END$') { [pscustomobject]@{ Time = $time; Kind = 'end'; Index = 0; Ms = 0 } }
        elseif ($msg -match '^CHUNK_AUDIO (\d+) (\d+)ms$') { [pscustomobject]@{ Time = $time; Kind = 'audio'; Index = [int]$Matches[1]; Ms = [int]$Matches[2] } }
        elseif ($msg -match '^CHUNK (\d+)$') { [pscustomobject]@{ Time = $time; Kind = 'chunk'; Index = [int]$Matches[1]; Ms = 0 } }
    }
}

# One read's numbers. Chunk markers count from 1 (playing), audio markers from 0 (generated); Rate is the playback rate.
# Audio durations are at the voice's own pace, so a chunk plays for duration / rate. TapTime is the phone time the read command was handled.
# AudioReadyMs = time from the previous chunk's audio (or the tap) to this chunk's audio being ready; synthesis overlaps playback, so it is NOT synthesis time.
function Get-ReadTimings($Events, [double]$TapTime, [double]$Rate, [int]$GapLimitMs) {
    $events = @($Events)
    $start = $events | Where-Object Kind -eq 'start' | Select-Object -First 1
    $end = $events | Where-Object Kind -eq 'end' | Select-Object -Last 1
    $chunks = @($events | Where-Object Kind -eq 'chunk' | Sort-Object Index)
    $audio = @{}
    $events | Where-Object Kind -eq 'audio' | ForEach-Object { if (-not $audio.ContainsKey($_.Index)) { $audio[$_.Index] = $_ } }
    $rows = for ($i = 0; $i -lt $chunks.Count; $i++) {
        $a = $audio[$i]
        $previousTime = if ($i -eq 0) { $TapTime } elseif ($audio.ContainsKey($i - 1)) { $audio[$i - 1].Time } else { $null }
        $synthMs = if ($a -and $null -ne $previousTime) { [int](($a.Time - $previousTime) * 1000) } else { $null }
        $playMs = if ($a) { [int]($a.Ms / $Rate) } else { $null }
        $gapMs = if ($playMs -ne $null -and $i + 1 -lt $chunks.Count) { [int](($chunks[$i + 1].Time - $chunks[$i].Time) * 1000) - $playMs } else { $null }
        [pscustomobject]@{ Chunk = $chunks[$i].Index; AudioMs = if ($a) { $a.Ms } else { $null }; PlayMs = $playMs; AudioReadyMs = $synthMs; GapAfterMs = $gapMs; Flag = ($null -ne $gapMs -and $gapMs -gt $GapLimitMs) }
    }
    $expectedMs = ($rows | Where-Object { $null -ne $_.PlayMs } | Measure-Object PlayMs -Sum).Sum
    $missing = -not $audio.ContainsKey(0) -or $audio.Count -ne $chunks.Count
    [pscustomobject]@{
        Misaligned     = [bool]($chunks.Count -gt 0 -and $missing)
        Complete       = [bool]($start -and $end -and $chunks.Count -gt 0)
        FirstAudioMs   = if ($chunks.Count) { [int](($chunks[0].Time - $TapTime) * 1000) } else { $null }
        Rows           = @($rows)
        TotalMs        = if ($start -and $end) { [int](($end.Time - [Math]::Min($start.Time, $chunks[0].Time)) * 1000) } else { $null }
        ExpectedMs     = $expectedMs
        FlaggedGaps    = @($rows | Where-Object Flag).Count
    }
}

function Write-TimingTable([string]$Title, $Timing, [int]$GapLimitMs) {
    Write-Host "== $Title"
    if (-not $Timing.Complete) { Write-Host '   incomplete: no READ_START / CHUNK / READ_END markers found'; return }
    if ($Timing.Misaligned) { Write-Host '   WARNING: audio markers missing or misaligned (CHUNK_AUDIO 0 absent or counts differ from CHUNK counts): per-chunk columns are not trustworthy' }
    Write-Host ("   time to first audio: {0} ms (read start to first chunk)" -f $Timing.FirstAudioMs)
    Write-Host ('   {0,5} {1,8} {2,8} {3,8} {4,12}' -f 'chunk', 'audio ms', 'play ms', 'ready ms', 'gap vs audio')
    foreach ($r in $Timing.Rows) {
        $flag = if ($r.Flag) { "  <-- GAP over $GapLimitMs ms" } else { '' }
        Write-Host ('   {0,5} {1,8} {2,8} {3,8} {4,12}{5}' -f $r.Chunk, $r.AudioMs, $r.PlayMs, $r.AudioReadyMs, $r.GapAfterMs, $flag)
    }
    Write-Host ("   total read {0} ms against {1} ms of audio at this rate; gaps flagged: {2}" -f $Timing.TotalMs, $Timing.ExpectedMs, $Timing.FlaggedGaps)
}
