#!/usr/bin/env bash
# Slice 244: create and boot one iPhone simulator on a macOS runner (also used by Slice 291's screenshot run).
# macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-boot.sh
#   SIM_DEVICE_NAME  name of the simulator to create (default rtst-sim)
#   SIM_DEVICE_TYPE  a CoreSimulator device-type id; unset = the smallest iPhone this image's runtime accepts
#   SIM_DEVICE_PATTERNS  Slice 291: ';'-separated case-insensitive regexes of device-type NAMES, tried in order (an iPhone Pro Max or an iPad Pro 13-inch
#                    for a store screenshot); within one pattern the newest type the runtime accepts wins. Used when SIM_DEVICE_TYPE is unset.
#   SIM_SETTLE_SECONDS  Slice 393: seconds to wait after bootstatus before returning (default 20), so the first Maestro driver start finds a settled simulator.
# Prints `udid=<UDID>` as its last line, and appends SIM_UDID=<UDID> to $GITHUB_ENV when that is set.
set -euo pipefail

NAME="${SIM_DEVICE_NAME:-rtst-sim}"
ERR_FILE="$(mktemp)"

# The newest available iOS runtime's identifier (the line ends `... - com.apple.CoreSimulator.SimRuntime.iOS-26-5`).
# Slice 404: some runner images also list a runtime built for one device (`SimRuntime.iOS-27-2-iPhone19-4`) after the
# general one; it refuses every other device type, so only ids that end in the version numbers count.
newest_ios_runtime() {
  local ids general
  ids="$(xcrun simctl list runtimes available | grep -E '^iOS ' \
    | sed -E 's/.* - (com\.apple\.CoreSimulator\.SimRuntime\.[^ ]+)$/\1/' || true)"
  general="$(echo "$ids" | grep -E '\.SimRuntime\.iOS-[0-9]+(-[0-9]+)*$' | tail -n 1 || true)"
  if [ -z "$general" ]; then
    echo "::error::no general iOS runtime on this image; runtimes found: ${ids:-none}" >&2
    return 1
  fi
  echo "$general"
}

# Plain iPhones (no Pro, Plus, Max, Air) in `simctl list devicetypes` order, which is oldest first: the oldest
# have the smallest screen and the oldest identity. A simulator shares the Mac host's RAM, so this is NOT a memory limit.
candidate_device_types() {
  xcrun simctl list devicetypes | grep -E '^iPhone ' | grep -vE 'Pro|Plus|Max|Air' | sed -E 's/.*\((com\.apple\.CoreSimulator\.SimDeviceType\.[^)]+)\)$/\1/'
}

# Slice 291: the device types whose NAME matches one of SIM_DEVICE_PATTERNS, pattern by pattern, newest first (`simctl list devicetypes` is oldest first).
pattern_device_types() {
  local pattern
  while IFS= read -r pattern; do
    [ -n "$pattern" ] || continue
    xcrun simctl list devicetypes | grep -iE "^($pattern) \(" | sed '1!G;h;$!d' | sed -E 's/.*\((com\.apple\.CoreSimulator\.SimDeviceType\.[^)]+)\)$/\1/' || true
  done < <(echo "$SIM_DEVICE_PATTERNS" | tr ';' '\n')
}

# Creates the simulator from the first device type the runtime accepts; prints the UDID.
create_simulator() {
  local runtime="$1" type udid source=candidate_device_types
  if [ -n "${SIM_DEVICE_TYPE:-}" ]; then
    xcrun simctl create "$NAME" "$SIM_DEVICE_TYPE" "$runtime"
    return
  fi
  if [ -n "${SIM_DEVICE_PATTERNS:-}" ]; then
    source=pattern_device_types
  fi
  while IFS= read -r type; do
    if udid=$(xcrun simctl create "$NAME" "$type" "$runtime" 2>"$ERR_FILE"); then
      echo "device type: $type" >&2
      echo "$udid"
      return
    fi
    echo "simctl create refused $type: $(cat "$ERR_FILE")" >&2
  done < <($source)
  echo "no device type accepted by $runtime ($source)" >&2
  return 1
}

RUNTIME="$(newest_ios_runtime)"
echo "runtime: $RUNTIME" >&2
echo "device types the image has:" >&2
xcrun simctl list devicetypes | grep -E '^(iPhone|iPad) ' >&2 || true

UDID="$(create_simulator "$RUNTIME")"
xcrun simctl boot "$UDID"
# The workflow step's own timeout-minutes bounds a hung boot.
xcrun simctl bootstatus "$UDID" -b >&2
# Slice 393 item 23: `bootstatus -b` returns while SpringBoard and the first-boot services still settle, and the first Maestro driver start on a fresh simulator then
# missed its window (244: StoryTile 60 s timeout, XCUITestServerError Unreachable; 387: IOSDriverTimeoutException; 291: the Dino iPad flow). A longer first wait, not a probe.
SETTLE_SECONDS="${SIM_SETTLE_SECONDS:-20}"
echo "settling $SETTLE_SECONDS s after boot" >&2
sleep "$SETTLE_SECONDS"

if [ -n "${GITHUB_ENV:-}" ]; then
  echo "SIM_UDID=$UDID" >> "$GITHUB_ENV"
fi
echo "udid=$UDID"
