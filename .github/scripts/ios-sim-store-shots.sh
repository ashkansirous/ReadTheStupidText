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
# VIDEO_FLOWS (glob, optional; Dino) = the App Store preview clip flows (.maestro/dino/store-shot-ios-video-<clip>.yaml), one per clip. On the iPhone 6.9" and the iPad 13" every clip still missing runs in ONE
# Maestro invocation (the iOS driver starts once; iPad run 45582715957 lost about 60 s of driver start per clip to the recording): each clip is a generated flow = runFlow of its unrecorded setup flow
# (store-shot-ios-videoprep-<clip>.yaml), Maestro's own startRecording, the clip flow's steps, stopRecording. It is kept as <out-dir>/<device>/store-<app>-<device>-video-<clip>.mp4 plus
# store-<app>-<device>-video-<clip>.mark (mark and span from that flow's commands.json, tools/dino/promo/store/footage-cuts.json); a clip that does not pass `store-media.mjs check` (ffprobe required) counts as not
# recorded (EXPECTED_VIDEOS of them, else the device fails); the missing ones get one more invocation after a simulator restart. Apple's preview sizes need no 6.5" footage. tools/dino/promo/store/ trims and captions them.
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
# shots that came out. 3600 s = 60 min (Slice 433: it was 2700 s before the preview clips); the workflow's shots step is 66 minutes and its job 90 (about 6 minutes of setup before the step, run
# 43748250465: 3 min 20 s). The worst case of shots and clips does NOT fit one job (boot 300 + seven shots at two attempts of 420 s = 5880, plus the clips below), so media=all runs them as two jobs.
# Slice 433 (run 45479054864, device findings 2-4): every attempt now starts with the Open-in pre-flow (PREFLIGHT_TIME_LIMIT_SECONDS 20 s at most, and skipped on a device where the URL scheme is pre-approved, run 45491086879 finding 1) and every retry is preceded by a simulator restart (RESTART_TIME_LIMIT_SECONDS 300 s at most), and
# each "will it fit" test above counts them: a shot attempt is 20 + 420 + 60 = 500 s (shot 1, the full-shelf fill: 20 + 900 + 60 = 980 s), a clip round 20 + 330 + n x 150 + 2 x 60 + 10 s, a retry adds 300 s. No attempt starts unless its whole cost fits
# in the 3600 s, so the script ends within 3600 s = 60 min; the workflow's step (66 min) and job (90 min: 6 setup + 8 ffmpeg install (clips) + 66 step + 2 hand-over = 82) keep their margin.
# It is not meant to: every shot is uploaded when taken, so `gh run rerun <run id> --failed` resumes and retakes only what is missing. RTST:
# boot 300 + 2 x 600 + restart 300 + hierarchy 2 x 60 = 1920 s, which fits.
SCRIPT_BUDGET_SECONDS="${SCRIPT_BUDGET_SECONDS:-3600}"
# Slice 433: the preview footage flows and how many recordings a device must end with.
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
# Slice 433 (in-flow recording): all missing clips of a device run in one Maestro invocation whose limit is one driver start (CLIP_BATCH_DRIVER_SECONDS) plus
# VIDEO_TIME_LIMIT_SECONDS per clip (its setup and its recorded steps). Measured: 54 to 111 s per clip in one batch (iPhone run 45607850849 attempt 2; iPad run 45607845756
# about 95 s each), so 150 s per clip. Maestro 2.12.0 starts its iOS driver anew in every `maestro test` (--reinstall-driver defaults to true, LocalXCTestInstaller.kt) and waits
# MAESTRO_DRIVER_STARTUP_TIMEOUT for it; the iPhone 6.9" needed 214 s in a warm-up (run 45607850849 attempt 2) and its clip batch died at the 180 s default of this script
# (attempt 1, before any clip), so the clip batch waits up to CLIP_DRIVER_STARTUP_TIMEOUT_MS = 300 s, the warm-up's value, and the batch limit holds 330 s for it.
# With those numbers a round 2 after a round 1 that died on the driver start fits the budget (restart 300 + 20 + 330 + 7 x 150 + 130 = 1830 s; attempt 1 had about 2200 s left).
# Clips job worst case: boot 300 + shelf set-up 900 + round 1 (20 + 330 + 7 x 150 + 120 + 10 = 1530) + restart 300 + round 2 for what is left: round 2 starts only when it fits.
VIDEO_TIME_LIMIT_SECONDS=150
CLIP_BATCH_DRIVER_SECONDS=330
CLIP_DRIVER_STARTUP_TIMEOUT_MS=300000
# The driver start timeout of the next maestro_test (run_clip_batch raises it for the clip batch and sets it back).
RUN_DRIVER_STARTUP_TIMEOUT_MS="$DRIVER_STARTUP_TIMEOUT_MS"
# Slice 433 (run time): the simulator booted early by ios-sim-store-preboot.sh (workflow step "Boot the simulator early"): its state file, process id and log, and how long
# to wait for it (its boot took 4.7 to 11 min; it starts about 5 min before this script). Set by use_preboot: the pre-booted udid and the bundle id approved before its first boot.
PREBOOT_STATE="${PREBOOT_STATE:-}"
PREBOOT_PID="${PREBOOT_PID:-}"
PREBOOT_LOG="${PREBOOT_LOG:-}"
PREBOOT_WAIT_SECONDS=900
PREBOOT_UDID=""
PREBOOT_BUNDLE=""
# Set by run_clip_batch: the clip flows of the last round that did not come out.
CLIPS_MISSING=()
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

