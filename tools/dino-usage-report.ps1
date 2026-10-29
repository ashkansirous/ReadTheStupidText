<#
.SYNOPSIS
  Dino Reader usage report (Slice 134): runs the Analytics Engine SQL API queries behind the questions in
  docs/plan/slices/134-dino-usage-events-and-report.md and prints one table per question.

.DESCRIPTION
  Reads the dataset `dino_usage` that worker/events.ts writes (needs telemetry switched on in dinoreader/config.json, see "Landing site" in CLAUDE.md).
  Blob layout, one data point per count: blob1 name, blob2 book (package id | own | none), blob3 language, blob4 source (shelf | own | none),
  blob5 has recording (yes | no), blob6 app version, blob7 platform, blob8 UTC day, blob9 detail; and, since Slice 166, the batch's device context
  (empty for builds that send none): blob10 country (two capital letters from the connection, never the IP; none when unknown), blob11 OS major version,
  blob12 form factor (phone | tablet | desktop | other), blob13 CPU architecture (arm64 | x64 | x86 | arm | other), blob14 core bucket (1-4 | 5-8 | 9+),
  blob15 RAM bucket in GB (<3 | 3-4 | 4-6 | 6-8 | 8+; total memory rounded up to whole GiB first, so a 4 GB phone is 4-6); double1 is the count's weight (1 / the app's
  sample rate). Every sum is SUM(_sample_interval * double1): _sample_interval is Analytics Engine's own sampling, double1 the app's.

  Privacy: every query ends in HAVING SUM(_sample_interval) >= $MinEvents, so a group with fewer than 10 recorded events (a rare language,
  a rare book) is never shown. This floor counts EVENTS, not families: no install id exists by design, so one household can reach 10 events
  alone (many voice changes, many opens). It keeps tiny groups out of the report; it is not an anonymity guarantee. A group is dropped, not rounded.

  Token: a Cloudflare API token with "Account Analytics: Read". Keep it in the secrets store (the manage-secrets skill, D:\secrets), never in the repo;
  pass it as -Token or set CLOUDFLARE_API_TOKEN; without either, CF_ANALYTICS_READ_TOKEN is read from D:\secrets\readthestupidtext\secrets.env. The account id is the one that owns the Worker (-AccountId or CLOUDFLARE_ACCOUNT_ID; defaults to 985d711340bd2cf7bf5d71f04a7e84c9).
  Nothing here writes anywhere; use -DryRun to print the SQL without calling the API.

.EXAMPLE
  ./tools/dino-usage-report.ps1 -DryRun
  ./tools/dino-usage-report.ps1 -Days 7 -Only "Top books per language"
