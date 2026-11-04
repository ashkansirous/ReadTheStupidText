# CI does this after every store-book merge and daily (Slice 158: dino-store.yml `publish-store`, .github/scripts/publish-store-books.mjs); this script is the manual fallback.
# Uploads one built store story (Slice 102) to the downloads bucket: `pwsh tools/upload-store-story.ps1 <story id> [-Root <store folder>]` (from any folder; Root defaults to the repo's artifacts/store).
# Build the folder first with `dotnet run --project tools/DinoReader.BookBuilder -- build` (from content/dino/, Slice 157). The cover and every package go up first and meta.json
# LAST, so a half-finished upload is never listed (worker/stories.ts offers a folder only when meta.json and a package are both there).
# Audio packs (Slice 246 B2) are uploaded with the cover and the package, so meta.json (whose `audio` field lists them) is still last. -MetaOnly sends meta.json alone.
# -MetaOnly re-uploads just meta.json (Slice 112: new age/author fields on a story whose package is unchanged; the folder must still hold the package).
# wrangler must already be logged in; --remote writes to the real bucket, not the local simulator.
param(
  [Parameter(Mandatory = $true)][string]$Id,
  [string]$Root = (Join-Path $PSScriptRoot '..' 'artifacts' 'store'),
  [string]$Bucket = 'readthestupidtext-downloads',
  [switch]$MetaOnly
)
$ErrorActionPreference = 'Stop'
if ($Id -notmatch '^[0-9a-f]{32}$') { throw "Not a story id: $Id" }
$folder = Join-Path $Root $Id
if (-not (Test-Path (Join-Path $folder 'meta.json'))) { throw "No meta.json in $folder; build the store folder first." }
$packages = Get-ChildItem -Path $folder -Filter 'story-v*.dino'
if (-not $packages) { throw "No story-v<n>.dino in $folder." }
$meta = Get-Content -Raw (Join-Path $folder 'meta.json') | ConvertFrom-Json
$version = $meta.version
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
