#!/usr/bin/env bash
# Slice 390: builds DinoStoreKit.xcframework (a static library for device and simulator, arm64) from Sources/DinoStoreKit/DinoStoreKit.swift.
# macOS with Xcode only (the iOS workflows select Xcode 26.6 first). Usage: build-xcframework.sh <output-dir>
# The result is <output-dir>/DinoStoreKit.xcframework; an existing one is replaced. DinoReader.App.csproj runs this
# before an iOS build when the output is missing or older than the Swift sources, and the Dino iOS workflows run it as
# their own step first so a Swift error shows up on its own line of the log. A plain swiftc call, no Swift package:
# a package archive puts a static product in usr/local/lib, not in a framework, and one file needs no package.
set -euo pipefail

out="${1:?usage: build-xcframework.sh <output-dir>}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

compile() {
  local sdk="$1" target="$2" dir="$3"
  mkdir -p "$dir"
  xcrun --sdk "$sdk" swiftc -emit-library -static -parse-as-library -O \
    -module-name DinoStoreKit \
    -target "$target" \
    -swift-version 5 \
    -o "$dir/libDinoStoreKit.a" \
    "$here/Sources/DinoStoreKit/DinoStoreKit.swift"
}

compile iphoneos arm64-apple-ios15.5 "$work/ios"
compile iphonesimulator arm64-apple-ios15.5-simulator "$work/sim"

mkdir -p "$out"
rm -rf "$out/DinoStoreKit.xcframework"
xcodebuild -create-xcframework \
  -library "$work/ios/libDinoStoreKit.a" \
  -library "$work/sim/libDinoStoreKit.a" \
  -output "$out/DinoStoreKit.xcframework"

test -f "$out/DinoStoreKit.xcframework/Info.plist"
echo "built $out/DinoStoreKit.xcframework"