# device_specs and device_patterns (Slice 433: shared with ios-sim-store-preboot.sh, which boots the same simulator early).
# shellcheck source=ios-sim-store-devices.sh
. "$HERE/ios-sim-store-devices.sh"

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

# Why the store simulators get Reduce Motion and the URL scheme approval, and how both are written: .github/scripts/ios-sim-store-prefs.sh.
# Review pass 1 (finding 2): the file read-back cannot tell a wrong key, so the booted simulator's own preferences are read too. Returns 1 when it does not read 1.
reduce_motion_read_back() {
  local value
  value="$(xcrun simctl spawn "$1" defaults read com.apple.Accessibility ReduceMotionEnabled 2>&1 || true)"
  echo "Reduce Motion as the booted simulator $1 reads it: $value"
  [ "$value" = "1" ] && return 0
  echo "::warning::the booted simulator $1 does not read ReduceMotionEnabled = 1 ('$value'); animation loops may still block Maestro"
  return 1
}

# The bundle id the simulator's scheme-approval file holds for dino-test:// now (empty when none).
scheme_approval_entry() {
  local plist="$HOME/Library/Developer/CoreSimulator/Devices/$1/data/Library/Preferences/com.apple.launchservices.schemeapproval.plist"
  /usr/libexec/PlistBuddy -c "Print :$SCHEME_APPROVAL_BRIDGE-->$APPROVED_URL_SCHEME" "$plist" 2>/dev/null </dev/null || true
}

# Slice 433 (run time): true when this simulator was pre-booted with both values written before its first boot (ios-sim-store-preboot.sh), for this bundle id, and both
# still hold on the booted simulator: then no second boot is needed (it took 3 to 3.5 min per run).
preboot_prefs_kept() {
  local udid="$1" bundle_id="$2"
  [ -n "$PREBOOT_UDID" ] && [ "$PREBOOT_UDID" = "$udid" ] || return 1
  if [ "$PREBOOT_BUNDLE" != "$bundle_id" ]; then
    echo "the pre-boot approved $PREBOOT_BUNDLE, the installed app is $bundle_id"
    return 1
  fi
  if [ "$(scheme_approval_entry "$udid")" != "$bundle_id" ]; then
    echo "the URL scheme approval written before the first boot is not in the file after it"
    return 1
  fi
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" reduce_motion_read_back "$udid"
}

