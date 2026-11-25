#!/usr/bin/env bash
# Slice 291 item 5 (iOS part): takes one app's App Store screenshots on a simulator (iPhone 6.9", iPhone 6.5" or iPad 13"; all three in
# sequence when no device is given) with the store-shot flows, and copies each PNG, renamed for its device, into <out-dir>/<device>/.
# Reuses ios-sim-boot.sh (Slice 244). macOS bash 3.2: no associative arrays, no mapfile, no ${var,,}.
#
# Usage: ios-sim-store-shots.sh <app> <app-dir> <flows> <out-dir> [device]
#   app      dino | rtst: the shot names in the flows are store-<app>-ios-<n>-<screen> and become store-<app>-<device>-<n>-<screen>.png
#   app-dir  the folder holding the built .app (a Debug iossimulator-arm64 build)
#   flows    (with MEDIA=clips: the shelf set-up flow, which has no shot) one Maestro flow file, or a glob that matches one file per shot (expanded here, taken in name order). Each file's appId line is
#            swapped for the app's bundle id and its `# expect-log:` lines are dropped. Several files = one shot each, run one Maestro
#            invocation per shot (7 minutes per attempt); a single file = one unit (10 minutes per attempt, the RTST frames flow).
#   out-dir  the folder that gets <device>/<png> (created)
#   device   iphone-6.5 | ipad-13 | iphone-6.9 | all (default all)
# Env: EXPECTED_SHOTS (PNG count the flow set must produce, else the device fails), STORE_SHOTS_TAG (draft release each shot is uploaded to as
# soon as it is taken; the workflow also downloads that release's PNGs into <out-dir>/<device>/ first, which is how a rerun resumes).
# Slice 433: Dino's iPad 13" is shot in landscape (2752 x 2064; RTST's stays upright): the script adds `- setOrientation: LANDSCAPE_LEFT` after the `---` line of its copy of every flow on that device, so one set of flows serves both.
# VIDEO_FLOWS (glob, optional; Dino) = the App Store preview clip flows (.maestro/dino/store-shot-ios-video-<clip>.yaml), one per clip (Slice 433, review pass 2: one recording per clip). On the iPhone 6.9" and the iPad 13" each clip first runs its
# unrecorded setup flow (store-shot-ios-videoprep-<clip>.yaml), then `xcrun simctl io <udid> recordVideo` starts, the clip flow runs alone and the recorder is stopped, so the mp4 holds exactly that clip. It is kept as
# <out-dir>/<device>/store-<app>-<device>-video-<clip>.mp4 (EXPECTED_VIDEOS of them, else the device fails; a file that cannot be read to the end counts as no recording). Apple's preview sizes need no 6.5" footage. tools/dino/promo/store/ trims and captions them.
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
# shots that came out. 3600 s = 60 min (Slice 433: it was 2700 s before the preview clips); the workflow's shots step is 66 minutes and its job 80 (about 6 minutes of setup before the step, run
# 43748250465: 3 min 20 s). The worst case does NOT fit one job: boot 300 + seven shots at two attempts of 420 s (5880) + seven clips at two attempts of 480 s (a setup and a recorded flow of 240 s: 6720) + a restart 300 = 13200 s.
# Slice 433 (run 45479054864, device findings 2-4): every attempt now starts with the Open-in pre-flow (PREFLIGHT_TIME_LIMIT_SECONDS 20 s at most, and skipped on a device where the URL scheme is pre-approved, run 45491086879 finding 1) and every retry is preceded by a simulator restart (RESTART_TIME_LIMIT_SECONDS 300 s at most), and
# each "will it fit" test above counts them: a shot attempt is 20 + 420 + 60 = 500 s (shot 1, the full-shelf fill: 20 + 900 + 60 = 980 s), a clip attempt 20 + 2 x 240 + 2 x 60 + 20 + 10 = 650 s, a retry adds 300 s. No attempt starts unless its whole cost fits
# in the 3600 s, so the script ends within 3600 s = 60 min; the workflow's step (66 min) and job (80 min: 6 setup + 66 step + 2 hand-over = 74) keep their margin.
# It is not meant to: every shot is uploaded when taken, so `gh run rerun <run id> --failed` resumes and retakes only what is missing. RTST:
# boot 300 + 2 x 600 + restart 300 + hierarchy 2 x 60 = 1920 s, which fits.
SCRIPT_BUDGET_SECONDS="${SCRIPT_BUDGET_SECONDS:-3600}"
# Slice 433: the preview footage. Each clip is a setup flow plus a recorded flow of its own, each with this limit. The recorder is given this long to write its file after it is stopped.
VIDEO_FLOWS="${VIDEO_FLOWS:-}"
EXPECTED_VIDEOS="${EXPECTED_VIDEOS:-}"
# Slice 433 (run 45466463555: slow shots used the whole budget and all seven clips were skipped): shots and clips are separate jobs with separate budgets.
# MEDIA = shots (no clips are recorded), clips (the workflow passes the shelf set-up flow as <flows>, see store-shot-ios-shelfprep.yaml, and the clips follow it) or all (both in one run, the default).
MEDIA="${MEDIA:-all}"
case "$MEDIA" in
  shots) VIDEO_FLOWS="" ;;
  clips | all) ;;
  *) echo "::error::MEDIA must be shots, clips or all, not '$MEDIA'"; exit 1 ;;
