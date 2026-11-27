#!/usr/bin/env bash
# Slice 393 item 23: wait until Maestro's iOS driver answers on a booted simulator, retrying a driver that does not come up, so the first real flow
# does not pay for the cold driver start. Three slices had recorded the symptom one by one: 244 (StoryTile 60 s timeout, XCUITestServerError
# Unreachable), 387 (IOSDriverTimeoutException), 291 (the Dino iPad flow). `maestro hierarchy` starts the driver (installs and launches the XCTest runner on
# the simulator) and reads the screen, so a successful call means the driver is up. macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-driver-ready.sh <simulator udid>
# Env: DRIVER_READY_ATTEMPTS (default 3), DRIVER_READY_TIME_LIMIT_SECONDS (per attempt, default 360, keep it above MAESTRO_DRIVER_STARTUP_TIMEOUT / 1000 so the script's watchdog never kills a driver that Maestro would still wait for), MAESTRO_DRIVER_STARTUP_TIMEOUT (ms, default 300000; Maestro's own
# default for iOS is 120 s, docs: maestro-cli/environment-variables.md), DRIVER_READY_PAUSE_SECONDS (between attempts, default 10).
# Slice 442: DRIVER_READY_BUDGET_SECONDS (default 0 = no cap) bounds the whole script: no attempt starts with under 60 s left and the last one is limited to what is left.
# Exit 0 when the driver answered (the line `driver ready on <udid> after attempt <n>` is printed), 1 when no attempt did. Never needed to be fatal for a caller:
# ios-sim-store-shots.sh goes on after a warning, since the flow's own retry is the next line of defence.
set -uo pipefail

UDID="${1:?simulator udid}"
ATTEMPTS="${DRIVER_READY_ATTEMPTS:-3}"
LIMIT="${DRIVER_READY_TIME_LIMIT_SECONDS:-360}"
PAUSE="${DRIVER_READY_PAUSE_SECONDS:-10}"
# Slice 442: the whole script's time (0 = no cap). An attempt is not started when less than MIN_ATTEMPT_SECONDS is left, and one that starts is limited to what is left,
# so the caller's step timeout or script budget never kills a half-done attempt.
BUDGET="${DRIVER_READY_BUDGET_SECONDS:-0}"
MIN_ATTEMPT_SECONDS=60
POLL_SECONDS=2
SCRIPT_STARTED=$SECONDS
export MAESTRO_DRIVER_STARTUP_TIMEOUT="${MAESTRO_DRIVER_STARTUP_TIMEOUT:-300000}"

# Kills a process and its descendants, only ones this script started (pgrep ships with macOS). No other process is ever killed by name.
kill_tree() {
  local pid="$1" child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    kill_tree "$child"
  done
  kill -9 "$pid" 2>/dev/null || true
}

# Slice 442: what is left of BUDGET in seconds (a large number when there is no cap).
time_left() {
  if [ "$BUDGET" -le 0 ]; then
    echo 999999
    return
  fi
  echo $((BUDGET - (SECONDS - SCRIPT_STARTED)))
}

# Slice 442: Maestro's iOS driver for this simulator is an xcodebuild/XCTest runner that outlives a killed or failed `maestro hierarchy`; it is stopped before the next attempt so
# the retry starts a fresh one (the same patterns as ios-sim-store-shots.sh kill_driver; only processes naming this simulator's id).
kill_driver() {
  pkill -9 -f "xcodebuild.*$UDID" 2>/dev/null || true
  pkill -9 -f "maestro.*$UDID" 2>/dev/null || true
}

# One `maestro hierarchy` under the time limit. Returns its status, 124 when the limit killed it.
try_driver() {
  local limit="$1" pid started rc=0
  maestro --device "$UDID" hierarchy > /dev/null 2>&1 &
  pid=$!
  started=$SECONDS
  while kill -0 "$pid" 2>/dev/null; do
    if [ $((SECONDS - started)) -ge "$limit" ]; then
      echo "::warning::driver check killed after $limit s on $UDID"
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
  left="$(time_left)"
  if [ "$left" -lt "$MIN_ATTEMPT_SECONDS" ]; then
    echo "::warning::driver check on $UDID stopped before attempt $attempt: $left s left of the $BUDGET s budget"
    break
  fi
  attempt_limit="$LIMIT"
  [ "$left" -lt "$attempt_limit" ] && attempt_limit="$left"
  echo "driver check: attempt $attempt of $ATTEMPTS on $UDID (limit $attempt_limit s, driver start timeout $MAESTRO_DRIVER_STARTUP_TIMEOUT ms)"
  rc=0
  try_driver "$attempt_limit" || rc=$?
  if [ "$rc" = "0" ]; then
    echo "driver ready on $UDID after attempt $attempt"
    exit 0
  fi
  echo "::warning::driver not ready on $UDID after attempt $attempt (status $rc)"
  kill_driver   # a failed or killed attempt may leave its XCTest runner behind: the next attempt starts a fresh one
  attempt=$((attempt + 1))
  [ "$attempt" -le "$ATTEMPTS" ] && sleep "$PAUSE"
done
echo "::warning::driver never answered on $UDID in $ATTEMPTS attempts"
exit 1
