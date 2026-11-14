#!/usr/bin/env bash
# Usage: testflight-upload.sh <path-to.ipa>
# Uploads a signed App Store .ipa to App Store Connect (it then appears in TestFlight once
# Apple finishes processing, up to ~30 minutes; this does not wait for that).
# Needs: ASC_API_KEY_P8_BASE64, ASC_API_KEY_ID, ASC_API_ISSUER_ID, on a macOS runner.
# altool reads the key from ~/.appstoreconnect/private_keys/AuthKey_<id>.p8.
#
# altool can exit 0 after a failed upload on Xcode 26 (fastlane issues 29739/29743), so
# its output is checked as well as its exit status. Distributing the build to a tester
# group stays a manual step in App Store Connect (Decision 70).
set -euo pipefail

# Slice 354 item 1: exit 0 when the upload succeeded, 1 when it did not. The LAST decisive line of the log wins: altool can log an
# "ERROR: ... 500" on "CHANGE UPLOAD STATE TO COMPLETE", retry, and still end with "UPLOAD SUCCEEDED with no errors" (Dino 1.124.0, 2026-11-12;
# RTST 1.100.0 the same). A log with no decisive line fails (Xcode 26 can exit 0 after a failed upload). Tested by altool-verdict.test.mjs; the build's arrival is then confirmed by asc-confirm-build.mjs (workflow step after this one).
altool_verdict() {
  local last
  last="$(grep -iE 'UPLOAD FAILED|\*\*\* Error|^ERROR:|No errors uploading|UPLOAD SUCCEEDED' "$1" | tail -n 1 || true)"
  if echo "$last" | grep -qiE 'No errors uploading|UPLOAD SUCCEEDED'; then
    echo "altool verdict: uploaded (last decisive line: $last)"; return 0
  fi
  echo "altool verdict: not uploaded (last decisive line: ${last:-none})"; return 1
}

upload() {
  local ipa="${1:?usage: testflight-upload.sh <path-to.ipa>}"
  local keydir="$HOME/.appstoreconnect/private_keys"
  local log="${RUNNER_TEMP:?}/altool.log"

  mkdir -p "$keydir"
  echo "$ASC_API_KEY_P8_BASE64" | base64 --decode > "$keydir/AuthKey_${ASC_API_KEY_ID}.p8"
  trap 'rm -f "$HOME/.appstoreconnect/private_keys/AuthKey_${ASC_API_KEY_ID}.p8"' EXIT

  # A non-zero altool exit must not abort here (set -e + pipefail): the log's last decisive line is the verdict (a retry can
  # end in "UPLOAD SUCCEEDED" after an earlier error), so the exit status is only reported.
  local status=0
  xcrun altool --upload-app --type ios --file "$ipa" \
    --apiKey "$ASC_API_KEY_ID" --apiIssuer "$ASC_API_ISSUER_ID" 2>&1 | tee "$log" || status=$?
  echo "altool exit status: $status"

  if ! altool_verdict "$log"; then
    echo "::error::altool did not report a successful upload; see the log above."; exit 1
  fi
  echo "Uploaded $(basename "$ipa") to App Store Connect." >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
}

# Sourced by the test (altool_verdict only); run as a script it uploads.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then upload "$@"; fi
