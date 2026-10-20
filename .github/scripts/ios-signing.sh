#!/usr/bin/env bash
# Usage: ios-signing.sh install | cleanup
# Signing material for build-ios, adapted from Lets Call Mom's ios.yml (Decision 70).
#   install : imports the Apple Distribution certificate into a temp keychain, installs
#             the App Store provisioning profile, and writes the App Store Connect API key.
#             Prints codesign_key and codesign_provision to $GITHUB_OUTPUT for
#             `dotnet publish -p:CodesignKey/-p:CodesignProvision`.
#   cleanup : removes all of it (run with `if: always()`).
# install reads: IOS_DIST_CERT_P12_BASE64, IOS_DIST_CERT_P12_PASSWORD,
# IOS_PROVISIONING_PROFILE_BASE64. Needs RUNNER_TEMP (and GITHUB_OUTPUT for install).
# The API key is NOT written here: only testflight-upload.sh needs it.
set -euo pipefail

KEYCHAIN="${RUNNER_TEMP:?}/signing.keychain-db"
PROFILE_DIRS=("$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles" "$HOME/Library/MobileDevice/Provisioning Profiles")
PROFILE_LIST="${RUNNER_TEMP}/rtst-profiles.txt"

install_certificate() {
  local password
  password="$(openssl rand -hex 16)"
  echo "$IOS_DIST_CERT_P12_BASE64" | base64 --decode > "$RUNNER_TEMP/dist.p12"
  security create-keychain -p "$password" "$KEYCHAIN"
  security set-keychain-settings -lut 3600 "$KEYCHAIN"
  security unlock-keychain -p "$password" "$KEYCHAIN"
  security import "$RUNNER_TEMP/dist.p12" -P "$IOS_DIST_CERT_P12_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN"
  security set-key-partition-list -S apple-tool:,apple: -k "$password" "$KEYCHAIN" > /dev/null
  # shellcheck disable=SC2046
  security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')
  rm -f "$RUNNER_TEMP/dist.p12"
}

# Xcode 16+ reads profiles from UserData, older Xcodes from MobileDevice: install in both.
install_profile() {
  local raw="$RUNNER_TEMP/profile.mobileprovision" decoded="$RUNNER_TEMP/profile.plist" uuid dir
  echo "$IOS_PROVISIONING_PROFILE_BASE64" | base64 --decode > "$raw"
  security cms -D -i "$raw" > "$decoded"
  uuid=$(/usr/libexec/PlistBuddy -c 'Print :UUID' "$decoded")
  : > "$PROFILE_LIST"
  for dir in "${PROFILE_DIRS[@]}"; do
    mkdir -p "$dir"
    cp "$raw" "$dir/$uuid.mobileprovision"
    echo "$dir/$uuid.mobileprovision" >> "$PROFILE_LIST"
  done
  echo "codesign_provision=$(/usr/libexec/PlistBuddy -c 'Print :Name' "$decoded")" >> "$GITHUB_OUTPUT"
  rm -f "$raw" "$decoded"
}

# The SHA-1 hash of the first valid Distribution identity in the temp keychain. The
# hash is unambiguous where a name can match twice (an old and a renewed certificate)
# and needs no quoting. Matches "Apple Distribution" and the older "iPhone Distribution".
# find-identity -v lines look like:  1) <40 hex chars> "Apple Distribution: Name (TEAM)"
publish_identity() {
  local hash
  hash=$(security find-identity -v -p codesigning "$KEYCHAIN" |
    sed -nE 's/^ *[0-9]+\) ([0-9A-Fa-f]{40}) "(Apple|iPhone) Distribution:.*/\1/p' | head -n 1)
  if [ -z "$hash" ]; then
    echo "::error::No valid 'Apple Distribution' or 'iPhone Distribution' identity in the imported certificate."; exit 1
  fi
  echo "codesign_key=$hash" >> "$GITHUB_OUTPUT"
}

cleanup() {
  security delete-keychain "$KEYCHAIN" || true
  if [ -f "$PROFILE_LIST" ]; then xargs -I{} rm -f {} < "$PROFILE_LIST"; fi
  rm -f "$PROFILE_LIST" "$RUNNER_TEMP/dist.p12"
}

case "${1:-}" in
  install) install_certificate; install_profile; publish_identity ;;
  cleanup) cleanup ;;
  *) echo "Usage: $0 install|cleanup" >&2; exit 2 ;;
esac
