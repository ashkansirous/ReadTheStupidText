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

# One line per device: label | ';'-separated device-type name patterns, in order | sizes App Store Connect accepts for that slot (WxH, portrait).
device_specs() {
  echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868 1290x2796 1260x2736'
  echo 'iphone-6.5|iPhone 11 Pro Max;iPhone Xs Max;iPhone 14 Plus;iPhone 13 Pro Max;iPhone 12 Pro Max|1242x2688 1284x2778'
  echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2064x2752 2048x2732'
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

# Runs the flow on a booted simulator; prints Maestro's hierarchy when it fails.
run_flow() {
  local udid="$1" flow_run="$2" test_out="$3" rc=0
  maestro --device "$udid" test --test-output-dir "$test_out" "$flow_run" || rc=$?
  if [ "$rc" != "0" ]; then
    echo "::group::maestro hierarchy"
    maestro --device "$udid" hierarchy || true
    echo "::endgroup::"
  fi
  return "$rc"
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
    elif run_flow "$udid" "$flow_run" "$test_out"; then
      collect_shots "$label" "$accepted" "$test_out"
    else
      problem "$label: the flow failed"
    fi
  fi
  xcrun simctl shutdown "$udid" || true
  xcrun simctl delete "$udid" || true
}

RUNNER_TEMP="${RUNNER_TEMP:-$(mktemp -d)}"
mkdir -p "$OUT"
# The specs come in on fd 3: maestro and xcrun read stdin and would swallow the remaining lines.
while IFS='|' read -r label patterns accepted <&3; do
  shoot_device "$label" "$patterns" "$accepted" < /dev/null
done 3< <(device_specs)

note "Shots for $APP are in $OUT: $(find "$OUT" -name '*.png' | wc -l | tr -d ' ') PNG(s)."
exit "$FAILED"
