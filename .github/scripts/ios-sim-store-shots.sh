#!/usr/bin/env bash
# Slice 291 item 5 (iOS part): takes one app's App Store screenshots on three simulators (iPhone 6.9", iPhone 6.5", iPad 13") with the
# store-shot flow, and copies each PNG, renamed for its device, into <out-dir>/<device>/. Reuses ios-sim-boot.sh (Slice 244).
# macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-store-shots.sh <app> <app-dir> <flow> <out-dir>
#   app      dino | rtst: the shot names in the flow are store-<app>-ios-<n>-<screen> and become store-<app>-<device>-<n>-<screen>.png
#   app-dir  the folder holding the built .app (a Debug iossimulator-arm64 build)
#   flow     the Maestro flow file; its appId line is swapped for the app's bundle id, its `# expect-log:` lines are dropped
#   out-dir  the folder that gets <device>/<png> (created)
# Needs Maestro on the PATH (ios-sim-install-maestro.sh). Exit 0 when every device produced every shot at an App Store size, 1 otherwise;
# a device that fails is recorded and the next one still runs. No artifacts: the workflow hands the PNGs over (store-screenshots.yml).
set -uo pipefail

APP="${1:?app: dino or rtst}"
APP_DIR="${2:?folder with the built .app}"
FLOW="${3:?flow file}"
OUT="${4:?out folder}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SUMMARY="${GITHUB_STEP_SUMMARY:-/dev/null}"
FAILED=0
PROBLEMS=0
# Maestro waits this long (milliseconds, MAESTRO_DRIVER_STARTUP_TIMEOUT) for its iOS driver to answer; the 6.9" simulator missed the default twice (run 43733772461).
DRIVER_STARTUP_TIMEOUT_MS=180000
# One try plus one retry per device.
FLOW_ATTEMPTS=2
# A flow attempt that runs longer than this is killed and counts as failed (run 43738623335: maestro hung 82 minutes after "XCTPerformOnMainRunLoop work timed out after 60.0s" and the 90-minute step limit ended the run).
# Unmeasured for a clean five-shot flow; the first two shots took about 4 minutes on the 6.5" simulator.
FLOW_TIME_LIMIT_SECONDS=900
# The script stops starting attempts after this (the workflow step limit is 100 minutes), so the hand-over step always has the shots that came out.
SCRIPT_BUDGET_SECONDS=5700
WATCHDOG_POLL_SECONDS=5
HIERARCHY_TIME_LIMIT_SECONDS=60

# One line per device: label | ';'-separated device-type name patterns, in order | sizes App Store Connect accepts for that slot (WxH, portrait).
# The 6.9" runs last: it failed in three runs in three ways (driver start timeout twice, a hung relaunch once) and the other two sets must not wait behind it.
device_specs() {
  echo 'iphone-6.5|iPhone 11 Pro Max;iPhone Xs Max;iPhone 14 Plus;iPhone 13 Pro Max;iPhone 12 Pro Max|1242x2688 1284x2778'
  echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2064x2752 2048x2732'
  echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868 1290x2796 1260x2736'
}

# The shot names the flow takes, one per line.
flow_shots() {
  grep -oE 'takeScreenshot: store-[A-Za-z0-9-]+' "$FLOW" | sed 's/.*: //'
}

note() {
  echo "$1"
  echo "$1" >> "$SUMMARY"
}

problem() {
  echo "::error::$1"
  echo "- FAILED: $1" >> "$SUMMARY"
  FAILED=1
  PROBLEMS=$((PROBLEMS + 1))
}

# Installs the app on the simulator and prints its bundle id.
install_app() {
  local udid="$1" bundle_app plist
  bundle_app="$(find "$APP_DIR" -maxdepth 1 -name '*.app' -print -quit)"
  [ -d "$bundle_app" ] || return 1
  plist="$bundle_app/Info.plist"
  xcrun simctl install "$udid" "$bundle_app" || return 1
  /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist"
}

# A copy of the flow whose appId is the bundle id; nothing on iOS reads the expect-log markers.
prepare_flow() {
  local bundle_id="$1" target="$2"
  sed -e "s/^appId: .*/appId: $bundle_id/" -e "/^# expect-log:/d" "$FLOW" > "$target"
  grep -q "^appId: $bundle_id\$" "$target"
}

