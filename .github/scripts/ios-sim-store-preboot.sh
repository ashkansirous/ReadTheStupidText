#!/usr/bin/env bash
# Slice 433 (run time): creates and boots Dino's store-shot simulator for one device label EARLY, in the background while the workflow builds the app, and writes the
# URL scheme approval and Reduce Motion into it BEFORE its first boot (ios-sim-store-prefs.sh through ios-sim-boot.sh's SIM_BEFORE_BOOT_SCRIPT).
# Why: iPad run 45597845229 spent 11 min creating and first-booting the simulator inside the shots step and 3.5 min on a second boot for Reduce Motion
# (run 45607845756: 4.7 min and 3 min). The build, Maestro, ffmpeg and package steps before the shots step take about 5 min, so the boot now overlaps them,
# and the second boot is skipped when the pre-boot values read back on the booted simulator (ios-sim-store-shots.sh, use_preboot / approve_url_scheme).
# macOS bash 3.2.
#
# Usage: ios-sim-store-preboot.sh <app> <device label> <csproj> <state file>
#   Writes <state file> when it ends: `udid=<udid>` and `bundle=<bundle id written into the approval>`, or `failed=<why>`. ios-sim-store-shots.sh waits for that file
#   (PREBOOT_STATE) and boots a simulator itself when it says failed or never comes. Its own output goes wherever the caller sends it (the workflow: PREBOOT_LOG).
set -uo pipefail

APP="${1:?app}"
LABEL="${2:?device label}"
PROJECT="${3:?csproj}"
STATE="${4:?state file}"
HERE="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=ios-sim-store-devices.sh
. "$HERE/ios-sim-store-devices.sh"

# Records the outcome for ios-sim-store-shots.sh: written to a temporary name and moved, so a reader never sees half a file.
finish() {
  printf '%s\n' "$@" > "$STATE.tmp" && mv "$STATE.tmp" "$STATE"
}

patterns="$(device_patterns "$LABEL")"
[ -n "$patterns" ] || { finish "failed=unknown device label $LABEL"; exit 1; }
# The app's bundle id, from the project the workflow builds (<ApplicationId>); ios-sim-store-shots.sh compares it with the installed app's and redoes the approval on a mismatch.
bundle="$(sed -n 's:.*<ApplicationId>\(.*\)</ApplicationId>.*:\1:p' "$PROJECT" | head -n 1)"
[ -n "$bundle" ] || { finish "failed=no ApplicationId in $PROJECT"; exit 1; }
echo "pre-boot of $LABEL ($patterns) for $bundle started"
started=$SECONDS
udid="$(STORE_BUNDLE_ID="$bundle" SIM_BEFORE_BOOT_SCRIPT="$HERE/ios-sim-store-prefs.sh" SIM_DEVICE_NAME="shots-$LABEL" SIM_DEVICE_PATTERNS="$patterns" GITHUB_ENV='' \
  bash "$HERE/ios-sim-boot.sh" | tail -n 1 | sed -n 's/^udid=//p')"
if [ -z "$udid" ]; then
  finish "failed=ios-sim-boot.sh booted no simulator ($patterns)"
  exit 1
fi
echo "pre-boot of $LABEL done in $((SECONDS - started)) s: $udid"
finish "udid=$udid" "bundle=$bundle"