esac
VIDEO_TIME_LIMIT_SECONDS=240   # each of a clip's two flows (setup, recorded)
RECORDER_FINISH_SECONDS=20
RECORDER_START_CHECK_SECONDS=5   # the recorder must be alive and its file must be growing within this long of starting
RECORDER_KILLED=no
# "yes" while the iPad 13" is shot: every flow copy is turned to landscape.
LANDSCAPE=no
VIDEO_UNITS=()
# How many PNGs the app's flow set must produce on a device (the workflow sets it: Dino one per flow file = 5, RTST 4); empty = whatever the flows name.
EXPECTED_SHOTS="${EXPECTED_SHOTS:-}"
# When set (the workflow sets it), each shot is uploaded to this draft release as soon as it is taken, so a job that later times out has handed it over.
STORE_SHOTS_TAG="${STORE_SHOTS_TAG:-}"
WATCHDOG_POLL_SECONDS=5
HIERARCHY_TIME_LIMIT_SECONDS=60
# Slice 433 (iOS run 45479054864, finding 2): before every attempt (first tries and retries) a small Maestro flow answers a SpringBoard "Open in <app>?" alert that a failed attempt left up.
# It is one Maestro start (a driver start of up to DRIVER_STARTUP_TIMEOUT_MS = 180 s on a cold driver, a few seconds on a warm one) and no step when no alert is up; 20 s is its watchdog, and a miss is only a warning.
# Run 45491086879 (finding 1): on the iPad in landscape Maestro cannot tap that alert at all (Maestro issue 2065) and the pre-flow ran into its 60 s limit every attempt, so the alert is now prevented (approve_url_scheme: the
# URL scheme is written into the simulator's LaunchServices approval plist) and the pre-flow is skipped on a device where that was confirmed; it stays, with this short limit, only as the fallback when the approval was not confirmed.
PREFLIGHT_TIME_LIMIT_SECONDS=20
# The URL scheme the flows open with openLink (every openLink in .maestro/dino/store-shot-ios-*.yaml is dino-test://) and the LaunchServices file that holds the approvals (Maestro issue 940, comment by satya164, 2025-11-27).
APPROVED_URL_SCHEME="dino-test"
SCHEME_APPROVAL_BRIDGE="com.apple.CoreSimulator.CoreSimulatorBridge"
# "yes" once the scheme approval of the current device was written and read back.
SCHEME_APPROVED=no
PREFLIGHT_FLOW="$HERE/../../.maestro/dino/store-shot-ios-preflight.yaml"
# Set when the clips' shelf set-up flow (store-shot-ios-shelfprep.yaml) failed on the device: no clip is recorded on a wrong shelf (run 45479054864: the iPad clips ran on a 4-book shelf).
SHELF_PREP_FAILED=no
# Set by run_limited: "yes" when the last call was killed at its limit (a command that exits 124 by itself is not mistaken for a kill).
LIMIT_HIT=no
# Set when a simulator did not come back after a restart: the rest of that device is skipped.
SIM_BROKEN=no
# Fail fast (ClaudeSkills device-test-pipeline section 5): when the first FAIL_FAST_AFTER shots (or clips) a device really attempts all fail twice and none
# came out, the rest of that device is stopped with one ::error:: line instead of spending its whole budget failing (iOS run 45491086879: the iPad shot
# for most of an hour and got 0). After an early success the device also stops once FAIL_FAST_AFTER + 1 attempted units fail in a row (a device that
# broke mid-run). A shot kept from a resumed run is not an attempt and counts neither way.
FAIL_FAST_AFTER="${FAIL_FAST_AFTER:-2}"
case "$FAIL_FAST_AFTER" in
  '' | *[!0-9]* | 0) echo "::error::FAIL_FAST_AFTER must be a whole number of at least 1, not '$FAIL_FAST_AFTER'"; exit 1 ;;
esac
UNITS_OK=0
UNITS_FAILED=0
UNITS_FAILED_IN_A_ROW=0
FAIL_FAST_HIT=no
# Set to "yes" by a unit that was kept from a resumed run instead of attempted.
UNIT_KEPT=no
UNITS=()
UNIT_LIMIT="$SHOT_TIME_LIMIT_SECONDS"

