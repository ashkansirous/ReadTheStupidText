#!/usr/bin/env bash
# Slice 390: builds DinoStoreKit.xcframework (device + simulator, arm64) from the Swift package beside this script.
# macOS with Xcode only (the iOS workflows select Xcode 26.6 first). Usage: build-xcframework.sh <output-dir>
# The result is <output-dir>/DinoStoreKit.xcframework; an existing one is replaced. DinoReader.App.csproj runs this
# before an iOS build when the output is missing or older than the Swift sources, and the Dino iOS workflows run it as
# their own step first so a Swift error shows up on its own line of the log.
set -euo pipefail

out="${1:?usage: build-xcframework.sh <output-dir>}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$here" # xcodebuild reads Package.swift from the working directory

archive() {
  local destination="$1" path="$2"
  xcodebuild archive \
    -scheme DinoStoreKit \
    -destination "$destination" \
    -archivePath "$path" \
    -derivedDataPath "$work/derived" \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    ARCHS=arm64 \
    ONLY_ACTIVE_ARCH=NO
}

archive "generic/platform=iOS" "$work/ios.xcarchive"
archive "generic/platform=iOS Simulator" "$work/sim.xcarchive"

mkdir -p "$out"
rm -rf "$out/DinoStoreKit.xcframework"
xcodebuild -create-xcframework \
  -framework "$work/ios.xcarchive/Products/Library/Frameworks/DinoStoreKit.framework" \
  -framework "$work/sim.xcarchive/Products/Library/Frameworks/DinoStoreKit.framework" \
  -output "$out/DinoStoreKit.xcframework"

test -f "$out/DinoStoreKit.xcframework/Info.plist"
echo "built $out/DinoStoreKit.xcframework"
