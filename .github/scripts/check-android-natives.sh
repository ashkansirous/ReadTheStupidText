#!/usr/bin/env bash
# Fails if a built Android package (.aab or .apk) lacks the sherpa-onnx native libraries
# under arm64-v8a. A clean build with no warnings is not proof the right binaries landed
# (CLAUDE.md, Mobile gotcha), and the Debug and Release packaging paths can differ.
# Usage: check-android-natives.sh <path-to-.aab-or-.apk>
set -euo pipefail

pkg="${1:?usage: check-android-natives.sh <aab-or-apk>}"
if [ ! -f "$pkg" ]; then
  echo "::error::Package not found: $pkg"
  exit 1
fi

listing="$(unzip -l "$pkg")"
missing=0
for lib in libonnxruntime.so libsherpa-onnx-c-api.so; do
  # .aab keeps libs under base/lib/<abi>/, .apk under lib/<abi>/.
  if printf '%s\n' "$listing" | grep -Eq "[[:space:]](base/)?lib/arm64-v8a/${lib}\$"; then
    echo "ok: $lib in $pkg"
  else
    echo "::error::$lib is missing from lib/arm64-v8a/ in $pkg"
    missing=1
  fi
done
exit "$missing"