# One line per device: label | ';'-separated device-type name patterns, in order | sizes App Store Connect accepts for that slot (WxH, portrait).
device_specs() {
  echo 'iphone-6.5|iPhone 11 Pro Max;iPhone Xs Max;iPhone 14 Plus;iPhone 13 Pro Max;iPhone 12 Pro Max|1242x2688 1284x2778'
  if [ "$APP" = dino ]; then
    # Slice 433: Dino's iPad shots are landscape and its iPhone 6.9" shots are exactly the frame's size (tools/emulator/store-captions.ps1 does not stretch or crop to another).
    echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2752x2064'
    echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868'
  else
    echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2064x2752 2048x2732'
    echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868 1290x2796 1260x2736'
  fi
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

# Slice 433 (review pass 2, finding 5): flow files in the order of their names WITHOUT the .yaml, byte-wise (LC_ALL=C), one per line. A glob sorts by the locale and with the extension,
# which puts store-shot-ios-4-language-ja.yaml before store-shot-ios-4-language.yaml ('-' sorts before '.'); without the extension the Spanish shot comes before the Japanese one.
order_flows() {
  local file
  for file in "$@"; do
    echo "${file%.yaml}"
  done | LC_ALL=C sort | sed 's/$/.yaml/'
}

# Fills UNITS from the flows argument (a file, or a glob: unquoted expansion, ordered by order_flows) and sets the per-attempt limit.
load_units() {
  local found=() unit
  # shellcheck disable=SC2206
  found=( $FLOW_SPEC )
  UNITS=()
  while IFS= read -r unit; do
    [ -z "$unit" ] || UNITS+=("$unit")
  done < <(order_flows "${found[@]}")
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

# Run 45491086879 (finding 1): writes the approval of the app's URL scheme into the simulator's LaunchServices scheme-approval plist, so iOS never asks "Open in <app>?" for a dino-test:// link
# (Maestro cannot tap that alert in landscape, issue 2065). Source: Maestro issue 940, the PlistBuddy line of a commenter, which names the file
# <device data>/Library/Preferences/com.apple.launchservices.schemeapproval.plist and the key "<bridge>--><scheme>" with the app's bundle id as its string value.
# The simulator is shut down while the file is written and booted again afterwards (the thread does not say whether a booted simulator re-reads it). Prints the written entry; returns 1 when it did not read back.
approve_url_scheme() {
  local udid="$1" bundle_id="$2" plist key written
  [ "$APP" = dino ] || return 0
  SCHEME_APPROVED=no
  plist="$HOME/Library/Developer/CoreSimulator/Devices/$udid/data/Library/Preferences/com.apple.launchservices.schemeapproval.plist"
  key="$SCHEME_APPROVAL_BRIDGE-->$APPROVED_URL_SCHEME"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl shutdown "$udid" >/dev/null 2>&1 || true
  mkdir -p "$(dirname "$plist")"
  /usr/libexec/PlistBuddy -c "Delete :$key" "$plist" >/dev/null 2>&1 </dev/null || true
  /usr/libexec/PlistBuddy -c "Add :$key string $bundle_id" "$plist" </dev/null || true
  written="$(/usr/libexec/PlistBuddy -c "Print :$key" "$plist" 2>/dev/null </dev/null || true)"
  echo "scheme approval in $plist:"
  /usr/libexec/PlistBuddy -c "Print" "$plist" 2>&1 </dev/null || true
  if ! run_limited "$RESTART_TIME_LIMIT_SECONDS" "$udid" restart_simulator "$udid"; then
    echo "::error::the simulator $udid did not boot again after the URL scheme approval was written"
    return 1
  fi
  if [ "$written" != "$bundle_id" ]; then
    echo "::error::the URL scheme approval did not take: $key reads '$written', expected '$bundle_id' ($plist)"
    return 1
  fi
  SCHEME_APPROVED=yes
  echo "URL scheme $APPROVED_URL_SCHEME:// is approved for $bundle_id on $udid"
  return 0
}

# A copy of one flow whose appId is the bundle id; nothing on iOS reads the expect-log markers.
prepare_flow() {
  local source="$1" bundle_id="$2" target="$3"
  sed -e "s/^appId: .*/appId: $bundle_id/" -e "/^# expect-log:/d" "$source" > "$target"
  # A recorded clip flow (store-shot-ios-video-*) never rotates: its setup flow did, and a rotation inside the recording would be footage.
  if [ "$LANDSCAPE" = yes ] && [[ "$(basename "$source")" != store-shot-ios-video-* ]]; then
    # Upright first, then sideways: the simulator keeps its rotation between attempts, and the one transition that worked in run 45466463555 (shot 1, attempt 1) was upright to sideways before the launch.
    awk '{ print } !done && /^---$/ { print "- setOrientation: PORTRAIT"; print "- setOrientation: LANDSCAPE_LEFT"; done = 1 }' "$target" > "$target.land" && mv "$target.land" "$target"
  fi
  # Run 45512162060: the iPhone Spanish clip's setup hit its 240 s limit; three optional 30 s waits for an "Open in" alert that the approved
  # URL scheme no longer lets appear took about 32 s each. Once the approval is read back (SCHEME_APPROVED), each such wait in the copy is 8 s
  # (review pass 2: the read-back does not prove iOS honours the approval, so a late alert still gets 8 s; that run's iPhone clips met no alert in 6 waits).
  if [ "$SCHEME_APPROVED" = yes ]; then
    awk 'open_in && /^ *timeout: 30000$/ { sub(/30000/, "8000") } { open_in = ($0 ~ /visible: "\.\*Open in\.\*"/); print }' "$target" > "$target.fast" && mv "$target.fast" "$target"
  fi
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

# iOS run 45502209923: on the iPad in landscape the app is laid out landscape (app log "[Landscape] reading laid out landscape=True") but
# Maestro's takeScreenshot writes the simulator's portrait frame buffer, 2064 x 2752, so every shot failed the 2752 x 2064 check. A portrait
# PNG whose swapped size is accepted is turned upright: LANDSCAPE_LEFT puts the home side on the right, so the picture's top is the buffer's
# right edge and a quarter turn anticlockwise (sips -r 270, sips turns clockwise) stands it up. The direction is reasoned, not yet seen: the
# coordinator looks at one turned shot before any store upload. Only while LANDSCAPE=yes, and only when the app's own last [Landscape] log line
# says it was laid out landscape (review: a shot whose rotation never applied must fail its size check, never be turned into a sideways pass).
upright_landscape_shot() {
  local png="$1" accepted="$2" udid="$3" bundle_id="$4" width height
  [ "$LANDSCAPE" = "yes" ] || return 0
  width="$(sips -g pixelWidth "$png" | awk '/pixelWidth/ {print $2}')"
  height="$(sips -g pixelHeight "$png" | awk '/pixelHeight/ {print $2}')"
  [ "$width" -lt "$height" ] || return 0
  case " $accepted " in
    *" ${height}x${width} "*) ;;
    *) return 0 ;;
  esac
  if ! app_laid_out_landscape "$udid" "$bundle_id"; then
    echo "::warning::$(basename "$png") is ${width}x${height} and the app's log does not say it was laid out landscape; not turned"
    return 0
  fi
  if ! sips -r 270 "$png" >/dev/null; then
    echo "::warning::sips could not turn $(basename "$png"); left at ${width}x${height}"
    return 0
  fi
  echo "turned $(basename "$png") from ${width}x${height} to ${height}x${width} (portrait frame buffer of a landscape shot)"
}

# Lines in the app's test-hook log before this attempt's flow (run_flow sets HOOK_LOG_LINES_BEFORE), so only this attempt's lines are read.
HOOK_LOG_LINES_BEFORE=0
hook_log_line_count() {
  local container
  container="$(xcrun simctl get_app_container "$1" "$2" data 2>/dev/null)" || { echo 0; return 0; }
  if [ -f "$container/Library/test-hook.log" ]; then wc -l < "$container/Library/test-hook.log" | tr -d ' '; else echo 0; fi
}

# 0 when the newest [Landscape] line THIS attempt wrote says landscape: reading "landscape=True" (LandscapeLayout.IsLandscape), or a shelf on
# the rail that is neither tablet-portrait form (GalleryPage logs tabletUpright and tabletCorner = IsTabletPortrait for a child or grown-up
# shelf; CornerLayout gives an upright tablet the rail too, so the rail alone, or tabletUpright=False alone, proves nothing: review pass 2).
app_laid_out_landscape() {
  local udid="$1" bundle_id="$2" container last
  container="$(xcrun simctl get_app_container "$udid" "$bundle_id" data 2>/dev/null)" || return 1
  last="$(tail -n +"$((HOOK_LOG_LINES_BEFORE + 1))" "$container/Library/test-hook.log" 2>/dev/null | grep -F '[Landscape]' | grep -E 'landscape=(True|False)|layout=(rail|grid)' | tail -n 1)"
  case "$last" in
    *landscape=True*) return 0 ;;
    *layout=rail*tabletUpright=False*tabletCorner=False*) return 0 ;;
    *) return 1 ;;
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
    upright_landscape_shot "$target" "$accepted" "$udid" "$bundle_id" # udid and bundle_id: run_unit_body's locals, in scope here
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
  local label="$1" png expected name found vunit candidates=("$OUT/$label"/*.png)
  expected="$(while IFS= read -r name; do [ -n "$name" ] && basename "$(shot_target "$label" "$name")"; done < <(all_shots))"
  # Slice 433 (review pass 3, finding 4): the footage recordings of the current clips are expected too; an old recording (an earlier clip layout) is pruned like a stale PNG.
  if [ "$VIDEO_COUNT" -gt 0 ] && video_device "$label"; then
    for vunit in "${VIDEO_UNITS[@]}"; do
      expected="$expected"$'\n'"$(basename "$(video_target "$label" "$vunit")")"
    done
    candidates+=("$OUT/$label"/*.mp4)
  fi
  for png in "${candidates[@]}"; do
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

# Slice 433 (run 45466463555: the iPad's landscape flows failed with no picture of the screen): after a failed attempt, the pixel size of the simulator's own screenshot
# (2752x2064 = the screen is sideways, 2064x2752 = upright) and the tail of Maestro's own log, which names the element it could not find. Never fails the script.
print_failure_evidence() {
  local udid="$1" test_out="$2" shot="$RUNNER_TEMP/failure-evidence-$$.png" log
  echo "::group::failure evidence"
  if run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl io "$udid" screenshot "$shot" >/dev/null 2>&1; then
    echo "simulator screen: $(sips -g pixelWidth -g pixelHeight "$shot" | awk '/pixel/ {printf "%s ", $2}')"
    rm -f "$shot"
  else
    echo "simctl screenshot failed or hung"
  fi
  log="$(find "$test_out" -name 'maestro.log' -print -quit 2>/dev/null)"
  if [ -n "$log" ]; then tail -n 40 "$log"; else echo "no maestro.log under $test_out"; fi
  echo "::endgroup::"
  return 0
}

# Runs one flow on a booted simulator under the time limit; prints Maestro's hierarchy when it fails (not after a kill: the driver is gone).
# Returns 0 on success, else the status; LIMIT_HIT=yes when it was killed at the limit.
run_flow() {
  local udid="$1" flow_run="$2" test_out="$3" bundle_id="$4" rc=0 killed
  HOOK_LOG_LINES_BEFORE="$(hook_log_line_count "$udid" "$bundle_id")"
  run_limited "$UNIT_LIMIT" "$udid" maestro_test "$udid" "$flow_run" "$test_out" || rc=$?
  killed="$LIMIT_HIT"
  if [ "$killed" = "yes" ]; then
    echo "::warning::the flow ran longer than $UNIT_LIMIT s on $udid and was killed"
  elif [ "$rc" != "0" ]; then
    echo "::group::maestro hierarchy"
    run_limited "$HIERARCHY_TIME_LIMIT_SECONDS" "$udid" maestro --device "$udid" hierarchy || true
    echo "::endgroup::"
    print_failure_evidence "$udid" "$test_out"
  fi
  # Time-limited like the hierarchy dump: simctl can hang on a simulator whose run loop hung (review pass 1, finding 2).
  [ "$rc" = "0" ] || run_limited "$HIERARCHY_TIME_LIMIT_SECONDS" "$udid" print_hook_log "$udid" "$bundle_id" || true
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

# Slice 433 (finding 2): runs the pre-flow on the simulator (Dino only) so a left-over "Open in" alert is answered before the attempt's own flow starts. Never fails the attempt.
answer_open_in_prompt() {
  local udid="$1" bundle_id="$2" pre_run="$RUNNER_TEMP/store-preflight-$1.yaml" rc=0
  [ "$APP" = dino ] || return 0
  [ "$SCHEME_APPROVED" = yes ] && return 0
  sed -e "s/^appId: .*/appId: $bundle_id/" "$PREFLIGHT_FLOW" > "$pre_run"
  run_limited "$PREFLIGHT_TIME_LIMIT_SECONDS" "$udid" maestro_test "$udid" "$pre_run" "$RUNNER_TEMP/maestro-out-preflight-$udid" || rc=$?
  [ "$rc" = "0" ] || echo "::warning::the Open-in pre-flow ended with status $rc on $udid; the attempt goes on"
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
  UNIT_KEPT=no
  if unit_complete "$label" "$accepted" "$unit"; then
    UNIT_KEPT=yes
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
    if ! time_left_for $((PREFLIGHT_TIME_LIMIT_SECONDS + UNIT_LIMIT + HIERARCHY_TIME_LIMIT_SECONDS)); then
      problem "$label: $unit_name: no time left for attempt $attempt (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
      return 1
    fi
    answer_open_in_prompt "$udid" "$bundle_id"
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
      if ! time_left_for $((RESTART_TIME_LIMIT_SECONDS + PREFLIGHT_TIME_LIMIT_SECONDS + UNIT_LIMIT + HIERARCHY_TIME_LIMIT_SECONDS)); then
        problem "$label: $unit_name: no time left for the retry (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
        return 1
      fi
      # Slice 433 (finding 2): after ANY failed attempt the simulator is shut down and booted again, as after a kill (a failure can leave an Open-in alert or a rotation that no later launchApp clears);
      # the next attempt's flow copy sets the orientation again (prepare_flow) after the pre-flow has answered a prompt.
      if ! reset_simulator "$udid" "$bundle_id" yes; then
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
  # The hook goes as a link (dino-test://full-shelf) or as a relaunch's launch argument (cmd: full-shelf, shot 1 since run 44449072620).
  if grep -qsE 'dino-test://full-shelf|cmd: full-shelf' "$5"; then
    UNIT_LIMIT="$FULL_SHELF_UNIT_TIME_LIMIT_SECONDS"
  fi
  run_unit_body "$@" || rc=$?
  # iOS run 45502209923: shot 1 (the full-shelf fill) was kept from the resumed run, so the fresh simulator kept its 4-book shelf
  # ("shelf total=4") and shots 2-4 failed waiting for a paid book. A kept full-shelf shot still fills the shelf, by the shelf set-up flow.
  if [ "$rc" -eq 0 ] && [ "$UNIT_KEPT" = "yes" ] && [ "$UNIT_LIMIT" != "$base_limit" ]; then
    note "- $1: $(basename "$5" .yaml) was kept, so the shelf is filled by store-shot-ios-shelfprep instead"
    run_unit_body "$1" "$2" "$3" "$4" "$(dirname "$5")/store-shot-ios-shelfprep.yaml" "$6-shelf" || rc=$?
    [ "$rc" -eq 0 ] || stop_when_shelfprep_failed "$1" "$(dirname "$5")/store-shot-ios-shelfprep.yaml"
  fi
  UNIT_LIMIT="$base_limit"
  return "$rc"
}

