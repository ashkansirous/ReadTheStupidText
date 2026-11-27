# Slice 447: the accepted-page save guard, dot-sourced by gemini-take-download.ps1 (and usable by any other saver).
# A book's art.md has a "## Accepted by the user" list; table rows there name the picture files the user accepted
# (`| 3 | high-res/3.jpg | 3196433 | ... |`, the file relative to the folder holding art.md). Walking up from a target path to the
# nearest art.md finds that list. Pictures are compared by path without extension, so 3.jpg and 3.png are the same page.
$script:AcceptedHeading = '## Accepted by the user'
$script:PreviousFolderName = 'previous'
$script:PictureExtensions = '.jpg', '.jpeg', '.png'

function Find-ArtRecord([string]$target) {
  $folder = [System.IO.Path]::GetDirectoryName($target)
  while ($folder) {
    $candidate = Join-Path $folder 'art.md'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    $folder = [System.IO.Path]::GetDirectoryName($folder)
  }
  return $null
}

function Get-PictureStem([string]$path) {
  $full = [System.IO.Path]::GetFullPath($path)
  $ext = [System.IO.Path]::GetExtension($full).ToLowerInvariant()
  if ($script:PictureExtensions -contains $ext) { $full = $full.Substring(0, $full.Length - $ext.Length) }
  return $full.ToLowerInvariant()
}

# The heading is matched by prefix, case-insensitively ("## Accepted by the user (2026-11-20)" counts).
$script:AcceptedHeadingPattern = '^##\s+Accepted by the user\b'
# A picture file name anywhere in a line: table cell or bullet, with or without backticks (names have no spaces).
$script:PictureNamePattern = '[\w./\\-]+\.(?:jpe?g|png)\b'
# A line that holds no picture by design: blank, a table separator, a table header (no digit), or an explicit "none".
$script:NoPicturesPattern = '^\s*$|^\s*\|?[\s:|-]+\|?\s*$|^\s*\|[^\d]*$|^\s*(?:[-*]\s*)?(?:none|nothing|n/a|\(empty\))(?:\s+yet)?\s*[.!]?\s*$'

# A listed name in the form compared against a target: lower case, forward slashes, no leading ./, no picture extension.
function ConvertTo-ListedStem([string]$name) {
  $stem = $name.ToLowerInvariant().Replace('\', '/')
  $stem = [regex]::Replace($stem, '\.(?:jpe?g|png)$', '')
  return ($stem -replace '^(\./)+', '')
}

function Get-PictureNames([string]$line) {
  return @([regex]::Matches($line, $script:PictureNamePattern, 'IgnoreCase') | ForEach-Object { $_.Value })
}

# The picture stems the art record lists as accepted (empty when there is no list). Fails closed: when the
# "## Accepted by the user" section exists and has content but no picture name can be read from it, this throws.
function Get-AcceptedStems([string]$artRecord) {
  $root = [System.IO.Path]::GetDirectoryName($artRecord)
  $inList = $false
  $stems = @()
  $unreadable = @()
  foreach ($line in Get-Content -LiteralPath $artRecord) {
    if ($line -match $script:AcceptedHeadingPattern) { $inList = $true; continue }
    if ($inList -and $line -match '^##\s') { break }
    if (-not $inList) { continue }
    $names = Get-PictureNames $line
    foreach ($name in $names) {
      $stems += ConvertTo-ListedStem $name
      if (-not (Test-Path -LiteralPath (Join-Path $root $name))) { Write-Warning "Accepted list in ${artRecord} names '$name', which is not under $root; it is matched by its trailing path." }
    }
    if (-not $names -and $line -notmatch $script:NoPicturesPattern) { $unreadable += $line.Trim() }
  }
  if ($inList -and -not $stems -and $unreadable) {
    throw "REFUSED: '$($script:AcceptedHeading)' in $artRecord has entries but no picture file name (like high-res/3.jpg) can be read from them ('$($unreadable[0])'). Fix the list, or pass -UserAskedRedo only when the user named this page."
  }
  return $stems
}

function Test-AcceptedPage([string]$target) {
  $artRecord = Find-ArtRecord $target
  if (-not $artRecord) { return $false }
  $targetStem = (Get-PictureStem $target).Replace('\', '/')
  foreach ($entry in (Get-AcceptedStems $artRecord)) {
    $listed = ($entry -split '/' | Select-Object -Last 2) -join '/'  # a repo-relative path still matches by its last folder + name
    if ($targetStem -eq $listed -or $targetStem.EndsWith("/$listed")) { return $true }
  }
  return $false
}

# Throws when $target is an accepted page and the caller did not pass -UserAskedRedo (nothing has been touched yet).
function Assert-NotAccepted([string]$target, [bool]$userAskedRedo) {
  if ($userAskedRedo -or -not (Test-AcceptedPage $target)) { return }
  throw "REFUSED: $target is listed under '$($script:AcceptedHeading)' in its book's art.md. Save it as a new candidate name, or pass -UserAskedRedo only when the user named this page."
}

# Where backups go (SKILL.md rule 3: <working folder>/previous/, never inside the repo's content/ or high-res/ folders).
# An explicit -BackupFolder wins. A target inside a book folder (one with an art.md above it) is repo content and has no
# default (a temp folder is swept nightly): Assert-BackupFolder refuses it. Any other target is a candidate in the working folder: <its folder>/previous.
function Get-BackupFolder([string]$target, [string]$backupFolder) {
  if ($backupFolder) { return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($backupFolder) }
  Assert-BackupFolder $target $backupFolder
  return Join-Path ([System.IO.Path]::GetDirectoryName($target)) $script:PreviousFolderName
}

# Throws (nothing touched yet) when $target is book content and no backup folder was given.
function Assert-BackupFolder([string]$target, [string]$backupFolder) {
  if ($backupFolder -or -not (Find-ArtRecord $target)) { return }
  throw "REFUSED: $target is book content; pass -BackupFolder <working folder>/previous so a replaced picture is kept outside the repo."
}

# A name in $folder that is free: <name><ext>, else <name>-<timestamp>[-<n>]<ext>, never an existing file.
function Get-FreeBackupName([string]$folder, [string]$name, [string]$ext) {
  $copy = Join-Path $folder "$name$ext"
  $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
  for ($n = 0; Test-Path -LiteralPath $copy; $n++) {
    $suffix = if ($n -eq 0) { $stamp } else { "$stamp-$n" }
    $copy = Join-Path $folder "$name-$suffix$ext"
  }
  return $copy
}

# Copies every existing picture with $target's name (any picture extension) to the backup folder before it is replaced;
# returns the copies' paths. A name already there gets a timestamp (and a counter) so no earlier copy is lost.
function Backup-ReplacedFiles([string]$target, [string]$backupFolder = '') {
  $folder = [System.IO.Path]::GetDirectoryName($target)
  $name = [System.IO.Path]::GetFileNameWithoutExtension($target)
  $previous = Get-BackupFolder $target $backupFolder
  $copies = @()
  foreach ($ext in $script:PictureExtensions) {
    $existing = Join-Path $folder "$name$ext"
    if (-not (Test-Path -LiteralPath $existing -PathType Leaf)) { continue }
    New-Item -ItemType Directory -Force -Path $previous | Out-Null
    $copy = Get-FreeBackupName $previous $name $ext
    Copy-Item -LiteralPath $existing -Destination $copy
    $copies += $copy
  }
  return $copies
}
