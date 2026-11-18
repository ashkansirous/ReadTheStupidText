#!/usr/bin/env bash
# Slice 291 item 5 (iOS part): takes one app's App Store screenshots on a simulator (iPhone 6.9", iPhone 6.5" or iPad 13"; all three in
# sequence when no device is given) with the store-shot flows, and copies each PNG, renamed for its device, into <out-dir>/<device>/.
# Reuses ios-sim-boot.sh (Slice 244). macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-store-shots.sh <app> <app-dir> <flows> <out-dir> [device]
#   app      dino | rtst: the shot names in the flows are store-<app>-ios-<n>-<screen> and become store-<app>-<device>-<n>-<screen>.png
#   app-dir  the folder holding the built .app (a Debug iossimulator-arm64 build)
#   flows    one Maestro flow file, or a glob that matches one file per shot (expanded here, taken in name order). Each file's appId line is
#            swapped for the app's bundle id and its `# expect-log:` lines are dropped. Several files = one shot each, run one Maestro
#            invocation per shot (7 minutes per attempt); a single file = one unit (10 minutes per attempt, the RTST frames flow).
#   out-dir  the folder that gets <device>/<png> (created)
#   device   iphone-6.5 | ipad-13 | iphone-6.9 | all (default all)
# Env: EXPECTED_SHOTS (PNG count the flow set must produce, else the device fails), STORE_SHOTS_TAG (draft release each shot is uploaded to as
# soon as it is taken; the workflow also downloads that release's PNGs into <out-dir>/<device>/ first, which is how a rerun resumes).
# Per shot: a shot already in <out-dir>/<device>/ at an accepted size is kept and never retaken; a shot that fails, exits 0 without a PNG of an
# accepted size, or hits its time limit is
# retried once on its own (app stopped, driver killed, simulator restarted after a kill); the shots after a shot that failed twice still run.
# Why one Maestro invocation per shot and not one session for the folder: the Maestro docs (context7 /mobile-dev-inc/maestro-docs, "Sequential
# execution") say a folder run goes in the order of config.yaml's executionOrder and continues after a failed flow by default
# (continueOnFailure: true) and reports per flow (JUnit), but they do not say the driver session is shared, and one invocation cannot be
# killed at a per-shot limit without ending the shots after it. So each shot is its own invocation.
# Needs Maestro on the PATH (ios-sim-install-maestro.sh). Exit 0 when every requested device produced every shot at an App Store size, 1
# otherwise; a device that fails is recorded and the next one still runs. No artifacts: the workflow hands the PNGs over (store-screenshots.yml).
set -uo pipefail

