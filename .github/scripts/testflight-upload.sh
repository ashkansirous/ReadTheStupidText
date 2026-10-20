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

ipa="${1:?usage: testflight-upload.sh <path-to.ipa>}"
keydir="$HOME/.appstoreconnect/private_keys"
log="${RUNNER_TEMP:?}/altool.log"

mkdir -p "$keydir"
echo "$ASC_API_KEY_P8_BASE64" | base64 --decode > "$keydir/AuthKey_${ASC_API_KEY_ID}.p8"
trap 'rm -f "$keydir/AuthKey_${ASC_API_KEY_ID}.p8"' EXIT

xcrun altool --upload-app --type ios --file "$ipa" \
  --apiKey "$ASC_API_KEY_ID" --apiIssuer "$ASC_API_ISSUER_ID" 2>&1 | tee "$log"

if grep -qiE 'UPLOAD FAILED|\*\*\* Error|ERROR:' "$log" || ! grep -qiE 'No errors uploading|UPLOAD SUCCEEDED' "$log"; then
  echo "::error::altool did not report a successful upload; see the log above."; exit 1
fi
echo "Uploaded $(basename "$ipa") to App Store Connect." >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