# Run 45491086879 (finding 1): the app's URL scheme approved (no "Open in <app>?" alert, which Maestro cannot tap in landscape, issue 2065) and Reduce Motion on, before any flow.
# A pre-booted simulator whose values held needs nothing more; otherwise the simulator is shut down, ios-sim-store-prefs.sh writes both and it is booted again
# (the Maestro thread does not say whether a booted simulator re-reads the file). Returns 1 when the approval does not read back.
approve_url_scheme() {
  local udid="$1" bundle_id="$2" written
  [ "$APP" = dino ] || return 0
  SCHEME_APPROVED=no
  if preboot_prefs_kept "$udid" "$bundle_id"; then
    SCHEME_APPROVED=yes
    echo "URL scheme $APPROVED_URL_SCHEME:// approved for $bundle_id and Reduce Motion on since the first boot of $udid (pre-boot): no second boot"
    return 0
  fi
  [ -z "$PREBOOT_UDID" ] || echo "the pre-boot values did not hold on $udid: written again with a second boot"
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl shutdown "$udid" >/dev/null 2>&1 || true
  bash "$HERE/ios-sim-store-prefs.sh" "$udid" "$bundle_id" || true
  if ! run_limited "$RESTART_TIME_LIMIT_SECONDS" "$udid" restart_simulator "$udid"; then
    echo "::error::the simulator $udid did not boot again after the URL scheme approval was written"
    return 1
  fi
  run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" reduce_motion_read_back "$udid" || true
  written="$(scheme_approval_entry "$udid")"
  if [ "$written" != "$bundle_id" ]; then
    echo "::error::the URL scheme approval did not take: it reads '$written', expected '$bundle_id'"
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
HOOK_LOG_FILE=""
# The app's test-hook log path on this simulator, or empty when the app has no data container yet.
hook_log_file() {
  local container
  container="$(xcrun simctl get_app_container "$1" "$2" data 2>/dev/null)" || return 0
  echo "$container/Library/test-hook.log"
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
      expected="$expected"$'\n'"$(basename "$(video_target "$label" "$vunit")")"$'\n'"$(basename "$(mark_file_of "$(video_target "$label" "$vunit")")")"
    done
    candidates+=("$OUT/$label"/*.mp4 "$OUT/$label"/*.mark)
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
  # $flow_run is one flow file, or (the preview clips) a folder of flows with its own config.yaml, run in one invocation.
  MAESTRO_DRIVER_STARTUP_TIMEOUT="$RUN_DRIVER_STARTUP_TIMEOUT_MS" maestro --device "$udid" test --test-output-dir "$test_out" "$flow_run"
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

# Slice 433 (runs 45531807696 and 45534156337: nothing said what the turn-page hook did in a passing clip): after a passing attempt, the
# HOOK lines this attempt wrote, so a hook that refused says why. Reads the file run_flow found before the flow (HOOK_LOG_FILE), with no
# simctl call, so it cannot hang and is not under the watchdog that kills the driver (review s433-hooklines-high #4). A log shorter than
# before (a clearState flow wiped it) is read whole (#1); a missing log or no HOOK line is said (#2). Never fails the script.
print_attempt_hook_lines() {
  local from=$((HOOK_LOG_LINES_BEFORE + 1)) lines
  # The first flow after an install or clearState starts with no data container, so the path found before the flow can be empty: look again.
  [ -n "$HOOK_LOG_FILE" ] || HOOK_LOG_FILE="$(hook_log_file "$1" "$2")"
  if [ -z "$HOOK_LOG_FILE" ] || [ ! -f "$HOOK_LOG_FILE" ]; then
    echo "app test-hook log: none at '${HOOK_LOG_FILE}'"
    return 0
  fi
  [ "$(wc -l < "$HOOK_LOG_FILE" | tr -d ' ')" -ge "$HOOK_LOG_LINES_BEFORE" ] || from=1
  lines="$(tail -n +"$from" "$HOOK_LOG_FILE" | grep -F 'HOOK' | tail -n 60)" || true
  echo "::group::app test-hook lines of this attempt"
  if [ -n "$lines" ]; then printf '%s\n' "$lines"; else echo "no HOOK line in this attempt"; fi
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
  HOOK_LOG_FILE="$(hook_log_file "$udid" "$bundle_id")"
  HOOK_LOG_LINES_BEFORE=0
  [ -f "$HOOK_LOG_FILE" ] && HOOK_LOG_LINES_BEFORE="$(wc -l < "$HOOK_LOG_FILE" | tr -d ' ')"
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
  if [ "$rc" = "0" ]; then
    print_attempt_hook_lines "$udid" "$bundle_id"
  else
    run_limited "$HIERARCHY_TIME_LIMIT_SECONDS" "$udid" print_hook_log "$udid" "$bundle_id" || true
  fi
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

# The mark file that belongs to a recording: store-...-video-<clip>.mp4 gives store-...-video-<clip>.mark (two lines: mark, span; tools/dino/promo/store/footage-cuts.json says what they are).
mark_file_of() {
  echo "${1%.mp4}.mark"
}

# Slice 433 (in-flow recording): the mark, the length check and the cut all live in one place, tools/dino/promo/store/store-media.mjs (node is set up for Dino by the workflow).
store_media() {
  node "$REPO_ROOT/tools/dino/promo/store/store-media.mjs" "$@"
}

# The clip id of a footage flow: store-shot-ios-video-<clip>.yaml gives <clip>.
video_clip_id() {
  local name
  name="$(basename "$1" .yaml)"
  echo "${name#store-shot-ios-video-}"
}

# Slice 433 (iPad run 45582715957: the runner had no ffprobe, so a 29 s file passed for a 67 s cut): a clip's recording counts only when `store-media.mjs check` passes:
# ffprobe (required; the workflow installs it) reads its video to the end, its mark file holds a mark and a span, and the recorder ran for the whole cut. Anything else is retaken.
clip_recording_ok() {
  [ -s "$1" ] && [ -s "$(mark_file_of "$1")" ] || return 1
  store_media check "$1" "$(video_clip_id "$2")"
}

# Slice 433 (in-flow recording): writes the generated flow of one clip, $3/clip-<clip>.yaml. Maestro records INSIDE the flow (startRecording runs simctl recordVideo), so the
# recording starts after the driver is up and after the unrecorded setup, and t = 0 is the end of the startRecording step in that flow's commands.json:
#   runFlow prep/prep-<clip>.yaml (store-shot-ios-videoprep-<clip>.yaml), startRecording clip-<clip>, the steps of store-shot-ios-video-<clip>.yaml, stopRecording.
write_clip_flow() {
  local unit="$1" bundle_id="$2" dir="$3" clip prep_unit
  clip="$(video_clip_id "$unit")"
  prep_unit="$(dirname "$unit")/store-shot-ios-videoprep-$clip.yaml"
  [ -f "$prep_unit" ] || { echo "::warning::no setup flow $(basename "$prep_unit")"; return 1; }
  prepare_flow "$prep_unit" "$bundle_id" "$dir/prep/prep-$clip.yaml" || return 1
  prepare_flow "$unit" "$bundle_id" "$dir/body/body-$clip.yaml" || return 1
  {
    printf 'appId: %s\nname: clip-%s\n---\n' "$bundle_id" "$clip"
    printf -- '- runFlow: prep/prep-%s.yaml\n- startRecording: clip-%s\n' "$clip" "$clip"
    sed '1,/^---$/d' "$dir/body/body-$clip.yaml"
    printf -- '- stopRecording\n'
  } > "$dir/clip-$clip.yaml"
}

# The folder one Maestro run gets: a generated flow per clip at the top, the setup and body copies in subfolders (only top-level flows run), and a config.yaml that
# runs the clips in order and goes on after a failed one.
write_clip_batch() {
  local bundle_id="$1" dir="$2" unit
  shift 2
  rm -rf "$dir"
  mkdir -p "$dir/prep" "$dir/body"
  printf 'executionOrder:\n  continueOnFailure: true\n  flowsOrder:\n' > "$dir/config.yaml"
  for unit in "$@"; do
    write_clip_flow "$unit" "$bundle_id" "$dir" || return 1
    printf '    - clip-%s\n' "$(video_clip_id "$unit")" >> "$dir/config.yaml"
  done
}

# The commands.json of one clip's flow: the one beside its startRecording folder (Maestro 2.12.0 layout), else any commands*.json under the run's output that names its
# recording path (the layout is read from Maestro's source, not yet seen on a run: review s433-inflow-high #6).
clip_commands_file() {
  local video="$1" test_out="$2" clip="$3" beside
  beside="$(dirname "$(dirname "$video")")/commands.json"
  if [ -f "$beside" ]; then echo "$beside"; return 0; fi
  find "$test_out" -name 'commands*.json' -exec grep -l "\"clip-$clip\"" {} + 2>/dev/null | head -1
}

# The mp4 of one clip under the run's output: startRecording/clip-<clip>.mp4 (Maestro 2.12.0 layout), else a clip-<clip>.mp4 anywhere under it (review s433-inflow-high #5).
clip_video_file() {
  local test_out="$1" clip="$2" video
  video="$(find "$test_out" -path '*/startRecording/*' -name "clip-$clip.mp4" -print -quit 2>/dev/null)"
  [ -n "$video" ] || video="$(find "$test_out" -name "clip-$clip.mp4" -print -quit 2>/dev/null)"
  echo "$video"
}

# Keeps one clip of a batch run: its mp4 (startRecording/clip-<clip>.mp4 under the run's output folder) is copied out, the mark file is written from its flow's commands.json
# (store-media.mjs mark prints every recorded step's times) and store-media.mjs check decides. Returns 1, naming why, when it is not kept.
collect_clip() {
  local label="$1" unit="$2" test_out="$3" clip target video commands
  clip="$(video_clip_id "$unit")"
  target="$(video_target "$label" "$unit")"
  rm -f "$target" "$(mark_file_of "$target")"
  video="$(clip_video_file "$test_out" "$clip")"
  if [ -z "$video" ]; then
    echo "::warning::$label: clip $clip: no clip-$clip.mp4 under $test_out (its flow stopped before startRecording); the files there:"
    find "$test_out" -maxdepth 4 | head -n 30
    return 1
  fi
  commands="$(clip_commands_file "$video" "$test_out" "$clip")"
  if [ -z "$commands" ] || [ ! -f "$commands" ]; then
    echo "::warning::$label: clip $clip: no commands.json for its flow under $test_out, so it has no mark"
    return 1
  fi
  cp "$video" "$target"
  if ! store_media mark "$commands" "$clip" "$(mark_file_of "$target")" || ! clip_recording_ok "$target" "$unit"; then
    echo "::warning::$label: clip $clip is not kept (the line above says why)"
    rm -f "$target" "$(mark_file_of "$target")"
    return 1
  fi
  note "- $label: $(basename "$target") recorded ($(wc -c < "$target" | tr -d ' ') bytes, mark $(head -1 "$(mark_file_of "$target")") s)"
  upload_shot "$target"
  upload_shot "$(mark_file_of "$target")"
}

# Slice 433 (in-flow recording, iPad run 45582715957): the given clips in ONE Maestro invocation, so the iOS driver starts once (about 60 s per invocation, all of which the old
# recorder filmed). Limit: one driver start plus VIDEO_TIME_LIMIT_SECONDS per clip. Sets CLIPS_MISSING to the clips that did not come out; returns 1 when the round could not start.
run_clip_batch() {
  local label="$1" udid="$2" bundle_id="$3" round="$4" dir test_out limit unit rc=0
  shift 4
  dir="$RUNNER_TEMP/clips-$label-$round"
  test_out="$RUNNER_TEMP/maestro-out-clips-$label-$round"
  limit=$((CLIP_BATCH_DRIVER_SECONDS + $# * VIDEO_TIME_LIMIT_SECONDS))
  CLIPS_MISSING=("$@")
  if ! time_left_for $((PREFLIGHT_TIME_LIMIT_SECONDS + limit + 2 * HIERARCHY_TIME_LIMIT_SECONDS + 10)); then
    problem "$label: no time left for clip round $round ($# clip(s), limit $limit s; script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
    return 1
  fi
  if ! write_clip_batch "$bundle_id" "$dir" "$@"; then
    problem "$label: the clip flows of round $round could not be written for $bundle_id"
    return 1
  fi
  answer_open_in_prompt "$udid" "$bundle_id"
  rm -rf "$test_out"
  mkdir -p "$test_out"
  note "- $label: clip round $round, $# clip(s) in one Maestro run (limit $limit s)"
  UNIT_LIMIT="$limit"
  RUN_DRIVER_STARTUP_TIMEOUT_MS="$CLIP_DRIVER_STARTUP_TIMEOUT_MS"
  run_flow "$udid" "$dir" "$test_out" "$bundle_id" || rc=$?
  RUN_DRIVER_STARTUP_TIMEOUT_MS="$DRIVER_STARTUP_TIMEOUT_MS"
  echo "clip round $round on $label: Maestro status $rc (killed at the $limit s limit: $LIMIT_HIT)"
  CLIPS_MISSING=()
  for unit in "$@"; do
    collect_clip "$label" "$unit" "$test_out" || CLIPS_MISSING+=("$unit")
  done
}

# The clip ids of the given flows, space-separated (for the log).
clip_names() {
  local unit names=""
  for unit in "$@"; do names="$names $(video_clip_id "$unit")"; done
  echo "${names# }"
}

# The clips that still need a recording on this device (one already in the out folder that passes the check is kept), one per line.
pending_clips() {
  local label="$1" unit target
  for unit in "${VIDEO_UNITS[@]}"; do
    target="$(video_target "$label" "$unit")"
    # The check's own line (kept, or why not) goes to the job log, not into the list (review s433-inflow-medium #5).
    if [ -f "$target" ] && clip_recording_ok "$target" "$unit" >&2; then
      note "- $label: $(basename "$target") kept (already recorded and checked, not retaken)" >&2
    else
      rm -f "$target" "$(mark_file_of "$target")"
      echo "$unit"
    fi
  done
}

# Returns 0 when this device records clips now: it is a preview device, its shelf and simulator are usable, and ffprobe is there to check every recording.
clips_can_start() {
  local label="$1"
  [ "$VIDEO_COUNT" -gt 0 ] || return 1
  video_device "$label" || return 1
  if [ "$SHELF_PREP_FAILED" = "yes" ] || [ "$SIM_BROKEN" = "yes" ]; then
    problem "$label: the $VIDEO_COUNT clip(s) are not recorded, the shelf set-up failed or the simulator is not usable"
    return 1
  fi
  if ! command -v ffprobe >/dev/null 2>&1; then
    problem "$label: ffprobe is not on the PATH, so no recording can be checked; no clip is recorded (the workflow's Install ffmpeg step puts it there)"
    return 1
  fi
}

# Round 2: the clips of CLIPS_MISSING once more, after a simulator restart, only when the restart and the whole round fit the budget (review s433-inflow-high #1).
# Every clip still missing after it is a problem.
run_clip_retry() {
  local label="$1" udid="$2" bundle_id="$3" unit pending=("${CLIPS_MISSING[@]}") cost
  note "- RETRY: $label: $(clip_names "${pending[@]}") did not come out in round 1; they are recorded once more after a simulator restart"
  cost=$((RESTART_TIME_LIMIT_SECONDS + PREFLIGHT_TIME_LIMIT_SECONDS + CLIP_BATCH_DRIVER_SECONDS + ${#pending[@]} * VIDEO_TIME_LIMIT_SECONDS + 2 * HIERARCHY_TIME_LIMIT_SECONDS + 10))
  if ! time_left_for "$cost"; then
    problem "$label: no time left for clip round 2 ($(clip_names "${pending[@]}"), $cost s; script budget $SCRIPT_BUDGET_SECONDS s, not a flow failure)"
    return 0
  fi
  if ! reset_simulator "$udid" "$bundle_id" yes; then
    SIM_BROKEN=yes
    problem "$label: the simulator did not come back within $RESTART_TIME_LIMIT_SECONDS s after clip round 1; $(clip_names "${pending[@]}") not recorded"
    return 0
  fi
  run_clip_batch "$label" "$udid" "$bundle_id" 2 "${pending[@]}" || return 0
  [ "${#CLIPS_MISSING[@]}" -gt 0 ] || return 0
  for unit in "${CLIPS_MISSING[@]}"; do
    problem "$label: clip $(video_clip_id "$unit") failed in both rounds (its warning lines above say why)"
  done
}

# Every clip after the shots, on the devices Apple's previews come from: all missing clips in one Maestro run (round 1), then run_clip_retry for what did not come out.
# A round that could not start (no time, flows not written) has already said why and is not retried (review s433-inflow-high #9).
run_video_units() {
  local label="$1" udid="$2" bundle_id="$3" base_limit="$UNIT_LIMIT" unit pending=() rc=0
  clips_can_start "$label" || return 0
  mkdir -p "$OUT/$label"
  while IFS= read -r unit; do
    [ -n "$unit" ] && pending+=("$unit")
  done < <(pending_clips "$label")
  [ "${#pending[@]}" -gt 0 ] || return 0
  run_clip_batch "$label" "$udid" "$bundle_id" 1 "${pending[@]}" || rc=$?
  UNIT_LIMIT="$base_limit"
  [ "$rc" -eq 0 ] && [ "${#CLIPS_MISSING[@]}" -gt 0 ] || return 0
  run_clip_retry "$label" "$udid" "$bundle_id"
  UNIT_LIMIT="$base_limit"
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

# Slice 433 (run time): takes the simulator ios-sim-store-preboot.sh created and booted for this device while the workflow built the app (PREBOOT_STATE; its process id
# PREBOOT_PID). Waits for the state file while the pre-boot still runs, at most PREBOOT_WAIT_SECONDS (a pre-boot still running then is stopped, so two simulators never boot at
# once), and prints the pre-boot's log (PREBOOT_LOG). Sets PREBOOT_UDID and PREBOOT_BUNDLE when that simulator is booted and named for this device; leaves them empty
# otherwise, and the caller boots one itself as before (review s433-1b-preboot-high #1: after shutting down and deleting every simulator the pre-boot made, so two never run at once).
use_preboot() {
  pick_preboot "$1"
  [ -n "${PREBOOT_STATE:-}" ] && [ -z "$PREBOOT_UDID" ] || return 0
  discard_preboot_simulators "$1"
}

# Shuts down and deletes every simulator named shots-<label> (what a failed, stopped or unusable pre-boot left), each simctl call under its limit.
discard_preboot_simulators() {
  local label="$1" udid
  for udid in $(xcrun simctl list devices 2>/dev/null | sed -n "s/^ *shots-$label (\([0-9A-F-]*\)).*/\1/p"); do
    echo "removing the pre-boot's simulator $udid (shots-$label)"
    run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl shutdown "$udid" >/dev/null 2>&1 || true
    run_limited "$SIMCTL_TIME_LIMIT_SECONDS" "$udid" xcrun simctl delete "$udid" >/dev/null 2>&1 || true
  done
}

# The pre-boot's simulator for use_preboot, or nothing.
pick_preboot() {
  local label="$1" started=$SECONDS udid bundle
  PREBOOT_UDID=""
  PREBOOT_BUNDLE=""
  [ -n "${PREBOOT_STATE:-}" ] || return 0
  while [ ! -f "$PREBOOT_STATE" ] && [ -n "${PREBOOT_PID:-}" ] && kill -0 "$PREBOOT_PID" 2>/dev/null \
    && [ $((SECONDS - started)) -lt "$PREBOOT_WAIT_SECONDS" ]; do
    sleep "$WATCHDOG_POLL_SECONDS"
  done
  echo "waited $((SECONDS - started)) s for the pre-boot of $label"
  if [ ! -f "$PREBOOT_STATE" ] && [ -n "${PREBOOT_PID:-}" ] && kill -0 "$PREBOOT_PID" 2>/dev/null; then
    echo "::warning::the pre-boot of $label did not finish in $PREBOOT_WAIT_SECONDS s; it is stopped and a simulator is booted here"
    kill_tree "$PREBOOT_PID"
  fi
  if [ -n "${PREBOOT_LOG:-}" ] && [ -f "$PREBOOT_LOG" ]; then
    echo "::group::pre-boot log ($label)"
    cat "$PREBOOT_LOG"
    echo "::endgroup::"
  fi
  [ -f "$PREBOOT_STATE" ] || { echo "no pre-boot state for $label: a simulator is booted here"; return 0; }
  udid="$(sed -n 's/^udid=//p' "$PREBOOT_STATE")"
  bundle="$(sed -n 's/^bundle=//p' "$PREBOOT_STATE")"
  if [ -z "$udid" ]; then
    echo "the pre-boot of $label failed ($(sed -n 's/^failed=//p' "$PREBOOT_STATE")): a simulator is booted here"
    return 0
  fi
  if ! xcrun simctl list devices booted 2>/dev/null | grep -F "shots-$label ($udid)" >/dev/null; then
    echo "::warning::the pre-booted simulator $udid is not booted as shots-$label: a simulator is booted here"
    return 0
  fi
  PREBOOT_UDID="$udid"
  PREBOOT_BUNDLE="$bundle"
  note "- $label: pre-booted simulator $udid used (created and booted while the app was built)"
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
  use_preboot "$label"
  udid="$PREBOOT_UDID"
  [ -n "$udid" ] || udid="$(SIM_SETTLE_SECONDS=20 SIM_DEVICE_NAME="shots-$label" SIM_DEVICE_PATTERNS="$patterns" GITHUB_ENV='' bash "$HERE/ios-sim-boot.sh" | tail -n 1 | sed 's/^udid=//')"
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
    # Slice 442: the warm-up never uses the time a shot attempt needs: it gets what is left of the script budget minus one unit, at most DRIVER_READY_BUDGET_SECONDS (the script itself starts no attempt it cannot finish).
    local warm_started=$SECONDS warm_budget=$((SCRIPT_BUDGET_SECONDS - SECONDS - UNIT_LIMIT))
    [ "$warm_budget" -le "$DRIVER_READY_BUDGET_SECONDS" ] || warm_budget="$DRIVER_READY_BUDGET_SECONDS"
    if [ "$warm_budget" -lt 60 ]; then
      note "- WARNING: $label: no time left for the Maestro driver warm-up (script budget $SCRIPT_BUDGET_SECONDS s); the first shot starts the driver itself"
    elif ! run_limited "$((warm_budget + 10))" "$udid" env DRIVER_READY_ATTEMPTS=2 DRIVER_READY_BUDGET_SECONDS="$warm_budget" DRIVER_READY_TIME_LIMIT_SECONDS="$DRIVER_READY_ATTEMPT_SECONDS" bash "$HERE/ios-sim-driver-ready.sh" "$udid"; then
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
