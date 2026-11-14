#!/usr/bin/env bash
# Slice 244: put a simulator run's evidence into the job log and step summary (no artifacts, Batch 8).
# Prints every crash report (.ips) of the app and the app's filtered unified log.
# macOS bash 3.2.
#
# Usage: ios-sim-collect.sh <udid> <process-name> [since-marker-file]
#   since-marker-file: only .ips files newer than this file count (the workflow touches it before the flow).
# Exit 0 when the app has no crash report, 3 when it has one (the workflow turns that into a failure).
set -uo pipefail

UDID="${1:?simulator udid}"
PROCESS="${2:?process name}"
MARKER="${3:-}"
SUMMARY="${GITHUB_STEP_SUMMARY:-/dev/null}"
REPORTS="$HOME/Library/Logs/DiagnosticReports"

# Crash reports of the app: macOS names them <process>-<date>.ips (the simulator writes to the host's folder).
# A memory kill leaves a JetsamEvent-*.ips instead; it lists every process, so it counts only when the app is its largest process.
find_reports() {
  local newer=() f
  if [ -n "$MARKER" ]; then
    newer=(-newer "$MARKER")
  fi
  find "$REPORTS" -maxdepth 2 -name "${PROCESS}*.ips" ${newer[@]+"${newer[@]}"} 2>/dev/null
  while IFS= read -r f; do
    if grep -q "\"largestProcess\" *: *\"$PROCESS\"" "$f" 2>/dev/null; then
      echo "$f"
    fi
  done < <(find "$REPORTS" -maxdepth 2 -name 'JetsamEvent*.ips' ${newer[@]+"${newer[@]}"} 2>/dev/null)
}

print_reports() {
  local count=0 f
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    count=$((count + 1))
    echo "::group::crash report $f"
    cat "$f"
    echo "::endgroup::"
    { echo "### Crash report: $(basename "$f")"; echo '```'; head -c 60000 "$f"; echo '```'; } >> "$SUMMARY"
  done < <(find_reports)
  echo "$count"
}

print_log() {
  local out
  out=$(xcrun simctl spawn "$UDID" log show --last 30m --style compact --predicate "process == \"$PROCESS\"" 2>&1 | tail -n 3000)
  echo "::group::log of $PROCESS (last 3000 lines)"
  echo "$out"
  echo "::endgroup::"
  { echo "### Log of $PROCESS (last 200 lines)"; echo '```'; echo "$out" | tail -n 200; echo '```'; } >> "$SUMMARY"
}

# macOS writes the .ips a little after the process dies: give it up to 30 s to appear before counting.
wait_for_reports() {
  local i
  for i in $(seq 1 15); do
    if [ -n "$(find_reports)" ]; then
      return 0
    fi
    sleep 2
  done
  return 0
}

wait_for_reports
print_log
COUNT=$(print_reports | tee /dev/stderr | tail -n 1)
case "$COUNT" in
  ''|*[!0-9]*) COUNT=0 ;;
esac
echo "crash reports for $PROCESS: $COUNT"
echo "Crash reports for \`$PROCESS\`: **$COUNT**" >> "$SUMMARY"
if [ "$COUNT" != "0" ]; then
  exit 3
fi
exit 0