# Slice 433 (finding 3): the shelf set-up flow failing twice leaves a shelf of 4 books (iOS run 45479054864, iPad); the rest of the device (its shots, its clips) is stopped with one clear error line instead of failing each on the wrong shelf.
stop_when_shelfprep_failed() {
  local label="$1" unit="$2"
  [ "$(basename "$unit")" = "store-shot-ios-shelfprep.yaml" ] || return 0
  SHELF_PREP_FAILED=yes
  echo "::error::$label: store-shot-ios-shelfprep failed; no clip or shot is taken on $label because the shelf is not the full 12-book shelf"
}

fail_fast_reset() {
  UNITS_OK=0
  UNITS_FAILED=0
  UNITS_FAILED_IN_A_ROW=0
  FAIL_FAST_HIT=no
}

# Counts one finished unit (its return code in $2) and stops the device once the first FAIL_FAST_AFTER attempted units all failed,
# or FAIL_FAST_AFTER + 1 failed in a row after an earlier success.
fail_fast_count() {
  local label="$1" rc="$2"
  [ "$UNIT_KEPT" = "yes" ] && return 0
  if [ "$rc" -eq 0 ]; then
    UNITS_OK=$((UNITS_OK + 1))
    UNITS_FAILED_IN_A_ROW=0
    return 0
  fi
  UNITS_FAILED=$((UNITS_FAILED + 1))
  UNITS_FAILED_IN_A_ROW=$((UNITS_FAILED_IN_A_ROW + 1))
  [ "$FAIL_FAST_HIT" = "no" ] || return 0
  if [ "$UNITS_OK" -eq 0 ] && [ "$UNITS_FAILED" -ge "$FAIL_FAST_AFTER" ]; then
    FAIL_FAST_HIT=yes
    echo "::error::$label: the first $UNITS_FAILED shots or clips attempted all failed (or found no time left) and none came out; the rest of $label is stopped (fail fast) instead of using the whole budget. Fix the cause in the log above, then rerun."
  elif [ "$UNITS_FAILED_IN_A_ROW" -gt "$FAIL_FAST_AFTER" ]; then
    FAIL_FAST_HIT=yes
    echo "::error::$label: $UNITS_FAILED_IN_A_ROW shots or clips in a row failed (or found no time left) after $UNITS_OK came out; the rest of $label is stopped (fail fast). Fix the cause in the log above, then rerun."
  fi
}

