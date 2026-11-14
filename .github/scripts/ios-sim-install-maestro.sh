#!/usr/bin/env bash
# Slice 244: install the Maestro CLI on a macOS runner (also used by Slice 291's screenshot run).
# Needs Java 17+ on the PATH (the workflow sets up temurin 17 first). Maestro has needed no idb since its own
# XCUITest driver (1.31); the driver is installed on the simulator by the first `maestro test`.
#
#   MAESTRO_VERSION  default below: the version `tools/` runs on Windows (README: .maestro/README.md)
# Appends the install's bin folder to $GITHUB_PATH when that is set.
set -euo pipefail

MAESTRO_VERSION="${MAESTRO_VERSION:-2.12.0}"
export MAESTRO_VERSION

java -version
curl -fsSL "https://get.maestro.mobile.dev" | bash

MAESTRO_BIN="$HOME/.maestro/bin"
if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$MAESTRO_BIN" >> "$GITHUB_PATH"
fi
export PATH="$MAESTRO_BIN:$PATH"
maestro --version
