#!/usr/bin/env bash
# Usage: compile-dino-test-package.sh <project.csproj> <platform> <output-dir>
# Slice 354 item 4: compiles Dino Reader's Windows head the way the VM test package is built (Release + DinoTestBooks, see
# tools/desktop-vm/Build-TestPackage.ps1), so a break in src/Dino/DinoReader.App/Testing/** fails a pull request (Slice 346 merged a
# CS0234 there that only a VM build showed). Compile only: nothing is packaged, uploaded or released from this build.
# It lives under .github/scripts on purpose: NoBundledBooksTests bans the DinoTestBooks property from every other CI file, so a
# Store or release build can never carry the test books.
set -euo pipefail

project="${1:?usage: compile-dino-test-package.sh <project.csproj> <platform>}"
platform="${2:?usage: compile-dino-test-package.sh <project.csproj> <platform> <output-dir>}"
output="${3:?usage: compile-dino-test-package.sh <project.csproj> <platform> <output-dir>}"

dotnet build "$project" --disable-build-servers \
  -o "$output" \
  -f net11.0-windows10.0.19041.0 \
  -c Release \
  -p:IncludeAndroid=false \
  -p:Platform="$platform" \
  -p:DinoTestBooks=true