# Prints why a unit is skipped and returns 0 when the device is stopped (simulator gone, wrong shelf, fail fast).
unit_skipped() {
  local label="$1" name="$2"
  if [ "$SIM_BROKEN" = "yes" ]; then
    problem "$label: $name skipped, the simulator is not usable"
  elif [ "$SHELF_PREP_FAILED" = "yes" ]; then
    problem "$label: $name skipped, store-shot-ios-shelfprep failed so the shelf is not the full one"
  elif [ "$FAIL_FAST_HIT" = "yes" ]; then
    problem "$label: $name skipped, fail fast (see the fail-fast error above)"
  else
    return 1
  fi
}

# Every unit in order on the booted simulator; one that failed does not stop the next, unless the first ones all failed (fail fast) or the simulator did not come back.
run_units() {
  local label="$1" accepted="$2" udid="$3" bundle_id="$4" unit index=0 rc
  SIM_BROKEN=no
  SHELF_PREP_FAILED=no
  fail_fast_reset
  for unit in "${UNITS[@]}"; do
    index=$((index + 1))
    unit_skipped "$label" "$(basename "$unit" .yaml)" && continue
    rc=0
    run_unit "$label" "$accepted" "$udid" "$bundle_id" "$unit" "$index" || rc=$?
    [ "$rc" -eq 0 ] || stop_when_shelfprep_failed "$label" "$unit"
    fail_fast_count "$label" "$rc"
  done
}

# Slice 433 item 6: fills VIDEO_UNITS (and VIDEO_COUNT) from the VIDEO_FLOWS glob; empty when the variable is empty or matches nothing.
VIDEO_COUNT=0
load_video_units() {
  local unit found=()
  VIDEO_UNITS=()
  VIDEO_COUNT=0
  [ -n "$VIDEO_FLOWS" ] || return 0
  # shellcheck disable=SC2206
  found=( $VIDEO_FLOWS )
  while IFS= read -r unit; do
    if [ -f "$unit" ]; then
      VIDEO_UNITS+=("$unit")
      VIDEO_COUNT=$((VIDEO_COUNT + 1))
    fi
  done < <(order_flows "${found[@]}")
}