# Prints "ok" when the PNG's pixel size is one of the accepted sizes, else the size it has.
check_size() {
  local png="$1" accepted="$2" width height size
  width="$(sips -g pixelWidth "$png" | awk '/pixelWidth/ {print $2}')"
  height="$(sips -g pixelHeight "$png" | awk '/pixelHeight/ {print $2}')"
  size="${width}x${height}"
  case " $accepted " in
    *" $size "*) echo ok ;;
    *) echo "$size" ;;
  esac
}

# Copies every shot of the flow from Maestro's output folder to <out>/<label>/store-<app>-<label>-<n>-<screen>.png; reports the missing and the wrong-sized.
collect_shots() {
  local label="$1" accepted="$2" test_out="$3" name rest found target verdict
  mkdir -p "$OUT/$label"
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    # Maestro may write takeScreenshot files under --test-output-dir or into the working directory; look in both.
    found="$(find "$test_out" "$PWD" -maxdepth 6 -name "$name.png" -print -quit 2>/dev/null || true)"
    if [ -z "$found" ]; then
      problem "$label: the flow left no $name.png"
      continue
    fi
    rest="${name#store-${APP}-ios-}"
    target="$OUT/$label/store-${APP}-${label}-${rest}.png"
    cp "$found" "$target"
    verdict="$(check_size "$target" "$accepted")"
    if [ "$verdict" = ok ]; then
      note "- $label: $(basename "$target") ok"
    else
      problem "$label: $(basename "$target") is $verdict px, App Store Connect takes $accepted"
    fi
  done < <(flow_shots)
}

# Kills a process and all its descendants (pgrep and pkill ship with macOS; GNU timeout does not).
kill_tree() {
  local pid="$1" child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    kill_tree "$child"
  done
  kill -9 "$pid" 2>/dev/null || true
}

# Kills what is left of Maestro's iOS driver for one simulator: the xcodebuild/XCTest runner names the simulator's id on its command line.
kill_driver() {
  local udid="$1"
  pkill -9 -f "xcodebuild.*$udid" 2>/dev/null || true
  pkill -9 -f "maestro.*$udid" 2>/dev/null || true
}

# Runs a command with a time limit (pure bash watchdog). Returns the command's status, or 124 after killing it and its children when the limit passed.
run_limited() {
  local limit="$1" udid="$2" pid started rc=0
  shift 2
  "$@" &
  pid=$!
  started=$SECONDS
  while kill -0 "$pid" 2>/dev/null; do
    if [ $((SECONDS - started)) -ge "$limit" ]; then
      echo "::warning::killed after $limit s: $*"
      kill_tree "$pid"
      kill_driver "$udid"
      wait "$pid" 2>/dev/null || true
      return 124
    fi
    sleep "$WATCHDOG_POLL_SECONDS"
  done
  wait "$pid" || rc=$?
  return "$rc"
}

# Maestro run with the driver start timeout set; a function so run_limited can start it in the background.
maestro_test() {
  local udid="$1" flow_run="$2" test_out="$3"
  MAESTRO_DRIVER_STARTUP_TIMEOUT="$DRIVER_STARTUP_TIMEOUT_MS" maestro --device "$udid" test --test-output-dir "$test_out" "$flow_run"
}

# Runs the flow on a booted simulator under the time limit; prints Maestro's hierarchy when it fails (not after a kill: the driver is gone).
# Returns 0 on success, 124 when it was killed at the limit, else Maestro's status.
run_flow() {
  local udid="$1" flow_run="$2" test_out="$3" rc=0
  run_limited "$FLOW_TIME_LIMIT_SECONDS" "$udid" maestro_test "$udid" "$flow_run" "$test_out" || rc=$?
  if [ "$rc" = "124" ]; then
    echo "::warning::the flow ran longer than $FLOW_TIME_LIMIT_SECONDS s on $udid and was killed"
  elif [ "$rc" != "0" ]; then
    echo "::group::maestro hierarchy"
    run_limited "$HIERARCHY_TIME_LIMIT_SECONDS" "$udid" maestro --device "$udid" hierarchy || true
    echo "::endgroup::"
  fi
  return "$rc"
}

# Before a retry: the app is stopped on that simulator and no driver of the attempt before is left; after a kill the simulator is also shut down and booted again (a hung main run loop does not recover by itself).
reset_simulator() {
  local udid="$1" bundle_id="$2" after_kill="$3"
  xcrun simctl terminate "$udid" "$bundle_id" 2>/dev/null || true
  kill_driver "$udid"
  if [ "$after_kill" = "yes" ]; then
    xcrun simctl shutdown "$udid" 2>/dev/null || true
    xcrun simctl boot "$udid" 2>/dev/null || true
    xcrun simctl bootstatus "$udid" -b >/dev/null 2>&1 || true
  fi
}

