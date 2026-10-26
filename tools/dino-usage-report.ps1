<#
.SYNOPSIS
  Dino Reader usage report (Slice 134): runs the Analytics Engine SQL API queries behind the questions in
  docs/plan/slices/134-dino-usage-events-and-report.md and prints one table per question.

.DESCRIPTION
  Reads the dataset `dino_usage` that worker/events.ts writes (needs the USAGE binding to be switched on first, see "Landing site" in CLAUDE.md).
  Blob layout, one data point per count: blob1 name, blob2 book (package id | own | none), blob3 language, blob4 source (shelf | own | none),
  blob5 has recording (yes | no), blob6 app version, blob7 platform, blob8 UTC day, blob9 detail; double1 is the count's weight (1 / the app's
  sample rate). Every sum is SUM(_sample_interval * double1): _sample_interval is Analytics Engine's own sampling, double1 the app's.

  Privacy: every query ends in HAVING SUM(_sample_interval) >= $MinEvents, so a group with fewer than 10 recorded events (a rare language,
  a rare book) is never shown. This floor counts EVENTS, not families: no install id exists by design, so one household can reach 10 events
  alone (many voice changes, many opens). It keeps tiny groups out of the report; it is not an anonymity guarantee. A group is dropped, not rounded.

  Token: a Cloudflare API token with "Account Analytics: Read". Keep it in the secrets store (the manage-secrets skill, D:\secrets), never in the repo;
  pass it as -Token or set CLOUDFLARE_API_TOKEN. The account id is the one that owns the Worker (-AccountId or CLOUDFLARE_ACCOUNT_ID).
  Nothing here writes anywhere; use -DryRun to print the SQL without calling the API.

.EXAMPLE
  ./tools/dino-usage-report.ps1 -DryRun
  ./tools/dino-usage-report.ps1 -Days 7 -Only "Top books per language"
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

$Dataset = 'dino_usage'
$Weighted = 'SUM(_sample_interval * double1)'
$Window = "timestamp > NOW() - INTERVAL '$Days' DAY"
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
  SUM(IF(blob1 = 'book_opened', _sample_interval * double1, 0)) AS opened,
  SUM(IF(blob1 = 'book_finished', _sample_interval * double1, 0)) AS finished
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
SELECT blob9 AS kind, $Weighted AS added
FROM $Dataset WHERE blob1 = 'own_book_added' AND $Window
GROUP BY kind $Enough ORDER BY added DESC
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
    'App opens by version, platform and screen language' = @"
SELECT blob6 AS version, blob7 AS platform, blob3 AS language, $Weighted AS opens
FROM $Dataset WHERE blob1 = 'app_opened' AND $Window
GROUP BY version, platform, language $Enough ORDER BY opens DESC
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
