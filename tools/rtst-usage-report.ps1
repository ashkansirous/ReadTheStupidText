<#
.SYNOPSIS
  Read The Stupid Text phone usage report (Slice 137): runs the Analytics Engine SQL API queries behind the questions in
  docs/plan/slices/137-rtst-phone-usage-counts.md and prints one table per question.

.DESCRIPTION
  Reads the dataset `rtst_usage` that worker/events.ts (handleRtstEvents) writes (needs telemetry switched on in readthestupidtext/config.json, see
  STORE.md, "Phone apps: anonymous usage counts"). Blob layout, one data point per count: blob1 name, blob2 app version, blob3 platform,
  blob4 UTC day, blob5 language, blob6 source language, blob7 mode (original | translation), blob8 voice engine, blob9 voice is default (yes | no),
  blob10 text length bucket, blob11 OS major version, blob12 stage, blob13 outcome, blob14 duration bucket, blob15 source (Windows reads only: selection | clipboard | hotkey | panel), blob16 early (timing counts only: yes when the head was prepared before the tap, Slice 139), blob17 wait stage (wait_shown and wait_cancelled only, Slice 148: preparing_voice | stalled | downloading_model | translating | reading_pictures | saving | getting_file; blob14 is how long the overlay showed); a field an event does not carry is
  empty. Since Slice 166 every count also carries the batch's device context (empty or -1 for builds that send none): blob18 country (two capital letters from the connection, never the IP; none when unknown), blob19 form factor (phone | tablet | desktop | other), blob20 CPU architecture (arm64 | x64 | x86 | arm | other), and blob11 OS major version is now filled on every count; double2 is the CPU core bucket's lower bound (1 | 5 | 9) and double3 the RAM bucket's lower bound in GB (0 | 3 | 4 | 6 | 8), -1 when not sent (rows written before this layout read 0, so the double queries keep only rows with a form factor in blob19). The RAM bucket is taken after rounding the total up to whole GiB (a phone sold as 4 GB reports about 3.7 GiB and counts as 4-6). double1 is the count's weight (1 / the app's sample rate). Every sum is SUM(_sample_interval * double1): _sample_interval is Analytics
  Engine's own sampling, double1 the app's.

  Privacy: every query ends in HAVING SUM(_sample_interval) >= $MinEvents, so a group with fewer than 10 recorded events (a rare language,
  a rare pair) is never shown. This floor counts EVENTS, not people: no install id exists by design, so one person can reach 10 events alone.
  It keeps tiny groups out of the report; it is not an anonymity guarantee. A group is dropped, not rounded.

  Token: a Cloudflare API token with "Account Analytics: Read". Keep it in the secrets store (the manage-secrets skill, D:\secrets), never in the repo;
  pass it as -Token or set CLOUDFLARE_API_TOKEN. The account id is the one that owns the Worker (-AccountId or CLOUDFLARE_ACCOUNT_ID).
  Nothing here writes anywhere; use -DryRun to print the SQL without calling the API.

.EXAMPLE
  ./tools/rtst-usage-report.ps1 -DryRun
  ./tools/rtst-usage-report.ps1 -Days 7 -Only "Translate offered"
#>
[CmdletBinding()]
param(
    [string]$AccountId = $env:CLOUDFLARE_ACCOUNT_ID,
    [string]$Token = $env:CLOUDFLARE_API_TOKEN,
    [int]$Days = 30,
    [int]$MinEvents = 10,
    [string]$Only,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$Dataset = 'rtst_usage'
$Weighted = 'SUM(_sample_interval * double1)'
$Window = "timestamp > NOW() - INTERVAL '$Days' DAY"
$Enough = "HAVING SUM(_sample_interval) >= $MinEvents"

# Each question: a title and the SQL that answers it. Counts are estimates of the true totals (sampling-weighted).
$Questions = [ordered]@{
    'Languages read (read language, by platform)' = @"
SELECT blob5 AS read_language, blob3 AS platform, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND $Window
GROUP BY read_language, platform $Enough ORDER BY reads DESC
"@
    'Texts by detected language' = @"
SELECT blob6 AS detected_language, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND $Window
GROUP BY detected_language $Enough ORDER BY reads DESC
"@
    'Windows reads by source (what started the read)' = @"
SELECT blob15 AS source, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND blob3 = 'windows' AND $Window
GROUP BY source $Enough ORDER BY reads DESC
"@
    'Translate offered vs chosen (language pair; phone only: on Windows translate_chosen means the switch was turned on)' = @"
SELECT blob6 AS from_language, blob5 AS to_language,
  SUM(IF(blob1 = 'translate_offered', _sample_interval * double1, 0)) AS offered,
  SUM(IF(blob1 = 'translate_chosen', _sample_interval * double1, 0)) AS chosen
FROM $Dataset WHERE blob1 IN ('translate_offered', 'translate_chosen') AND blob3 <> 'windows' AND $Window
GROUP BY from_language, to_language $Enough ORDER BY offered DESC LIMIT 200
"@
    'Original vs translation reads' = @"
SELECT blob7 AS mode, blob3 AS platform, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND $Window
GROUP BY mode, platform $Enough ORDER BY reads DESC
"@
    'Voice engine in use, and how many keep the default voice' = @"
SELECT blob8 AS engine, blob9 AS default_voice, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND $Window
GROUP BY engine, default_voice $Enough ORDER BY reads DESC
"@
    'Voice changes (language, engine)' = @"
SELECT blob5 AS language, blob8 AS engine, $Weighted AS changes
FROM $Dataset WHERE blob1 = 'voice_changed' AND $Window
GROUP BY language, engine $Enough ORDER BY changes DESC
"@
    'Timing: how long each wait takes (stage, platform, outcome, duration)' = @"
SELECT blob12 AS stage, blob3 AS platform, blob13 AS outcome, blob14 AS duration, $Weighted AS waits
FROM $Dataset WHERE blob1 = 'timing' AND $Window
GROUP BY stage, platform, outcome, duration $Enough ORDER BY stage, platform, outcome, duration
"@
    'Timing: translations by language pair (stage, pair, duration)' = @"
SELECT blob12 AS stage, blob6 AS from_language, blob5 AS to_language, blob14 AS duration, $Weighted AS waits
FROM $Dataset WHERE blob1 = 'timing' AND blob12 IN ('model_download', 'translate') AND $Window
GROUP BY stage, from_language, to_language, duration $Enough ORDER BY stage, waits DESC LIMIT 200
"@
    'Timing: early start vs not (stage, platform, early, duration)' = @"
SELECT blob12 AS stage, blob3 AS platform, blob16 AS early, blob14 AS duration, $Weighted AS waits
FROM $Dataset WHERE blob1 = 'timing' AND blob12 IN ('first_audio', 'translate') AND $Window
GROUP BY stage, platform, early, duration $Enough ORDER BY stage, platform, early, duration
"@
    'Waits the overlay showed, and the ones cancelled (wait, platform, duration)' = @"
SELECT blob17 AS wait_stage, blob3 AS platform, blob14 AS duration, $Weighted AS shown,
  SUM(IF(blob1 = 'wait_cancelled', _sample_interval * double1, 0)) AS cancelled
FROM $Dataset WHERE blob1 IN ('wait_shown', 'wait_cancelled') AND $Window
GROUP BY wait_stage, platform, duration $Enough ORDER BY wait_stage, platform, duration
"@
    'Text length of reads' = @"
SELECT blob10 AS length_bucket, $Weighted AS reads
FROM $Dataset WHERE blob1 = 'read_started' AND $Window
GROUP BY length_bucket $Enough ORDER BY reads DESC
"@
    'App opens by version, platform, OS and app language' = @"
SELECT blob2 AS version, blob3 AS platform, blob11 AS os_major, blob5 AS app_language, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY version, platform, os_major, app_language $Enough ORDER BY opens DESC
"@
    'App opens by country (blob18; none = no country, XX or Tor)' = @"
SELECT blob18 AS country, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY country $Enough ORDER BY opens DESC
"@
    'App opens by OS major version and platform (blob11)' = @"
SELECT blob11 AS os_major, blob3 AS platform, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY os_major, platform $Enough ORDER BY opens DESC
"@
    'App opens by form factor (blob19: phone | tablet | desktop | other)' = @"
SELECT blob19 AS form_factor, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY form_factor $Enough ORDER BY opens DESC
"@
    'App opens by CPU architecture (blob20: arm64 | x64 | x86 | arm | other)' = @"
SELECT blob20 AS arch, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY arch $Enough ORDER BY opens DESC
"@
    'App opens by CPU core bucket (double2: lower bound 1 | 5 | 9; older builds are left out: their unwritten doubles read 0)' = @"
SELECT double2 AS cores_from, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND blob19 <> '' AND $Window
GROUP BY cores_from $Enough ORDER BY opens DESC
"@
    'App opens by RAM bucket (double3: lower bound in GB 0 | 3 | 4 | 6 | 8; older builds are left out: their unwritten doubles read 0)' = @"
SELECT double3 AS ram_gb_from, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND blob19 <> '' AND $Window
GROUP BY ram_gb_from $Enough ORDER BY opens DESC
"@
    'Timing: first-audio waits by RAM bucket (are slow reads a low-end problem?)' = @"
SELECT double3 AS ram_gb_from, blob14 AS duration, $Weighted AS waits
FROM $Dataset WHERE blob1 = 'timing' AND blob12 = 'first_audio' AND blob19 <> '' AND $Window
GROUP BY ram_gb_from, duration $Enough ORDER BY ram_gb_from, waits DESC
"@
}

function Invoke-AnalyticsSql([string]$Sql) {
    $uri = "https://api.cloudflare.com/client/v4/accounts/$AccountId/analytics_engine/sql"
    $headers = @{ Authorization = "Bearer $Token" }
    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body ($Sql + ' FORMAT JSON') -ContentType 'text/plain'
}

function Show-Question([string]$Title, [string]$Sql) {
    Write-Host "`n== $Title (last $Days days, groups under $MinEvents events hidden) =="
    if ($DryRun) {
        Write-Host $Sql
        return
    }

    $rows = (Invoke-AnalyticsSql $Sql).data
    if (-not $rows) {
        Write-Host '(nothing at or above the minimum)'
        return
    }

    $rows | Format-Table -AutoSize | Out-String | Write-Host
}

if (-not $DryRun -and ((-not $AccountId) -or (-not $Token))) {
    throw 'Set -AccountId and -Token (or CLOUDFLARE_ACCOUNT_ID / CLOUDFLARE_API_TOKEN); the token lives in the secrets store.'
}

foreach ($title in $Questions.Keys) {
    if (-not $Only -or $title -like "*$Only*") {
        Show-Question $title $Questions[$title]
    }
}