APP="${1:?app: dino or rtst}"
APP_DIR="${2:?folder with the built .app}"
FLOW_SPEC="${3:?flow file or glob}"
OUT="${4:?out folder}"
DEVICE="${5:-all}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SUMMARY="${GITHUB_STEP_SUMMARY:-/dev/null}"
FAILED=0
PROBLEMS=0
# Maestro waits this long (milliseconds, MAESTRO_DRIVER_STARTUP_TIMEOUT) for its iOS driver to answer; the 6.9" simulator missed the default twice (run 43733772461).
DRIVER_STARTUP_TIMEOUT_MS=180000
# One try plus one retry per shot.
FLOW_ATTEMPTS=2
# A shot attempt that runs longer than this is killed and counts as failed (run 43738623335: maestro hung 82 minutes after "XCTPerformOnMainRunLoop work timed out after 60.0s").
# 420 s = a driver start of up to 180 s (DRIVER_STARTUP_TIMEOUT_MS) + the longest shot, the Farsi one (language switch, optional 10 s waits, waits of up to 60 s)
# with room; run 43748250465 took about 11 minutes for all five shots of one session on the iPhone 6.5".
# A single-file unit (the whole RTST frames flow: the 300 s pack wait plus three screens) gets 600 s, so one retry fits the budget below.
SHOT_TIME_LIMIT_SECONDS=420
SINGLE_UNIT_TIME_LIMIT_SECONDS=600
# A flow that runs the full-shelf hook waits up to 600 s for the shelf (flow: 600000 ms) after the driver start (180 s) and its own steps: 900 s, so the first full fill is not killed mid-wait.
FULL_SHELF_UNIT_TIME_LIMIT_SECONDS=900
# Shutting down, booting and waiting for the simulator after a kill (check-pass finding 2: it had no limit).
RESTART_TIME_LIMIT_SECONDS=300
# A simctl call (install, terminate, shutdown, delete) that hangs is killed after this.
SIMCTL_TIME_LIMIT_SECONDS=120
# Room for ios-sim-boot.sh before the first shot (it has no limit of its own).
BOOT_RESERVE_SECONDS=300
# The driver warm-up of ios-sim-driver-ready.sh (Slice 393 item 23): two attempts of DRIVER_READY_ATTEMPT_SECONDS (above the 300 s default
# MAESTRO_DRIVER_STARTUP_TIMEOUT of that script) with a 10 s pause; it stops itself, this is the outer watchdog. No reserve is held for it: the seconds it
# really takes are charged to the script budget by $SECONDS, so a driver that starts at once costs nothing extra.
DRIVER_READY_ATTEMPT_SECONDS=330
DRIVER_READY_BUDGET_SECONDS=$((2 * DRIVER_READY_ATTEMPT_SECONDS + 10 + 30))
# The script stops starting new attempts after this (the missing shots are reported and the exit code is 1), so the hand-over step always has the
# shots that came out. 2700 s = 45 min; the workflow's shots step is 48 minutes and its job 55 (about 6 minutes of setup before the step, run
# 43748250465: 3 min 20 s). The worst case does NOT fit one job: boot 300 + five shots at two attempts of 420 s (4200) + a restart 300 = 4800 s.
# It is not meant to: every shot is uploaded when taken, so `gh run rerun <run id> --failed` resumes and retakes only what is missing. RTST:
# boot 300 + 2 x 600 + restart 300 + hierarchy 2 x 60 = 1920 s, which fits.
SCRIPT_BUDGET_SECONDS="${SCRIPT_BUDGET_SECONDS:-2700}"
# How many PNGs the app's flow set must produce on a device (the workflow sets it: Dino one per flow file = 5, RTST 4); empty = whatever the flows name.
EXPECTED_SHOTS="${EXPECTED_SHOTS:-}"
# When set (the workflow sets it), each shot is uploaded to this draft release as soon as it is taken, so a job that later times out has handed it over.
STORE_SHOTS_TAG="${STORE_SHOTS_TAG:-}"
WATCHDOG_POLL_SECONDS=5
HIERARCHY_TIME_LIMIT_SECONDS=60
# Set by run_limited: "yes" when the last call was killed at its limit (a command that exits 124 by itself is not mistaken for a kill).
LIMIT_HIT=no
# Set when a simulator did not come back after a restart: the rest of that device is skipped.
SIM_BROKEN=no
UNITS=()
UNIT_LIMIT="$SHOT_TIME_LIMIT_SECONDS"

# One line per device: label | ';'-separated device-type name patterns, in order | sizes App Store Connect accepts for that slot (WxH, portrait).
device_specs() {
  echo 'iphone-6.5|iPhone 11 Pro Max;iPhone Xs Max;iPhone 14 Plus;iPhone 13 Pro Max;iPhone 12 Pro Max|1242x2688 1284x2778'
  echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2064x2752 2048x2732'
  echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868 1290x2796 1260x2736'
}

# The shot names one flow file takes, one per line.
unit_shots() {
  grep -oE 'takeScreenshot: store-[A-Za-z0-9-]+' "$1" | sed 's/.*: //'
}