# Apple's preview sizes come from the iPhone 6.9" (886 x 1920) and the iPad 13" (1600 x 1200, landscape) only.
video_device() {
  case "$1" in
    iphone-6.9 | ipad-13) return 0 ;;
    *) return 1 ;;
  esac
}

# The path a footage flow's recording gets: store-shot-ios-video-<n>-<screen> becomes <out>/<label>/store-<app>-<label>-video-<n>-<screen>.mp4.
video_target() {
  local label="$1" name rest
  name="$(basename "$2" .yaml)"
  rest="${name#store-shot-ios-video-}"
  echo "$OUT/$label/store-${APP}-${label}-video-${rest}.mp4"
}

# Slice 433 (review pass 3, finding 3): footage-cuts.json holds ONE leadIn (the recorder's start-up, on a line of its own) and each clip's seconds; a recording must last leadIn + seconds.
# Prints that minimum for a clip id (e.g. 3-language-es), or nothing when the file or the clip is not found.
clip_min_seconds() {
  local cuts="$REPO_ROOT/tools/dino/promo/store/footage-cuts.json" lead secs
  [ -f "$cuts" ] || return 0
  lead="$(sed -n 's/^ *"leadIn": *\([0-9.]*\),*$/\1/p' "$cuts" | head -1)"
  secs="$(sed -n "s/^ *\"$1\": *{ *\"seconds\": *\\([0-9.]*\\) *}.*/\\1/p" "$cuts" | head -1)"
  [ -n "$lead" ] && [ -n "$secs" ] || return 0
  awk -v a="$lead" -v b="$secs" 'BEGIN { print a + b }'
}

# The clip id of a footage flow: store-shot-ios-video-<clip>.yaml gives <clip>.
video_clip_id() {
  local name
  name="$(basename "$1" .yaml)"
  echo "${name#store-shot-ios-video-}"
}

# Slice 433 (review pass 2, finding 2; pass 3, finding 3): a recording counts only when it can be read to the end and is long enough. ffprobe (when the runner has it) reads every video packet, which fails on an mp4 whose moov atom was never written,
# and its duration must reach the optional second argument (leadIn + seconds: simctl writes frames only when the screen changes, so a held static clip can end early); without ffprobe the file must at least hold a moov atom.
recording_readable() {
  local file="$1" min_seconds="${2:-}" packets duration
  [ -s "$file" ] || return 1
  if command -v ffprobe >/dev/null 2>&1; then
    packets="$(ffprobe -v error -count_packets -select_streams v:0 -show_entries stream=nb_read_packets -of csv=p=0 "$file" 2>/dev/null)" || return 1
    [ "${packets:-0}" -gt 0 ] 2>/dev/null || return 1
    [ -n "$min_seconds" ] || return 0
    duration="$(ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 "$file" 2>/dev/null)" || return 1
    awk -v d="${duration:-0}" -v m="$min_seconds" 'BEGIN { exit !(d + 0 >= m - 0.1) }'
    return
  fi
  LC_ALL=C grep -q moov "$file"
}

# A clip's recording is readable and at least as long as its cut needs.
clip_recording_ok() {
  recording_readable "$1" "$(clip_min_seconds "$(video_clip_id "$2")")"
}

# Slice 433 (review pass 3, finding 2): the recorder must be alive and its file must be growing within RECORDER_START_CHECK_SECONDS of starting; its stderr is kept in $3 and printed when it fails.
recorder_started() {
  local rec_pid="$1" video_tmp="$2" rec_log="$3" waited=0
  while [ "$waited" -lt "$RECORDER_START_CHECK_SECONDS" ]; do
    sleep 1
    waited=$((waited + 1))
    if ! kill -0 "$rec_pid" 2>/dev/null; then
      echo "::warning::the screen recorder exited within $waited s of starting; its output:"
      cat "$rec_log" 2>/dev/null || true
      return 1
    fi
    # Alive plus either bytes in the file or simctl's "Recording started" line in its stderr log (that exact text is unconfirmed by any doc; either one passes).
    if [ -s "$video_tmp" ] || grep -q "Recording started" "$rec_log" 2>/dev/null; then
      return 0
    fi
  done
  echo "::warning::the screen recorder is running but wrote nothing to its file in $RECORDER_START_CHECK_SECONDS s; its output:"
  cat "$rec_log" 2>/dev/null || true
  return 1
}

# Stops the recorder with SIGINT (simctl then finishes the file) and waits up to RECORDER_FINISH_SECONDS for it; only after that wait is it killed, and a killed recorder is a failed attempt (RECORDER_KILLED=yes), never a file to keep.
stop_recorder() {
  local rec_pid="$1" waited=0
  RECORDER_KILLED=no
  kill -INT "$rec_pid" 2>/dev/null || true
  while kill -0 "$rec_pid" 2>/dev/null && [ "$waited" -lt "$RECORDER_FINISH_SECONDS" ]; do
    sleep 1
    waited=$((waited + 1))
  done
  if kill -0 "$rec_pid" 2>/dev/null; then
    echo "::warning::the screen recorder did not finish within $RECORDER_FINISH_SECONDS s after SIGINT; it is killed and this attempt is retaken (its file is unusable)"
    RECORDER_KILLED=yes
    kill -9 "$rec_pid" 2>/dev/null || true
  fi
  wait "$rec_pid" 2>/dev/null || true
  echo "recorder stopped after $waited s"
}

