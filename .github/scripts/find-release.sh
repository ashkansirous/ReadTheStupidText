#!/usr/bin/env bash
# Usage: find-release.sh <asset-regex>
# Finds the newest PUBLISHED release with an asset matching <asset-regex>.
# "{version}" in the regex is replaced by that release's version (tag minus
# the leading "v", dots matched literally). Prints the tag on line 1, then
# each matching asset name; prints nothing if no release matches.
# Needs GH_TOKEN and GITHUB_REPOSITORY (both set in Actions).
set -euo pipefail

pattern="$1"
tags=$(gh release list --repo "$GITHUB_REPOSITORY" --limit 50 --exclude-drafts \
  --json tagName --jq '.[].tagName')

for tag in $tags; do
  version="${tag#v}"
  literal_version="${version//./[.]}"
  regex="${pattern//\{version\}/$literal_version}"
  # Captured first, not piped into grep -q: an early grep exit would SIGPIPE
  # gh and, under pipefail, read as "no match".
  names=$(gh release view "$tag" --repo "$GITHUB_REPOSITORY" --json assets --jq '.assets[].name')
  if matches=$(grep -E "$regex" <<< "$names"); then
    echo "$tag"
    echo "$matches"
    exit 0
  fi
done
