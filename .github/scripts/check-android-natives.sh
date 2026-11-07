#!/usr/bin/env bash
# Checks the native libraries of a built Android package (.aab or .apk) under arm64-v8a.
# A clean build with no warnings is not proof the right binaries landed (CLAUDE.md, Mobile
# gotcha), and the Debug and Release packaging paths can differ.
# Usage: check-android-natives.sh <path-to-.aab-or-.apk> [--none]
#   default: the package must carry the sherpa-onnx native libraries (Read The Stupid Text).
#   --none:  the package must carry none of sherpa-onnx, onnxruntime, ML Kit or the translate
#            native library (Dino Reader, Slice 305).
set -euo pipefail

pkg="${1:?usage: check-android-natives.sh <aab-or-apk> [--none]}"
mode="${2:-}"
if [ ! -f "$pkg" ]; then
  echo "::error::Package not found: $pkg"
  exit 1
fi

listing="$(unzip -l "$pkg")"
bad=0

if [ "$mode" = "--none" ]; then
  # .aab keeps libs under base/lib/<abi>/, .apk under lib/<abi>/.
  for pattern in 'libonnxruntime\.so' 'libsherpa-onnx-c-api\.so' 'libmlkit_[^/ ]*\.so' 'libtranslate_jni\.so'; do
    if grep -Eq "[[:space:]](base/)?lib/[^/]+/${pattern}\$" <<< "$listing"; then
      echo "::error::${pattern} is present in $pkg but this app must not ship it"
      bad=1
    else
      echo "ok: no ${pattern} in $pkg"
    fi
  done
  exit "$bad"
fi

for lib in libonnxruntime.so libsherpa-onnx-c-api.so; do
  if grep -Eq "[[:space:]](base/)?lib/arm64-v8a/${lib}$" <<< "$listing"; then
    echo "ok: $lib in $pkg"
  else
    echo "::error::$lib is missing from lib/arm64-v8a/ in $pkg"
    bad=1
  fi
done
exit "$bad"