# One clip attempt: the unrecorded setup flow runs first; then the simulator screen is recorded (h264) while the clip flow runs alone, and the recorder is stopped, so the file holds exactly that clip.
# Returns the failing flow's status (1 when the recorder had to be killed); LIMIT_HIT says whether a flow was killed at its limit.
record_unit_attempt() {
  local udid="$1" prep_run="$2" flow_run="$3" test_out="$4" bundle_id="$5" video_tmp="$6" rec_pid rec_log rc=0 killed
  rm -f "$video_tmp"
  rm -rf "$test_out-setup"
  mkdir -p "$test_out-setup"
  run_flow "$udid" "$prep_run" "$test_out-setup" "$bundle_id" || rc=$?
  killed="$LIMIT_HIT"
  if [ "$rc" != "0" ]; then
    echo "::warning::the setup flow $(basename "$prep_run") failed (status $rc); nothing is recorded"
    LIMIT_HIT="$killed"
    return "$rc"
  fi
  rec_log="$test_out-recorder.log"
  xcrun simctl io "$udid" recordVideo --codec=h264 --force "$video_tmp" >"$rec_log" 2>&1 &
  rec_pid=$!
  if ! recorder_started "$rec_pid" "$video_tmp" "$rec_log"; then
    stop_recorder "$rec_pid"
    LIMIT_HIT=no
    return 1
  fi
  run_flow "$udid" "$flow_run" "$test_out" "$bundle_id" || rc=$?
  killed="$LIMIT_HIT"
  stop_recorder "$rec_pid"
  [ "$RECORDER_KILLED" = "no" ] || [ "$rc" != "0" ] || rc=1
  echo "clip attempt of $(basename "$flow_run"): flow status $rc, file $(wc -c < "$video_tmp" 2>/dev/null | tr -d ' ') bytes"
  LIMIT_HIT="$killed"
  return "$rc"
}

# One footage flow: kept when its file is already in the out folder, else recorded, and once more on a failure (the same one retry as a shot).
run_video_unit() {
  local label="$1" udid="$2" bundle_id="$3" unit="$4" index="$5" target flow_run prep_unit prep_run test_out video_tmp attempt=1 rc killed base_limit="$UNIT_LIMIT" unit_name
  unit_name="$(basename "$unit" .yaml)"
  target="$(video_target "$label" "$unit")"
  prep_unit="$(dirname "$unit")/$(basename "$unit" | sed 's/^store-shot-ios-video-/store-shot-ios-videoprep-/')"
  UNIT_KEPT=no
  if clip_recording_ok "$target" "$unit"; then
    UNIT_KEPT=yes
    note "- $label: $(basename "$target") kept (already recorded, readable and long enough, not retaken)"
    return 0
  fi
  rm -f "$target"
  mkdir -p "$OUT/$label"
  flow_run="$RUNNER_TEMP/store-video-$label-$index.yaml"
  prep_run="$RUNNER_TEMP/store-videoprep-$label-$index.yaml"
  test_out="$RUNNER_TEMP/maestro-out-video-$label-$index"
  video_tmp="$RUNNER_TEMP/store-video-$label-$index.mp4"
  if [ ! -f "$prep_unit" ]; then
    problem "$label: $unit_name: no setup flow $(basename "$prep_unit")"
    return 1
  fi
  if ! prepare_flow "$unit" "$bundle_id" "$flow_run" || ! prepare_flow "$prep_unit" "$bundle_id" "$prep_run"; then
    problem "$label: $unit_name: the appId line was not swapped for $bundle_id"
    return 1
  fi
  UNIT_LIMIT="$VIDEO_TIME_LIMIT_SECONDS"
  while [ "$attempt" -le "$FLOW_ATTEMPTS" ]; do
    if ! time_left_for $((PREFLIGHT_TIME_LIMIT_SECONDS + 2 * UNIT_LIMIT + 2 * HIERARCHY_TIME_LIMIT_SECONDS + RECORDER_FINISH_SECONDS + 10)); then
      problem "$label: $unit_name: no time left for attempt $attempt (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
      UNIT_LIMIT="$base_limit"
      return 1
    fi
    answer_open_in_prompt "$udid" "$bundle_id"
    reset_attempt "$unit" "$test_out"
    rc=0
    record_unit_attempt "$udid" "$prep_run" "$flow_run" "$test_out" "$bundle_id" "$video_tmp" || rc=$?
    killed="$LIMIT_HIT"
    if [ "$rc" = "0" ] && clip_recording_ok "$video_tmp" "$unit"; then
      cp "$video_tmp" "$target"
      note "- $label: $(basename "$target") recorded ($(wc -c < "$target" | tr -d ' ') bytes)"
      upload_shot "$target"
      UNIT_LIMIT="$base_limit"
      return 0
    fi
    [ "$rc" != "0" ] || { rc=1; killed=no; echo "::warning::$label: $unit_name exited 0 but left no recording that can be read to the end and is long enough (leadIn + seconds in footage-cuts.json)"; rm -f "$video_tmp"; }
    if [ "$attempt" -lt "$FLOW_ATTEMPTS" ]; then
      note "- RETRY: $label $unit_name attempt $attempt failed (status $rc, killed at the $UNIT_LIMIT s limit: $killed), it is recorded once more"
      if ! time_left_for $((RESTART_TIME_LIMIT_SECONDS + PREFLIGHT_TIME_LIMIT_SECONDS + 2 * UNIT_LIMIT + 2 * HIERARCHY_TIME_LIMIT_SECONDS + RECORDER_FINISH_SECONDS + 10)); then
        problem "$label: $unit_name: no time left for the retry (script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
        UNIT_LIMIT="$base_limit"
        return 1
      fi
      # Slice 433 (finding 2): a fresh boot after every failed clip attempt, as after a kill (the setup flow turns the iPad again).
      if ! reset_simulator "$udid" "$bundle_id" yes; then
        SIM_BROKEN=yes
        problem "$label: the simulator did not come back within $RESTART_TIME_LIMIT_SECONDS s after $unit_name; the rest of this device is skipped"
        UNIT_LIMIT="$base_limit"
        return 1
      fi
    fi
    attempt=$((attempt + 1))
  done
  UNIT_LIMIT="$base_limit"
  problem "$label: $unit_name failed twice (killed at the $VIDEO_TIME_LIMIT_SECONDS s limit, or Maestro failed; the log above says which)"
  return 1
}

