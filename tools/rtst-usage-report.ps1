<#
.SYNOPSIS
  Read The Stupid Text phone usage report (Slice 137): runs the Analytics Engine SQL API queries behind the questions in
  docs/plan/slices/137-rtst-phone-usage-counts.md and prints one table per question.

.DESCRIPTION
  Reads the dataset `rtst_usage` that worker/events.ts (handleRtstEvents) writes (needs the RTST_USAGE binding to be switched on first, see
  STORE.md, "Phone apps: anonymous usage counts"). Blob layout, one data point per count: blob1 name, blob2 app version, blob3 platform,
  blob4 UTC day, blob5 language, blob6 source language, blob7 mode (original | translation), blob8 voice engine, blob9 voice is default (yes | no),
  blob10 text length bucket, blob11 OS major version, blob12 stage, blob13 outcome, blob14 duration bucket; a field an event does not carry is
  empty. double1 is the count's weight (1 / the app's sample rate). Every sum is SUM(_sample_interval * double1): _sample_interval is Analytics
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
    'Translate offered vs chosen (language pair)' = @"
SELECT blob6 AS from_language, blob5 AS to_language,
  SUM(IF(blob1 = 'translate_offered', _sample_interval * double1, 0)) AS offered,
  SUM(IF(blob1 = 'translate_chosen', _sample_interval * double1, 0)) AS chosen
FROM $Dataset WHERE blob1 IN ('translate_offered', 'translate_chosen') AND $Window
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
