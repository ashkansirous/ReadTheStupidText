#!/usr/bin/env bash
# Copies the one signed .<ext> (aab|apk) out of $PUBLISH_DIR into dist/ as
# ReadTheStupidText-$VERSION.<ext>. The publish output is named after the
# package id with no version; carried-forward release assets must still say
# which build they are. Staged right after each publish so the next publish
# can't overwrite it.
set -euo pipefail

ext="$1"
mapfile -t files < <(find "$PUBLISH_DIR" -maxdepth 1 -name "*-Signed.$ext")
if [ "${#files[@]}" -ne 1 ]; then
  echo "::error::Expected exactly 1 signed .$ext in $PUBLISH_DIR, found ${#files[@]}"
  exit 1
fi
mkdir -p dist
cp "${files[0]}" "dist/ReadTheStupidText-${VERSION}.$ext"
echo "Staged dist/ReadTheStupidText-${VERSION}.$ext"