# Every footage flow in order after the shots, on the devices Apple's previews come from; one that failed does not stop the next.
run_video_units() {
  local label="$1" udid="$2" bundle_id="$3" unit index=0 rc
  [ "$VIDEO_COUNT" -gt 0 ] || return 0
  video_device "$label" || return 0
  if [ "$SHELF_PREP_FAILED" = "yes" ]; then
    problem "$label: the $VIDEO_COUNT clip(s) are not recorded, store-shot-ios-shelfprep failed so the shelf is not the full one"
    return 0
  fi
  [ "$FAIL_FAST_HIT" = "yes" ] || fail_fast_reset
  for unit in "${VIDEO_UNITS[@]}"; do
    index=$((index + 1))
    unit_skipped "$label" "$(basename "$unit" .yaml)" && continue
    rc=0
    run_video_unit "$label" "$udid" "$bundle_id" "$unit" "$index" || rc=$?
    fail_fast_count "$label" "$rc"
  done
}

# Slice 404: the Dino full-shelf hook imports a PAID book from a package file (the store refuses a paid download without a real receipt; nothing is faked).
# Decision 5: a package is never built here. The paid books of the shelf (tools/dino/full-shelf-books.json) are read from the downloads bucket, where the store upload already put
# dinoreader/stories/<id>/story-v<n>.dino, by .github/scripts/fetch-shelf-packages.mjs into artifacts/full-shelf/<id>/story-v<n>.dino (CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID, wrangler from `npm ci`);
# the packages are copied into <app data container>/Library/full-shelf/<id>.dino (the hook's FullShelfPlan.PackageFolder inside FileSystem.AppDataDirectory, which is the container's Library folder on iOS).
# A flow that never calls the hook needs none of it.
REPO_ROOT="$(cd "$HERE/../.." && pwd)"
SHELF_ROOT="${SHELF_ROOT:-$REPO_ROOT/artifacts/full-shelf}" # the workflow's fetch step sets SHELF_ROOT and SHELF_FETCHED=yes (the only step with the Cloudflare secrets)

flows_use_full_shelf() {
  [ "$APP" = dino ] && grep -qs 'full-shelf' "${UNITS[@]}"
}

# Fetches the stored packages once per run (every device copies the same files). Returns 1, naming why, when the bucket cannot be read.
fetch_shelf_packages() {
  [ "${SHELF_FETCHED:-no}" = yes ] && return 0
  if ! command -v node >/dev/null 2>&1; then
    echo "::error::node is not on the PATH to fetch the stored book packages (node .github/scripts/fetch-shelf-packages.mjs artifacts/full-shelf)"
    return 1
  fi
  (cd "$REPO_ROOT" && node .github/scripts/fetch-shelf-packages.mjs "$SHELF_ROOT") >&2 || return 1
  SHELF_FETCHED=yes
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
  for dir in "$SHELF_ROOT"/*/; do
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
  if ! fetch_shelf_packages; then
    problem "$label: the stored book packages for the full-shelf hook could not be fetched"
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
  SCHEME_APPROVED=no # per device: approve_url_scheme sets it again only once this device's approval reads back
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
  elif ! approve_url_scheme "$udid" "$bundle_id"; then
    problem "$label: the URL scheme $APPROVED_URL_SCHEME:// could not be approved for $bundle_id; no shot is taken without it (the iOS \"Open in\" alert cannot be tapped in landscape)"
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
    run_video_units "$label" "$udid" "$bundle_id"
  fi
  kill_driver "$udid"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl shutdown "$udid" || true
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl delete "$udid" || true
}

# One summary line per device: which shots it produced, then OK or failed (the FAILED lines above say why).
device_verdict() {
  local label="$1" problems_before="$2" accepted="$3" name target made="" missing="" total=0 got=0 vunit vtotal=0 vgot=0
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
  if [ "$VIDEO_COUNT" -gt 0 ] && video_device "$label"; then
    for vunit in "${VIDEO_UNITS[@]}"; do
      vtotal=$((vtotal + 1))
      if clip_recording_ok "$(video_target "$label" "$vunit")" "$vunit"; then vgot=$((vgot + 1)); fi
    done
    note "Footage $label produced ($vgot of $vtotal)"
    if [ -n "$EXPECTED_VIDEOS" ] && { [ "$vtotal" -lt "$EXPECTED_VIDEOS" ] || [ "$vgot" -lt "$EXPECTED_VIDEOS" ]; }; then
      problem "$label: $vgot preview recording(s), $EXPECTED_VIDEOS expected for this flow set ($vtotal named by the flows)"
    fi
  fi
  if [ "$PROBLEMS" -eq "$problems_before" ] && [ "$got" -eq "$total" ] && [ "$vgot" -eq "$vtotal" ]; then
    note "**RESULT $label: OK, all $total shots at an App Store size, $vgot of $vtotal clips.**"
  else
    note "**RESULT $label: FAILED, $got of $total shots (see the FAILED lines above).**"
    [ -z "$missing" ] || note "Missing on $label:$missing. Resume with: gh run rerun ${GITHUB_RUN_ID:-<run id>} --failed (it retakes only the missing shots)."
    FAILED=1
  fi
}

RUNNER_TEMP="${RUNNER_TEMP:-$(mktemp -d)}"
mkdir -p "$OUT"
load_units
load_video_units
MATCHED=0
# The specs come in on fd 3: maestro and xcrun read stdin and would swallow the remaining lines.
while IFS='|' read -r label patterns accepted <&3; do
  if [ "$DEVICE" != "all" ] && [ "$DEVICE" != "$label" ]; then
    continue
  fi
  MATCHED=1
  if [ "$MEDIA" = "clips" ] && ! video_device "$label"; then
    note "### $APP on $label: no preview clips are recorded on this device, nothing to do"
    continue
  fi
  LANDSCAPE=no
  if [ "$label" = "ipad-13" ] && [ "$APP" = dino ]; then LANDSCAPE=yes; fi
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
