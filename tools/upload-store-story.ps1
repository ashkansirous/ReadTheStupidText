# CI does this on every merge (Slice 158: release.yml `publish-store`, .github/scripts/publish-store-books.mjs); this script is the manual fallback.
# Uploads one built store story (Slice 102) to the downloads bucket: `pwsh tools/upload-store-story.ps1 <story id> [-Root <store folder>]` (from any folder; Root defaults to the repo's artifacts/store).
# Build the folder first with `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests`. The cover and every package go up first and meta.json
# LAST, so a half-finished upload is never listed (worker/stories.ts offers a folder only when meta.json and a package are both there).
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
$version = (Get-Content -Raw (Join-Path $folder 'meta.json') | ConvertFrom-Json).version
if (-not ($packages | Where-Object { $_.Name -eq "story-v$version.dino" })) { throw "meta.json says version $version but $folder has no story-v$version.dino; the story would drop out of the list." }

function Send-Object([string]$Name, [string]$ContentType) {
  $file = Join-Path $folder $Name
  & npx wrangler r2 object put "$Bucket/dinoreader/stories/$Id/$Name" --file $file --content-type $ContentType --remote
  if ($LASTEXITCODE -ne 0) { throw "Upload of $Name failed." }
}

if (-not $MetaOnly) {
  if (Test-Path (Join-Path $folder 'cover.jpg')) { Send-Object 'cover.jpg' 'image/jpeg' }
  foreach ($package in $packages) { Send-Object $package.Name 'application/octet-stream' }
}
Send-Object 'meta.json' 'application/json'
Write-Host "Uploaded $Id"
