#!/usr/bin/env bash
# Slice 433: writes the two simulator preferences Dino's store shots and preview clips need into a SHUT-DOWN simulator's data folder, so the boot that follows reads them.
#   1. The approval of the app's URL scheme (run 45491086879, finding 1): iOS never asks "Open in <app>?" for a dino-test:// link, which Maestro cannot tap in landscape
#      (issue 2065). Source: Maestro issue 940, the PlistBuddy line of a commenter, which names <device data>/Library/Preferences/com.apple.launchservices.schemeapproval.plist
#      and the key "<bridge>--><scheme>" with the app's bundle id as its string value.
#   2. Reduce Motion (iOS run 45512162060): the wait overlay's HoppingImage loops forever (repeat: () => true) and XCTest waits for the app to be idle before every
#      hierarchy read; the app stands that loop still when Reduce Motion is on (ReduceMotion.IsOn). Same domain and key as Appium's setReduceMotion
#      (com.apple.Accessibility ReduceMotionEnabled = 1, appium-ios-simulator lib/extensions/settings.ts). Karaoke highlight and page turns do not depend on it.
# Used two ways: by ios-sim-boot.sh (SIM_BEFORE_BOOT_SCRIPT) between `simctl create` and the first boot, so no second boot is needed (Slice 433: that restart took 3 to 3.5 min
# per run), and by ios-sim-store-shots.sh on a simulator it shut down itself (the fallback when the pre-boot values did not survive the first boot).
# macOS bash 3.2.
#
# Usage: ios-sim-store-prefs.sh <udid> [bundle id]   (bundle id default: $STORE_BUNDLE_ID)
# Prints both entries; exits 1 when either does not read back from its file.
set -uo pipefail

UDID="${1:?simulator udid}"
BUNDLE_ID="${2:-${STORE_BUNDLE_ID:-}}"
[ -n "$BUNDLE_ID" ] || { echo "::error::ios-sim-store-prefs.sh: no bundle id (argument 2 or STORE_BUNDLE_ID)"; exit 1; }
PREFS="$HOME/Library/Developer/CoreSimulator/Devices/$UDID/data/Library/Preferences"
# The URL scheme the flows open with openLink (every openLink in .maestro/dino/store-shot-ios-*.yaml is dino-test://) and the bridge that holds the approval.
SCHEME_KEY="com.apple.CoreSimulator.CoreSimulatorBridge-->dino-test"
SCHEME_PLIST="$PREFS/com.apple.launchservices.schemeapproval.plist"
MOTION_PLIST="$PREFS/com.apple.Accessibility.plist"

# Sets one key of a plist file (created when missing) and prints what the file then holds for it.
set_key() {
  local plist="$1" key="$2" type="$3" value="$4"
  /usr/libexec/PlistBuddy -c "Delete :$key" "$plist" >/dev/null 2>&1 </dev/null || true
  /usr/libexec/PlistBuddy -c "Add :$key $type $value" "$plist" >/dev/null 2>&1 </dev/null || true
  /usr/libexec/PlistBuddy -c "Print :$key" "$plist" 2>/dev/null </dev/null || true
}

mkdir -p "$PREFS"
rc=0
scheme="$(set_key "$SCHEME_PLIST" "$SCHEME_KEY" string "$BUNDLE_ID")"
echo "scheme approval in $SCHEME_PLIST: $SCHEME_KEY = $scheme"
[ "$scheme" = "$BUNDLE_ID" ] || { echo "::warning::the URL scheme approval did not take: '$scheme', expected '$BUNDLE_ID'"; rc=1; }
motion="$(set_key "$MOTION_PLIST" ReduceMotionEnabled integer 1)"
echo "Reduce Motion in $MOTION_PLIST: ReduceMotionEnabled = $motion"
[ "$motion" = "1" ] || { echo "::warning::Reduce Motion did not take on $UDID: '$motion'"; rc=1; }
exit "$rc"
