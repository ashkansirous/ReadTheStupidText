#!/usr/bin/env bash
# Fails when a built iOS .app lacks what App Store Connect validates on upload: a compiled
# icon catalog (Assets.car), CFBundleIconName and a launch screen (UILaunchScreen or
# UILaunchStoryboardName). Dino Reader v1.33.0 compiled and signed cleanly yet was rejected
# for all three (90022, 90023, 90475, 90713), so the build checks its own output.
# Usage: check-ios-bundle.sh <path/to/App.app>. macOS only (PlistBuddy); bash 3.2 safe.
set -euo pipefail

app="${1:-}"
plist="$app/Info.plist"
buddy=/usr/libexec/PlistBuddy
failed=0

has_key() {
  "$buddy" -c "Print :$1" "$plist" >/dev/null 2>&1
}

fail() {
  echo "::error::$1"
  failed=1
}

[ -n "$app" ] && [ -d "$app" ] || { echo "::error::No .app bundle found to check (got '$app')."; exit 1; }
[ -f "$plist" ] || { echo "::error::No Info.plist in $app"; exit 1; }
[ -f "$app/Assets.car" ] || fail "No Assets.car in $app: the icon catalog was not compiled by actool."
[ -d "$app/Assets.xcassets" ] && fail "A raw Assets.xcassets folder was copied into $app instead of being compiled."
has_key CFBundleIconName || fail "Info.plist has no CFBundleIconName (App Store Connect error 90713)."
if ! has_key UILaunchScreen && ! has_key UILaunchStoryboardName; then
  fail "Info.plist has no UILaunchScreen or UILaunchStoryboardName (App Store Connect error 90475)."
fi

if [ "$failed" -ne 0 ]; then
  exit 1
fi
echo "iOS bundle check passed: Assets.car, CFBundleIconName=$("$buddy" -c 'Print :CFBundleIconName' "$plist"), launch screen."