# Empties the attempt's output folder and removes the flow's shots from the working directory (Maestro may write them there), so a retry never mixes in a shot of the attempt before.
reset_attempt() {
  local test_out="$1" name
  rm -rf "$test_out"
  mkdir -p "$test_out"
  while IFS= read -r name; do
    [ -n "$name" ] && rm -f "$PWD/$name.png"
  done < <(flow_shots)
}

# Runs the flow, and once more on a fresh output folder when the first attempt failed (run 43733772461: "iOS driver not ready in time" on the iPhone 6.9", twice, before any step ran).
# Only the last attempt's shots are collected. Returns the last attempt's status.
run_flow_with_retry() {
  local udid="$1" flow_run="$2" test_out="$3" bundle_id="$4" attempt=1 rc killed=no
  while [ "$attempt" -le "$FLOW_ATTEMPTS" ]; do
    if [ $((SECONDS + FLOW_TIME_LIMIT_SECONDS)) -gt "$SCRIPT_BUDGET_SECONDS" ]; then
      note "- no time left for attempt $attempt (script budget $SCRIPT_BUDGET_SECONDS s)"
      return 1
    fi
    reset_attempt "$test_out"
    rc=0
    run_flow "$udid" "$flow_run" "$test_out" || rc=$?
    if [ "$rc" = "0" ]; then
      return 0
    fi
    [ "$rc" = "124" ] && killed=yes || killed=no
    if [ "$attempt" -lt "$FLOW_ATTEMPTS" ]; then
      echo "::warning::attempt $attempt of the store-shot flow failed on $udid (status $rc); retrying once on a fresh output folder (its shots are discarded)"
      note "- RETRY: attempt $attempt failed (status $rc, killed at the time limit: $killed), the flow runs once more (shots of the failed attempt discarded)"
      reset_simulator "$udid" "$bundle_id" "$killed"
    fi
    attempt=$((attempt + 1))
  done
  return 1
}

# One device: boot, install, run, collect, delete the simulator. Any failure is recorded and the next device still runs.
shoot_device() {
  local label="$1" patterns="$2" accepted="$3" udid bundle_id flow_run test_out
  note "### $APP on $label"
  udid="$(SIM_DEVICE_NAME="shots-$label" SIM_DEVICE_PATTERNS="$patterns" GITHUB_ENV='' bash "$HERE/ios-sim-boot.sh" | tail -n 1 | sed 's/^udid=//')"
  if [ -z "$udid" ]; then
    problem "$label: no simulator booted (device patterns: $patterns)"
    return
  fi
  xcrun simctl list devices booted
  if ! bundle_id="$(install_app "$udid")"; then
    problem "$label: the app could not be installed from $APP_DIR"
  else
    flow_run="$RUNNER_TEMP/store-shot-$label.yaml"
    test_out="$RUNNER_TEMP/maestro-out-$label"
    if ! prepare_flow "$bundle_id" "$flow_run"; then
      problem "$label: the appId line was not swapped for $bundle_id"
    elif run_flow_with_retry "$udid" "$flow_run" "$test_out" "$bundle_id"; then
      collect_shots "$label" "$accepted" "$test_out"
    else
      problem "$label: the flow failed (killed at the $FLOW_TIME_LIMIT_SECONDS s limit, or Maestro failed; the log above says which)"
    fi
  fi
  kill_driver "$udid"
  xcrun simctl shutdown "$udid" || true
  xcrun simctl delete "$udid" || true
}

# One summary line per device: all shots, or failed (the FAILED lines above it say why).
device_verdict() {
  local label="$1" problems_before="$2" shots
  shots="$(flow_shots | grep -c .)"
  if [ "$PROBLEMS" -eq "$problems_before" ]; then
    note "**RESULT $label: OK, all $shots shots at an App Store size.**"
  else
    note "**RESULT $label: FAILED, not a full set of $shots shots (see the FAILED lines above).**"
  fi
}

RUNNER_TEMP="${RUNNER_TEMP:-$(mktemp -d)}"
mkdir -p "$OUT"
# The specs come in on fd 3: maestro and xcrun read stdin and would swallow the remaining lines.
while IFS='|' read -r label patterns accepted <&3; do
  problems_before="$PROBLEMS"
  shoot_device "$label" "$patterns" "$accepted" < /dev/null
  device_verdict "$label" "$problems_before"
done 3< <(device_specs)

note "Shots for $APP are in $OUT: $(find "$OUT" -name '*.png' | wc -l | tr -d ' ') PNG(s)."
exit "$FAILED"
