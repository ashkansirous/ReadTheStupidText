# CI does this after every store-book merge and daily (Slice 158: dino-store.yml `publish-store`, .github/scripts/publish-store-books.mjs); this script is the manual fallback.
# Uploads one built store story (Slice 102) to the downloads bucket: `pwsh tools/dino/upload-store-story.ps1 <story id> [-Root <store folder>]` (from any folder; Root defaults to the repo's artifacts/store).
# Build the folder first with `dotnet run --project tools/dino/DinoReader.BookBuilder -- build` (from content/dino/, Slice 157). The cover and every package go up first and meta.json
# LAST, so a half-finished upload is never listed (worker/stories.ts offers a folder only when meta.json and a package are both there).
# Audio packs (Slice 246 B2) are uploaded with the cover and the package, so meta.json (whose `audio` field lists them) is still last. -MetaOnly sends meta.json alone.
# -MetaOnly re-uploads just meta.json (Slice 112: new age/author fields on a story whose package is unchanged; the folder must still hold the package).
# wrangler must already be logged in; --remote writes to the real bucket, not the local simulator.
param(
  [Parameter(Mandatory = $true)][string]$Id,
  [string]$Root = (Join-Path $PSScriptRoot '..' '..' 'artifacts' 'store'),
  [string]$Bucket = 'readthestupidtext-downloads',
  [switch]$MetaOnly,
  # Slice 277: run only the audio-completeness check and upload nothing (the dry run).
  [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
if ($Id -notmatch '^[0-9a-f]{32}$') { throw "Not a story id: $Id" }

# Slice 277: a book reaches the store only with complete audio (a pack in every language, a clip for every sentence). The check names the book, language and first missing sentences; a gap uploads nothing.
# Needs the manifests from Git LFS: git lfs pull --include "content/dino/*/audio/*/manifest.json" --exclude ""
$stem = Get-ChildItem -Path (Join-Path $PSScriptRoot '..' '..' 'content' 'dino') -Directory | Where-Object { (Test-Path (Join-Path $_.FullName 'book.json')) -and ((Get-Content -Raw (Join-Path $_.FullName 'book.json') | ConvertFrom-Json).id -eq $Id) } | Select-Object -First 1 -ExpandProperty Name
if (-not $stem) { throw "No content/dino book has the id $Id; refusing to upload a story whose audio cannot be checked." }
$bookDir = Join-Path $PSScriptRoot '..' '..' 'content' 'dino' $stem
# A manifest that is still a Git LFS pointer would make the check throw with no hint; say what to run.
$pointers = Get-ChildItem -Path (Join-Path $bookDir 'audio') -Recurse -Filter 'manifest.json' -ErrorAction SilentlyContinue | Where-Object { (Get-Content -TotalCount 1 $_.FullName) -like 'version https://git-lfs*' }
if ($pointers) { throw "The audio manifests of $stem are Git LFS pointers; run: git lfs pull --include `"content/dino/$stem/audio/*/manifest.json`" --exclude `"`" and try again." }
& dotnet run --project (Join-Path $PSScriptRoot 'DinoReader.BookBuilder') --configuration Release -- check-audio $stem
if ($LASTEXITCODE -ne 0) { throw "The audio of $stem is incomplete (see the lines above); nothing was uploaded." }
if ($CheckOnly) { Write-Host "Audio of $stem is complete; -CheckOnly, nothing uploaded."; return }
$folder = Join-Path $Root $Id
if (-not (Test-Path (Join-Path $folder 'meta.json'))) { throw "No meta.json in $folder; build the store folder first." }
$packages = Get-ChildItem -Path $folder -Filter 'story-v*.dino'
if (-not $packages) { throw "No story-v<n>.dino in $folder." }
$meta = Get-Content -Raw (Join-Path $folder 'meta.json') | ConvertFrom-Json
$version = $meta.version
# The check above read the working tree; the folder being uploaded must be the build of that tree: every audio pack the book records (audio-cast.json) must be listed in meta.json at the same version.
$castPath = Join-Path $bookDir 'audio-cast.json'
$recorded = if (Test-Path $castPath) { (Get-Content -Raw $castPath | ConvertFrom-Json).audioPacks } else { $null }
$listed = if ($meta.PSObject.Properties['audio']) { $meta.audio } else { $null }
# Slice 277: a language counts as complete only with its audioPacks record too; a language with clips and a manifest but no record would ship with no audio.
$recordedNames = if ($recorded) { @($recorded.PSObject.Properties.Name) } else { @() }
$withoutRecord = Get-ChildItem -Path (Join-Path $bookDir 'audio') -Directory -ErrorAction SilentlyContinue | Where-Object { (Test-Path (Join-Path $_.FullName 'manifest.json')) -and ($recordedNames -notcontains $_.Name) } | ForEach-Object { $_.Name }
if ($withoutRecord) { throw "$stem has audio for $($withoutRecord -join ', ') but no audioPacks record in audio-cast.json for it; run build-audio ($stem --lang <language>) and try again." }
if ($recorded) {
  foreach ($language in $recorded.PSObject.Properties) {
    $built = if ($listed -and $listed.PSObject.Properties[$language.Name]) { $listed.($language.Name).version } else { $null }
    if ($built -ne $language.Value.version) { throw "$folder is stale: audio $($language.Name) is v$built in meta.json but v$($language.Value.version) in $stem's audio-cast.json; rebuild the store folder (BookBuilder build, build-audio)." }
    # Same pack version is not enough (clips can change without a new version in a folder built earlier): the size meta.json lists and the zip's own size must equal the size the book records.
    $listedBytes = $listed.($language.Name).bytes
    $zip = Join-Path $folder "audio-$($language.Name)-v$($language.Value.version).zip"
    if ($listedBytes -ne $language.Value.bytes -or -not (Test-Path $zip) -or (Get-Item $zip).Length -ne $language.Value.bytes) { throw "$folder is stale: audio $($language.Name) differs in size from the pack $stem's audio-cast.json records ($($language.Value.bytes) bytes); rebuild the store folder (BookBuilder build, build-audio)." }
  }
}
if (-not ($packages | Where-Object { $_.Name -eq "story-v$version.dino" })) { throw "meta.json says version $version but $folder has no story-v$version.dino; the story would drop out of the list." }

function Send-Object([string]$Name, [string]$ContentType) {
  $file = Join-Path $folder $Name
  & npx wrangler r2 object put "$Bucket/dinoreader/stories/$Id/$Name" --file $file --content-type $ContentType --remote
  if ($LASTEXITCODE -ne 0) { throw "Upload of $Name failed." }
}

if (-not $MetaOnly) {
  if (Test-Path (Join-Path $folder 'cover.jpg')) { Send-Object 'cover.jpg' 'image/jpeg' }
  foreach ($package in $packages) { Send-Object $package.Name 'application/octet-stream' }
  # Slice 246 B2: the audio packs (audio-<lang>-v<n>.zip, built by `BookBuilder build-audio`) go up before meta.json too; a book without any behaves as before.
  # Only the version meta.json lists for each language goes up, never the superseded zips build-audio leaves in the folder. The same version means the same content hash (and the zip is
  # deterministic), so re-uploading a version already in the bucket replaces it with identical bytes; a new content hash is always a new version, never an overwrite.
  $audio = if ($meta.PSObject.Properties['audio']) { $meta.audio } else { $null }
  if ($audio) {
    foreach ($language in $audio.PSObject.Properties) {
      $packName = "audio-$($language.Name)-v$($language.Value.version).zip"
      if (-not (Test-Path (Join-Path $folder $packName))) { throw "meta.json lists $packName but $folder does not have it; run build-audio first." }
      Send-Object $packName 'application/zip'
    }
  }
}
Send-Object 'meta.json' 'application/json'
Write-Host "Uploaded $Id"
