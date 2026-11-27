#!/usr/bin/env bash
# Slice 393 item 23: wait until Maestro's iOS driver answers on a booted simulator, retrying a driver that does not come up, so the first real flow
# does not pay for the cold driver start. Three slices had recorded the symptom one by one: 244 (StoryTile 60 s timeout, XCUITestServerError
# Unreachable), 387 (IOSDriverTimeoutException), 291 (the Dino iPad flow). `maestro hierarchy` starts the driver (installs and launches the XCTest runner on
# the simulator) and reads the screen, so a successful call means the driver is up. macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-driver-ready.sh <simulator udid>
# Env: DRIVER_READY_ATTEMPTS (default 3), DRIVER_READY_TIME_LIMIT_SECONDS (per attempt, default 360, keep it above MAESTRO_DRIVER_STARTUP_TIMEOUT / 1000 so the script's watchdog never kills a driver that Maestro would still wait for), MAESTRO_DRIVER_STARTUP_TIMEOUT (ms, default 300000; Maestro's own
# default for iOS is 120 s, docs: maestro-cli/environment-variables.md), DRIVER_READY_PAUSE_SECONDS (between attempts, default 10).
# Exit 0 when the driver answered (the line `driver ready on <udid> after attempt <n>` is printed), 1 when no attempt did. Never needed to be fatal for a caller:
# ios-sim-store-shots.sh goes on after a warning, since the flow's own retry is the next line of defence.
set -uo pipefail

UDID="${1:?simulator udid}"
ATTEMPTS="${DRIVER_READY_ATTEMPTS:-3}"
LIMIT="${DRIVER_READY_TIME_LIMIT_SECONDS:-360}"
PAUSE="${DRIVER_READY_PAUSE_SECONDS:-10}"
POLL_SECONDS=2
export MAESTRO_DRIVER_STARTUP_TIMEOUT="${MAESTRO_DRIVER_STARTUP_TIMEOUT:-300000}"

# Kills a process and its descendants, only ones this script started (pgrep ships with macOS). No other process is ever killed by name.
kill_tree() {
  local pid="$1" child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    kill_tree "$child"
  done
  kill -9 "$pid" 2>/dev/null || true
}

# One `maestro hierarchy` under the time limit. Returns its status, 124 when the limit killed it.
try_driver() {
  local pid started rc=0
  maestro --device "$UDID" hierarchy > /dev/null 2>&1 &
  pid=$!
  started=$SECONDS
  while kill -0 "$pid" 2>/dev/null; do
    if [ $((SECONDS - started)) -ge "$LIMIT" ]; then
      echo "::warning::driver check killed after $LIMIT s on $UDID"
      kill_tree "$pid"
      wait "$pid" 2>/dev/null || true
      return 124
    fi
    sleep "$POLL_SECONDS"
  done
  wait "$pid" || rc=$?
  return "$rc"
}

attempt=1
while [ "$attempt" -le "$ATTEMPTS" ]; do
  echo "driver check: attempt $attempt of $ATTEMPTS on $UDID (limit $LIMIT s, driver start timeout $MAESTRO_DRIVER_STARTUP_TIMEOUT ms)"
  rc=0
  try_driver || rc=$?
  if [ "$rc" = "0" ]; then
    echo "driver ready on $UDID after attempt $attempt"
    exit 0
  fi
  echo "::warning::driver not ready on $UDID after attempt $attempt (status $rc)"
  attempt=$((attempt + 1))
  [ "$attempt" -le "$ATTEMPTS" ] && sleep "$PAUSE"
done
echo "::warning::driver never answered on $UDID in $ATTEMPTS attempts"
exit 1