# Every shot of every unit, one per line.
all_shots() {
  local unit
  for unit in "${UNITS[@]}"; do
    unit_shots "$unit"
  done
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

# True when `needed` more seconds fit in the script budget.
time_left_for() {
  [ $((SECONDS + $1)) -le "$SCRIPT_BUDGET_SECONDS" ]
}

# Fills UNITS from the flows argument (a file, or a glob: unquoted expansion, sorted by name) and sets the per-attempt limit.
load_units() {
  # shellcheck disable=SC2206
  UNITS=( $FLOW_SPEC )
  local unit
  for unit in "${UNITS[@]}"; do
    [ -f "$unit" ] || { echo "::error::no flow file: $unit"; exit 1; }
  done
  if [ "${#UNITS[@]}" -gt 1 ]; then
    UNIT_LIMIT="$SHOT_TIME_LIMIT_SECONDS"
  else
    UNIT_LIMIT="$SINGLE_UNIT_TIME_LIMIT_SECONDS"
  fi
}

# Installs the app on the simulator and prints its bundle id.
install_app() {
  local udid="$1" bundle_app plist
  bundle_app="$(find "$APP_DIR" -maxdepth 1 -name '*.app' -print -quit)"
  [ -d "$bundle_app" ] || return 1
  plist="$bundle_app/Info.plist"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl install "$udid" "$bundle_app" >&2 || return 1
  /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist"
}

# A copy of one flow whose appId is the bundle id; nothing on iOS reads the expect-log markers.
prepare_flow() {
  local source="$1" bundle_id="$2" target="$3"
  sed -e "s/^appId: .*/appId: $bundle_id/" -e "/^# expect-log:/d" "$source" > "$target"
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

# The path a shot gets in the out folder: store-<app>-ios-<n>-<screen> becomes <out>/<label>/store-<app>-<label>-<n>-<screen>.png.
shot_target() {
  local label="$1" name="$2" rest
  rest="${name#store-${APP}-ios-}"
  echo "$OUT/$label/store-${APP}-${label}-${rest}.png"
}

# True when every shot of the unit is already in the out folder at an accepted size (kept, never retaken).
unit_complete() {
  local label="$1" accepted="$2" unit="$3" name target count=0
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    count=$((count + 1))
    target="$(shot_target "$label" "$name")"
    [ -f "$target" ] || return 1
    [ "$(check_size "$target" "$accepted")" = ok ] || return 1
  done < <(unit_shots "$unit")
  [ "$count" -gt 0 ]
}

# Hands one taken shot to this run's draft release at once (a job that later times out or is cancelled has still handed it over, and a rerun or a
# resume_run dispatch starts from it). A failed upload is a warning: the hand-over step uploads everything again.
upload_shot() {
  local target="$1"
  [ -n "$STORE_SHOTS_TAG" ] || return 0
  if run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "" gh release upload "$STORE_SHOTS_TAG" "$target" --clobber --repo "${GITHUB_REPOSITORY:?}"; then
    echo "uploaded $(basename "$target") to the draft release $STORE_SHOTS_TAG"
  else
    echo "::warning::could not upload $(basename "$target") to $STORE_SHOTS_TAG yet; the hand-over step retries"
  fi
}

# Copies every shot of one unit from Maestro's output folder to the out folder (and uploads each good one). Returns 1 when a shot is missing or
# has a wrong size, so the caller counts the attempt as failed and retries; the good shots of the unit stay.
collect_unit() {
  local label="$1" accepted="$2" unit="$3" test_out="$4" name found target verdict bad=0
  mkdir -p "$OUT/$label"
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    # Maestro may write takeScreenshot files under --test-output-dir or into the working directory; look in both.
    # Only where THIS attempt wrote it: this attempt's output folder (emptied before the attempt) or the exact $PWD/<name>.png (removed before the
    # attempt by reset_attempt); a stale copy elsewhere in the checkout is never collected.
    found="$(find "$test_out" -maxdepth 6 -name "$name.png" -print -quit 2>/dev/null || true)"
    if [ -z "$found" ] && [ -f "$PWD/$name.png" ]; then found="$PWD/$name.png"; fi
    target="$(shot_target "$label" "$name")"
    if [ -z "$found" ]; then
      echo "::warning::$label: the flow exited 0 but left no $name.png"
      rm -f "$target"
      bad=1
      continue
    fi
    cp "$found" "$target"
    verdict="$(check_size "$target" "$accepted")"
    if [ "$verdict" = ok ]; then
      note "- $label: $(basename "$target") ok"
      upload_shot "$target"
    else
      rm -f "$target"
      echo "::warning::$label: $(basename "$target") is $verdict px, App Store Connect takes $accepted"
      bad=1
    fi
  done < <(unit_shots "$unit")
  return "$bad"
}

# Removes PNGs in the device's out folder that no shot of the flows names (a stale file from an earlier run's draft release).
prune_unexpected() {
  local label="$1" png expected name found
  expected="$(while IFS= read -r name; do [ -n "$name" ] && basename "$(shot_target "$label" "$name")"; done < <(all_shots))"
  for png in "$OUT/$label"/*.png; do
    [ -f "$png" ] || continue
    found="$(echo "$expected" | grep -Fx "$(basename "$png")" || true)"
    if [ -z "$found" ]; then
      note "- $label: $(basename "$png") from the earlier draft is not a shot of these flows, removed"
      rm -f "$png"
      # The stale asset also leaves the draft, so downloading the whole draft never shows it (a failure only warns).
      if [ -n "$STORE_SHOTS_TAG" ]; then
        run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "" gh release delete-asset "$STORE_SHOTS_TAG" "$(basename "$png")" --yes --repo "${GITHUB_REPOSITORY:?}" \
          || echo "::warning::could not delete $(basename "$png") from the draft release $STORE_SHOTS_TAG"
      fi
    fi
  done
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
  [ -n "$udid" ] || return 0
  pkill -9 -f "xcodebuild.*$udid" 2>/dev/null || true
  pkill -9 -f "maestro.*$udid" 2>/dev/null || true
}

# Runs a command with a time limit (pure bash watchdog). Returns the command's status, or 124 after killing it and its children when the
# limit passed; LIMIT_HIT says which (yes only for a watchdog kill).
run_limited() {
  local limit="$1" udid="$2" pid started rc=0
  shift 2
  LIMIT_HIT=no
  "$@" &
  pid=$!
  started=$SECONDS
  while kill -0 "$pid" 2>/dev/null; do
    if [ $((SECONDS - started)) -ge "$limit" ]; then
      echo "::warning::killed after $limit s: $*"
      LIMIT_HIT=yes
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

# Slice 404: prints the app's test-hook log (the HOOK lines; the Debug build appends every line to Library/test-hook.log, TestHookLog.iOS.cs) and the
# folder listing the hook reads, so a failed shot says why the hook did nothing. Never fails the script.
print_hook_log() {
  local udid="$1" bundle_id="$2" container
  container="$(xcrun simctl get_app_container "$udid" "$bundle_id" data 2>/dev/null)" || container=""
  echo "::group::app test-hook log ($bundle_id)"
  if [ -z "$container" ]; then
    echo "no data container found for $bundle_id on $udid"
  else
    echo "container: $container"
    if [ -f "$container/Library/test-hook.log" ]; then
      grep -E 'HOOK|RTST-TEST' "$container/Library/test-hook.log" | tail -n 60 || true
    else
      echo "no Library/test-hook.log (no hook line was ever written)"
    fi
    ls -la "$container/Library" "$container/Library/full-shelf" 2>&1 | head -n 40 || true
  fi
  echo "::endgroup::"
  return 0
}

# Runs one flow on a booted simulator under the time limit; prints Maestro's hierarchy when it fails (not after a kill: the driver is gone).
# Returns 0 on success, else the status; LIMIT_HIT=yes when it was killed at the limit.
run_flow() {
  local udid="$1" flow_run="$2" test_out="$3" bundle_id="$4" rc=0 killed
  run_limited "$UNIT_LIMIT" "$udid" maestro_test "$udid" "$flow_run" "$test_out" || rc=$?
  killed="$LIMIT_HIT"
  if [ "$killed" = "yes" ]; then
    echo "::warning::the flow ran longer than $UNIT_LIMIT s on $udid and was killed"
  elif [ "$rc" != "0" ]; then
    echo "::group::maestro hierarchy"
    run_limited "$HIERARCHY_TIME_LIMIT_SECONDS" "$udid" maestro --device "$udid" hierarchy || true
    echo "::endgroup::"
  fi
  [ "$rc" = "0" ] || print_hook_log "$udid" "$bundle_id"
  LIMIT_HIT="$killed"
  return "$rc"
}

# Shutdown, boot and wait for a simulator whose main run loop hung (a function so run_limited can start it in the background).
restart_simulator() {
  local udid="$1"
  xcrun simctl shutdown "$udid" 2>/dev/null || true
  xcrun simctl boot "$udid" 2>/dev/null || true
  xcrun simctl bootstatus "$udid" -b >/dev/null 2>&1
}

# Before a retry: the app is stopped on that simulator and no driver of the attempt before is left; after a kill the simulator is also shut
# down and booted again (a hung main run loop does not recover by itself), within RESTART_TIME_LIMIT_SECONDS. Returns 1 when it did not come back.
reset_simulator() {
  local udid="$1" bundle_id="$2" after_kill="$3"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl terminate "$udid" "$bundle_id" >/dev/null 2>&1 || true
  kill_driver "$udid"
  if [ "$after_kill" = "yes" ]; then
    run_limited "$RESTART_TIME_LIMIT_SECONDS" "$udid" restart_simulator "$udid" || return 1
  fi
  return 0
}

# Empties the attempt's output folder and removes the unit's shots from the working directory (Maestro may write them there), so a retry never mixes in a shot of the attempt before.
reset_attempt() {
  local unit="$1" test_out="$2" name
  rm -rf "$test_out"
  mkdir -p "$test_out"
  while IFS= read -r name; do
    [ -n "$name" ] && rm -f "$PWD/$name.png"
  done < <(unit_shots "$unit")
}

# One shot (or the single RTST unit): kept when already taken at an accepted size, else run, and once more on a fresh output folder when
# the first attempt failed (run 43733772461: "iOS driver not ready in time" on the iPhone 6.9", twice, before any step ran).
# Only the last attempt's shots are collected. A shot that failed twice is a problem; the caller goes on with the next one.
run_unit_body() {
  local label="$1" accepted="$2" udid="$3" bundle_id="$4" unit="$5" index="$6" flow_run test_out attempt=1 rc killed unit_name
  unit_name="$(basename "$unit" .yaml)"
  if unit_complete "$label" "$accepted" "$unit"; then
    note "- $label: $unit_name kept (already taken at an accepted size, not retaken)"
    return 0
  fi
  flow_run="$RUNNER_TEMP/store-shot-$label-$index.yaml"
  test_out="$RUNNER_TEMP/maestro-out-$label-$index"
  if ! prepare_flow "$unit" "$bundle_id" "$flow_run"; then
    problem "$label: $unit_name: the appId line was not swapped for $bundle_id"
    return 1
  fi
  while [ "$attempt" -le "$FLOW_ATTEMPTS" ]; do
    if ! time_left_for $((UNIT_LIMIT + HIERARCHY_TIME_LIMIT_SECONDS)); then
      problem "$label: $unit_name: no time left for attempt $attempt (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
      return 1
    fi
    reset_attempt "$unit" "$test_out"
    rc=0
    run_flow "$udid" "$flow_run" "$test_out" "$bundle_id" || rc=$?
    killed="$LIMIT_HIT"
    if [ "$rc" = "0" ]; then
      # Exit 0 with a PNG missing or of a wrong size is a failed attempt too (it gets the retry).
      if collect_unit "$label" "$accepted" "$unit" "$test_out"; then
        return 0
      fi
      rc=1
      killed=no
    fi
    if [ "$attempt" -lt "$FLOW_ATTEMPTS" ]; then
      echo "::warning::attempt $attempt of $unit_name failed on $udid (status $rc); retrying once on a fresh output folder (its shots are discarded)"
      note "- RETRY: $label $unit_name attempt $attempt failed (status $rc, killed at the $UNIT_LIMIT s limit: $killed), it runs once more (shots of the failed attempt discarded)"
      if ! time_left_for $((RESTART_TIME_LIMIT_SECONDS + UNIT_LIMIT + HIERARCHY_TIME_LIMIT_SECONDS)); then
        problem "$label: $unit_name: no time left for the retry (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
        return 1
      fi
      if ! reset_simulator "$udid" "$bundle_id" "$killed"; then
        SIM_BROKEN=yes
        problem "$label: the simulator did not come back within $RESTART_TIME_LIMIT_SECONDS s after $unit_name was killed; the rest of this device is skipped"
        return 1
      fi
    fi
    attempt=$((attempt + 1))
  done
  problem "$label: $unit_name failed twice (killed at the $UNIT_LIMIT s limit, or Maestro failed; the log above says which)"
  return 1
}

# Slice 404 (review pass: the wait in a flow that runs the full-shelf hook is 600 s, above the 420 s a shot gets): a unit that calls the hook gets the longer per-attempt limit, the rest keep theirs.
run_unit() {
  local base_limit="$UNIT_LIMIT" rc=0
  if grep -qs 'dino-test://full-shelf' "$5"; then
    UNIT_LIMIT="$FULL_SHELF_UNIT_TIME_LIMIT_SECONDS"
  fi
  run_unit_body "$@" || rc=$?
  UNIT_LIMIT="$base_limit"
  return "$rc"
}

# Every unit in order on the booted simulator; one that failed does not stop the next, a simulator that did not come back does.
run_units() {
  local label="$1" accepted="$2" udid="$3" bundle_id="$4" unit index=0
  SIM_BROKEN=no
  for unit in "${UNITS[@]}"; do
    index=$((index + 1))
    if [ "$SIM_BROKEN" = "yes" ]; then
      problem "$label: $(basename "$unit" .yaml) skipped, the simulator is not usable"
      continue
    fi
    run_unit "$label" "$accepted" "$udid" "$bundle_id" "$unit" "$index" || true
  done
}

# Slice 404: the Dino full-shelf hook imports a PAID book from a package file (the store refuses a paid download without a real receipt; nothing is faked).
# BookBuilder writes artifacts/store/<id>/story-v<n>.dino; the packages are copied into <app data container>/Library/full-shelf/<id>.dino (the hook's
# FullShelfPlan.PackageFolder inside FileSystem.AppDataDirectory, which is the container's Library folder on iOS). A flow that never calls the hook needs none of it.
REPO_ROOT="$(cd "$HERE/../.." && pwd)"
STORE_ROOT="$REPO_ROOT/artifacts/store"

flows_use_full_shelf() {
  [ "$APP" = dino ] && grep -qs 'full-shelf' "${UNITS[@]}"
}

# The stems of the store books under content/dino that have no package under artifacts/store yet, one per line (a stem's id is the "id" line of its book.json).
stems_without_package() {
  local json id
  for json in "$REPO_ROOT"/content/dino/*/book.json; do
    [ -f "$json" ] || continue
    grep -qE '"kind": *"(Store|Starter)"' "$json" || continue # Starters too: Good Enough and Tortoise and the Hare are paid in the live list (run 44411861765: missing-package)
    id="$(sed -n 's/^ *"id": *"\([^"]*\)".*/\1/p' "$json" | head -n 1)"
    [ -n "$id" ] || continue
    ls "$STORE_ROOT/$id"/story-v*.dino >/dev/null 2>&1 || basename "$(dirname "$json")"
  done
}

# Pulls each stem's Git LFS print-size pictures (content/dino/<stem>/high-res, kept out of a normal fetch by .lfsconfig) and creates its artifacts/store/<id> folder;
# BookBuilder build refuses a book whose high-res folder holds only pointers. Returns 1, naming the stem, when a pull fails.
pull_book_high_res() {
  local stem id
  for stem in "$@"; do
    if ! (cd "$REPO_ROOT" && git lfs pull --include "content/dino/$stem/high-res/**" --exclude "") >&2; then
      echo "::error::git lfs pull for $stem (content/dino/$stem/high-res) failed, so its book package cannot be built"
      return 1
    fi
    id="$(sed -n 's/^ *"id": *"\([^"]*\)".*/\1/p' "$REPO_ROOT/content/dino/$stem/book.json" | head -n 1)"
    [ -z "$id" ] || mkdir -p "$STORE_ROOT/$id"
  done
}

# Builds the missing packages with BookBuilder (dotnet is set up by store-screenshots.yml). Returns 1, naming why, when it cannot.
build_missing_packages() {
  local stems
  stems="$(stems_without_package | tr '\n' ' ')"
  [ -n "$stems" ] || return 0
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "::error::the book packages for [$stems] are missing under artifacts/store and dotnet is not on the PATH to build them (dotnet run --project tools/dino/DinoReader.BookBuilder -- build <stem>)"
    return 1
  fi
  echo "building the missing book packages: $stems"
  pull_book_high_res $stems || return 1
  # shellcheck disable=SC2086
  (cd "$REPO_ROOT" && dotnet run --project tools/dino/DinoReader.BookBuilder -- build $stems) >&2
}

# Copies the newest package of every book into the app's full-shelf folder on the simulator. The data container exists after the app's first start, so a launch and a terminate come first when it does not yet.
copy_full_shelf_packages() {
  local udid="$1" bundle_id="$2" container folder dir id newest count=0
  container="$(xcrun simctl get_app_container "$udid" "$bundle_id" data 2>/dev/null)" || container=""
  if [ -z "$container" ]; then
    xcrun simctl launch "$udid" "$bundle_id" >/dev/null 2>&1 || true
    sleep 3
    xcrun simctl terminate "$udid" "$bundle_id" >/dev/null 2>&1 || true
    container="$(xcrun simctl get_app_container "$udid" "$bundle_id" data 2>/dev/null)" || return 1
  fi
  folder="$container/Library/full-shelf"
  mkdir -p "$folder" || return 1
  for dir in "$STORE_ROOT"/*/; do
    id="$(basename "$dir")"
    newest="$(ls "$dir"story-v*.dino 2>/dev/null | sed -n 's/.*story-v\([0-9][0-9]*\)\.dino$/\1 &/p' | sort -n | tail -n 1 | cut -d' ' -f2-)"
    [ -n "$newest" ] || continue
    cp "$newest" "$folder/$id.dino" || return 1
    count=$((count + 1))
  done
  echo "copied $count book package(s) to $folder"
  [ "$count" -gt 0 ]
}

# Puts the packages on the simulator when a flow uses the hook; a failure is a problem and no shot is taken (the hook would end `failed ... missing-package`).
prepare_full_shelf() {
  local label="$1" udid="$2" bundle_id="$3"
  flows_use_full_shelf || return 0
  if ! build_missing_packages; then
    problem "$label: the book packages for the full-shelf hook could not be built"
    return 1
  fi
  if ! copy_full_shelf_packages "$udid" "$bundle_id"; then
    problem "$label: the book packages could not be copied into the simulator's app container"
    return 1
  fi
}

# One device: boot, install, run every shot, delete the simulator. Any failure is recorded and the next device still runs.
shoot_device() {
  local label="$1" patterns="$2" accepted="$3" udid bundle_id
  note "### $APP on $label"
  if ! time_left_for $((BOOT_RESERVE_SECONDS + UNIT_LIMIT)); then
    problem "$label: no time left to boot a simulator (script budget $SCRIPT_BUDGET_SECONDS s)"
    return
  fi
  udid="$(SIM_DEVICE_NAME="shots-$label" SIM_DEVICE_PATTERNS="$patterns" GITHUB_ENV='' bash "$HERE/ios-sim-boot.sh" | tail -n 1 | sed 's/^udid=//')"
  if [ -z "$udid" ]; then
    problem "$label: no simulator booted (device patterns: $patterns)"
    return
  fi
  xcrun simctl list devices booted
  if ! bundle_id="$(install_app "$udid")"; then
    problem "$label: the app could not be installed from $APP_DIR"
  elif ! prepare_full_shelf "$label" "$udid" "$bundle_id"; then
    echo "no shot is taken on $label without the book packages"
  else
    # Slice 393 item 23: the first shot no longer pays for the cold driver start; a driver that never answers is a warning, the shot's own retry follows.
    local warm_started=$SECONDS
    if ! run_limited "$DRIVER_READY_BUDGET_SECONDS" "$udid" env DRIVER_READY_ATTEMPTS=2 DRIVER_READY_TIME_LIMIT_SECONDS="$DRIVER_READY_ATTEMPT_SECONDS" bash "$HERE/ios-sim-driver-ready.sh" "$udid"; then
      note "- WARNING: $label: the Maestro driver did not answer before the first shot; the shots go on with their own retry"
    fi
    echo "driver warm-up on $label took $((SECONDS - warm_started)) s (charged to the $SCRIPT_BUDGET_SECONDS s script budget)"
    run_units "$label" "$accepted" "$udid" "$bundle_id"
  fi
  kill_driver "$udid"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl shutdown "$udid" || true
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl delete "$udid" || true
}

# One summary line per device: which shots it produced, then OK or failed (the FAILED lines above say why).
device_verdict() {
  local label="$1" problems_before="$2" accepted="$3" name target made="" missing="" total=0 got=0
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    total=$((total + 1))
    target="$(shot_target "$label" "$name")"
    if [ -f "$target" ] && [ "$(check_size "$target" "$accepted")" = ok ]; then
      got=$((got + 1))
      made="$made $(basename "$target")"
    else
      missing="$missing $(basename "$target")"
    fi
  done < <(all_shots)
  note "Shots $label produced ($got of $total):${made:- none}"
  if [ -n "$EXPECTED_SHOTS" ] && { [ "$total" -lt "$EXPECTED_SHOTS" ] || [ "$got" -lt "$EXPECTED_SHOTS" ]; }; then
    problem "$label: $got PNG(s) at an App Store size, $EXPECTED_SHOTS expected for this flow set ($total named by the flows)"
  fi
  if [ "$PROBLEMS" -eq "$problems_before" ] && [ "$got" -eq "$total" ]; then
    note "**RESULT $label: OK, all $total shots at an App Store size.**"
  else
    note "**RESULT $label: FAILED, $got of $total shots (see the FAILED lines above).**"
    [ -z "$missing" ] || note "Missing on $label:$missing. Resume with: gh run rerun ${GITHUB_RUN_ID:-<run id>} --failed (it retakes only the missing shots)."
    FAILED=1
  fi
}

RUNNER_TEMP="${RUNNER_TEMP:-$(mktemp -d)}"
mkdir -p "$OUT"
load_units
MATCHED=0
# The specs come in on fd 3: maestro and xcrun read stdin and would swallow the remaining lines.
while IFS='|' read -r label patterns accepted <&3; do
  if [ "$DEVICE" != "all" ] && [ "$DEVICE" != "$label" ]; then
    continue
  fi
  MATCHED=1
  problems_before="$PROBLEMS"
  prune_unexpected "$label"
  shoot_device "$label" "$patterns" "$accepted" < /dev/null
  device_verdict "$label" "$problems_before" "$accepted"
done 3< <(device_specs)

if [ "$MATCHED" -eq 0 ]; then
  echo "::error::unknown device '$DEVICE' (iphone-6.5, ipad-13, iphone-6.9 or all)"
  exit 1
fi
note "Shots for $APP are in $OUT: $(find "$OUT" -name '*.png' | wc -l | tr -d ' ') PNG(s)."
exit "$FAILED"
