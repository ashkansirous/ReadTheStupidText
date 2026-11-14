# Slice 389 — Tooling: the emulator preflight picks an installed JDK
**Product:** Shared

*Planned.*

**Touches:** `tools/emulator/common.ps1`, `tools/emulator/preflight.yaml` only if it needs a change, `tools/CLAUDE.md` (drop or reword the JAVA_HOME rule from Slice 291), `docs/plan/slices/389-tooling-emulator-preflight-picks-an-installed-jdk.md`

## Why

Device runs failed preflight because the machine `JAVA_HOME` points at a removed JDK (Slice 291 recorded the workaround in `tools/CLAUDE.md`: set `JAVA_HOME` by hand for the process).

## Work

- [ ] 1. When `JAVA_HOME` is missing or points at a folder with no `java`, the `tools/emulator` preflight looks for an installed JDK, sets `JAVA_HOME` for that process only, and prints which one it chose.
- [ ] 2. If none is found, fail with one line saying so.

## Verification

- A run with `JAVA_HOME` pointing at a removed folder passes preflight and prints the JDK chosen.