#>
[CmdletBinding()]
param(
    [string]$AccountId = $(if ($env:CLOUDFLARE_ACCOUNT_ID) { $env:CLOUDFLARE_ACCOUNT_ID } else { '985d711340bd2cf7bf5d71f04a7e84c9' }),
    [string]$Token = $env:CLOUDFLARE_API_TOKEN,
    [int]$Days = 30,
    [int]$MinEvents = 10,
    [string]$Only,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Slice 167: with no -Token and no CLOUDFLARE_API_TOKEN, read the read-only analytics token from the secrets store.
$SecretsEnv = 'D:\secrets\readthestupidtext\secrets.env'
if (-not $Token -and -not $DryRun -and (Test-Path $SecretsEnv)) {
    $line = Get-Content $SecretsEnv | Where-Object { $_ -match '^\s*CF_ANALYTICS_READ_TOKEN\s*=' } | Select-Object -First 1
    if ($line) { $Token = ($line -replace '^[^=]*=', '').Trim().Trim('"').Trim("'") }
}

$Dataset = 'dino_usage'
$Weighted = 'SUM(_sample_interval * double1)'
# Slice 167: leave out rows already stored from test builds: version 1.0 / 1.0.0 / 0.0.0 (built on a desk) and Android x64 (only emulators are x64 Android).
# Analytics Engine cannot delete rows, so the report filters them. Builds from Slice 167 on send no test counts at all.
# The x64 filter applies only to rows before the app gate shipped; after it, x64 Android is a real device (Chromebook, Intel tablet).
$AppGateShipped = '2026-10-28 00:00:00'
$Window = "timestamp > NOW() - INTERVAL '$Days' DAY AND blob6 NOT IN ('1.0','1.0.0','1.0.0.0','0.0.0','0.0.0.0') AND NOT (blob7 = 'android' AND blob13 = 'x64' AND timestamp < toDateTime('$AppGateShipped'))"
$Enough = "HAVING SUM(_sample_interval) >= $MinEvents"

# Each question: a title and the SQL that answers it. Counts are estimates of the true totals (sampling-weighted).
$Questions = [ordered]@{
    'Top books per language' = @"
SELECT blob3 AS language, blob2 AS book, $Weighted AS opened
FROM $Dataset WHERE blob1 = 'book_opened' AND blob4 = 'shelf' AND $Window
GROUP BY language, book $Enough ORDER BY language, opened DESC LIMIT 200
"@
    'Finished vs opened per book' = @"
SELECT blob2 AS book, blob3 AS language,
  SUM(IF(blob1 = 'book_opened', _sample_interval * double1, 0.0)) AS opened,
  SUM(IF(blob1 = 'book_finished', _sample_interval * double1, 0.0)) AS finished
FROM $Dataset WHERE blob1 IN ('book_opened', 'book_finished') AND blob4 = 'shelf' AND $Window
GROUP BY book, language $Enough ORDER BY opened DESC LIMIT 200
"@
    'Store downloads and filter use' = @"
SELECT blob9 AS filters_in_use, blob3 AS language, $Weighted AS downloads
FROM $Dataset WHERE blob1 = 'store_download' AND $Window
GROUP BY filters_in_use, language $Enough ORDER BY downloads DESC
"@
    'Reading: shelf books vs own books' = @"
SELECT blob4 AS source, $Weighted AS opened
FROM $Dataset WHERE blob1 = 'book_opened' AND $Window
GROUP BY source $Enough
"@
    'Own books added by kind' = @"
SELECT blob9 AS detail, $Weighted AS added
FROM $Dataset WHERE blob1 = 'own_book_added' AND $Window
GROUP BY detail $Enough ORDER BY added DESC
"@
    'Recordings by book and pages recorded' = @"
SELECT blob2 AS book, blob9 AS pages_recorded, $Weighted AS recordings
FROM $Dataset WHERE blob1 = 'recording_made' AND $Window
GROUP BY book, pages_recorded $Enough ORDER BY recordings DESC LIMIT 200
"@
    'Voice changes' = @"
SELECT blob9 AS what_changed, blob3 AS language, $Weighted AS changes
FROM $Dataset WHERE blob1 = 'voice_changed' AND $Window
GROUP BY what_changed, language $Enough ORDER BY changes DESC
"@
    'Language changes (from, to)' = @"
SELECT blob9 AS from_language, blob3 AS to_language, $Weighted AS changes
FROM $Dataset WHERE blob1 = 'reading_language_changed' AND $Window
GROUP BY from_language, to_language $Enough ORDER BY changes DESC
"@
    'Waits the overlay showed (blob9 detail is "stage:duration bucket")' = @"
SELECT blob9 AS wait_and_duration, blob7 AS platform, $Weighted AS shown
FROM $Dataset WHERE blob1 = 'wait_shown' AND $Window
GROUP BY wait_and_duration, platform $Enough ORDER BY shown DESC
"@
    'Waits cancelled (blob9 detail is "stage:duration bucket")' = @"
SELECT blob9 AS wait_and_duration, blob7 AS platform, $Weighted AS cancelled
FROM $Dataset WHERE blob1 = 'wait_cancelled' AND $Window
GROUP BY wait_and_duration, platform $Enough ORDER BY cancelled DESC
"@
    'Intro starts (blob9 detail: first_run or replay)' = @"
SELECT blob9 AS started_from, blob7 AS platform, $Weighted AS starts
FROM $Dataset WHERE blob1 = 'intro_started' AND $Window
GROUP BY started_from, platform $Enough ORDER BY starts DESC
"@
    'Intro step funnel (blob9 detail is "step:tried" or "step:skipped")' = @"
SELECT blob9 AS step_and_action, blob7 AS platform, $Weighted AS taps
FROM $Dataset WHERE blob1 = 'intro_step' AND $Window
GROUP BY step_and_action, platform $Enough ORDER BY step_and_action
"@
    'Story languages saved (blob9 detail is "count:intro|settings"; blob3 = the app language)' = @"
SELECT blob9 AS count_and_where, blob3 AS app_language, $Weighted AS saves
FROM $Dataset WHERE blob1 = 'story_languages_set' AND $Window
GROUP BY count_and_where, app_language $Enough ORDER BY saves DESC
"@
    'Side story languages added (blob3)' = @"
SELECT blob3 AS language, $Weighted AS added
FROM $Dataset WHERE blob1 = 'story_language_added' AND $Window
GROUP BY language $Enough ORDER BY added DESC
"@
    'Story language switches by the child (blob3 = the new language; blob2 = shelf book id, own, or none for the shelf button)' = @"
SELECT blob3 AS language, blob4 AS source, $Weighted AS switches
FROM $Dataset WHERE blob1 = 'story_language_switched' AND $Window
GROUP BY language, source $Enough ORDER BY switches DESC
"@
    'Voice pack downloads (blob9: done, failed, cancelled)' = @"
SELECT blob3 AS language, blob9 AS outcome, blob7 AS platform, $Weighted AS downloads
FROM $Dataset WHERE blob1 = 'voice_pack_download' AND $Window
GROUP BY language, outcome, platform $Enough ORDER BY downloads DESC
"@
    'Intro ends (finished vs skipped all)' = @"
SELECT blob9 AS ended, blob7 AS platform, $Weighted AS ends
FROM $Dataset WHERE blob1 = 'intro_ended' AND $Window
GROUP BY ended, platform $Enough ORDER BY ends DESC
"@
    'App opens by version, platform and screen language' = @"
SELECT blob6 AS version, blob7 AS platform, blob3 AS language, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY version, platform, language $Enough ORDER BY opens DESC
"@
    'App opens by country (blob10; none = no country, XX or Tor)' = @"
SELECT blob10 AS country, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY country $Enough ORDER BY opens DESC
"@
    'App opens by OS major version and platform (blob11; empty = an older build)' = @"
SELECT blob11 AS os_major, blob7 AS platform, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY os_major, platform $Enough ORDER BY opens DESC
"@
    'App opens by form factor (blob12: phone | tablet | desktop | other)' = @"
SELECT blob12 AS form_factor, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY form_factor $Enough ORDER BY opens DESC
"@
    'App opens by CPU architecture (blob13: arm64 | x64 | x86 | arm | other)' = @"
SELECT blob13 AS arch, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY arch $Enough ORDER BY opens DESC
"@
    'App opens by CPU core bucket (blob14: 1-4 | 5-8 | 9+)' = @"
SELECT blob14 AS cores, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY cores $Enough ORDER BY opens DESC
"@
    'App opens by RAM bucket in GB (blob15: <3 | 3-4 | 4-6 | 6-8 | 8+)' = @"
SELECT blob15 AS ram_gb, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY ram_gb $Enough ORDER BY opens DESC
"@
    'Waits by form factor and RAM bucket (are slow waits a low-end problem?)' = @"
SELECT blob12 AS form_factor, blob15 AS ram_gb, blob9 AS wait_and_duration, $Weighted AS shown
FROM $Dataset WHERE blob1 = 'wait_shown' AND $Window
GROUP BY form_factor, ram_gb, wait_and_duration $Enough ORDER BY shown DESC LIMIT 200
"@
}

function Invoke-AnalyticsSql([string]$Sql) {
    $uri = "https://api.cloudflare.com/client/v4/accounts/$AccountId/analytics_engine/sql"
    $headers = @{ Authorization = "Bearer $Token" }
    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body ($Sql + ' FORMAT JSON') -ContentType 'text/plain'
}

# The own-book detail is "kind:pages" (an older app sent the kind alone). It is split here, not in SQL, so the query uses no
# string functions the Analytics Engine SQL API may not support.
function Split-OwnBookDetail($Rows) {
    $Rows | ForEach-Object {
        $parts = ([string]$_.detail).Split(':', 2)
        [pscustomobject]@{ kind = $parts[0]; pages = $(if ($parts.Count -gt 1) { $parts[1] } else { 'unknown (older app)' }); added = $_.added }
    } | Group-Object kind, pages | ForEach-Object {
        $first = $_.Group[0]
        [pscustomobject]@{ kind = $first.kind; pages = $first.pages; added = ($_.Group | Measure-Object added -Sum).Sum }
    } | Sort-Object added -Descending
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

    if ($Title -eq 'Own books added by kind') {
        $rows = Split-OwnBookDetail $rows
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
