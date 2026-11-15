# Slice 389 — Tooling: the emulator preflight picks an installed JDK
**Product:** Shared

*Built (wave 2026-11-15), PR #1158.*

**Touches:** `tools/emulator/common.ps1`, `tools/emulator/run-flows.ps1` (`Test-MaestroPreflight` launches Maestro), `tools/emulator/preflight.yaml` only if it needs a change, `tools/emulator/test-jdk-pick.ps1`, `.github/workflows/tools-selftests.yml`, `tools/CLAUDE.md` (drop or reword the JAVA_HOME rule from Slice 291), `docs/plan/slices/389-tooling-emulator-preflight-picks-an-installed-jdk.md`

## Why

Device runs failed preflight because the machine `JAVA_HOME` points at a removed JDK (Slice 291 recorded the workaround in `tools/CLAUDE.md`: set `JAVA_HOME` by hand for the process).

## Work

- [x] 1. When `JAVA_HOME` is missing or points at a folder with no `java`, the `tools/emulator` preflight looks for an installed JDK, sets `JAVA_HOME` for that process only, and prints which one it chose.
- [x] 2. If none is found, fail with one line saying so.

**Search order (decided):** a valid `JAVA_HOME` (has `bin\java.exe`) first; else, among JDKs under (version read from the folder name; below 17 ignored; the newest major in 17..21 first, because Maestro's docs recommend Java 17 or 21 and warn about very new JDKs (context7 `/mobile-dev-inc/maestro-docs`, known-issues); only when none exists, the newest other one >= 17) `~/.jdks`, `Program Files\Eclipse Adoptium`, `Program Files\Microsoft`, then Android Studio's `jbr`. Set per process only. No JDK: prints `JDK not found: ...` and exits with the emulator-unhealthy code. Built in `Set-ProcessJavaHome` (`common.ps1`), called first in `Confirm-EmulatorPreflight`; self-test `tools/emulator/test-jdk-pick.ps1` (listed in `tools-selftests.yml`; its `tools/**` path filter already covers `tools/emulator/**`).

## From review

- Applied #1-#5 (`reviews/s389-high.md`), none skipped: a working `java` on PATH counts as found (a dead JAVA_HOME is cleared for the process so the launcher uses PATH); no JDK stops with exit 7 (`ExitJdkNotFound`, listed in run-flows.ps1's header, not the emulator's 5); an unparseable folder name is ignored, never throws; the version is read from the JDK's `release` file (JAVA_HOME and jbr too, a JAVA_HOME below 17 is replaced), then the folder name; `Set-ProcessJavaHome` takes `-Roots`/`-JavaOnPath` and `test-jdk-pick.ps1` covers it.

- Applied #1 and #4 (`reviews/s389-medium.md`): a `java` on PATH counts only when `java -version` reports major >= 17 (`Test-JavaOnPath`, `Get-JavaMajorFromVersionText`); search roots add `Program Files\Java`, `Program Files\Amazon Corretto`, `Program Files\Zulu` (both Program Files folders), with test cases.
- Skipped #2 (`reviews/s389-medium.md`): "Only run-flows.ps1 calls Set-ProcessJavaHome; other scripts launching Maestro still fail on dead JAVA_HOME." - other Maestro launchers are outside this slice's Touches; follow-up: store-screenshots.ps1 and tools/phone launchers do not call Set-ProcessJavaHome yet.
- Skipped #3 (`reviews/s389-medium.md`): "JDK check runs late (after emulator boot, install, lease wait) inside lease try block" - low severity, not applied this pass.
- Skipped #5 (`reviews/s389-medium.md`): "Unparseable JDK folder gets Major 0: Get-InstalledJdkHomes drops it, Test-JdkVersionOk accepts it as JAVA_HOME." - low severity, not applied this pass.
- Skipped #6 (`reviews/s389-medium.md`): "Self-test misses Test-JavaOnPath, exit 7 wiring in run-flows" - low severity, not applied this pass (the version parser and Java-root cases are now covered).
- Skipped #7 (`reviews/s389-medium.md`): "Set-ProcessJavaHome has a redundant trimmed-JAVA_HOME branch duplicating Select-JdkHome's Trim" - low severity, not applied this pass.
- Skipped #8 (`reviews/s389-medium.md`): "Exit code 7 documented only for run-flows" - low severity, not applied this pass.

## Follow-ups

- store-screenshots.ps1 and tools/phone launchers do not call Set-ProcessJavaHome yet.

## Verification

- A run with `JAVA_HOME` pointing at a removed folder passes preflight and prints the JDK chosen.
  - Result (2026-11-15, emulator-5554, this machine's real `JAVA_HOME` = the removed `C:\Program Files\Eclipse Adoptium\jdk-21.0.11.10-hotspot\`): PASS for the pick and the Maestro preflight. `Set-ProcessJavaHome` printed `JDK: JAVA_HOME is unusable (...); using C:\Users\ashka\.jdks\openjdk-26.0.1 for this run only.`, then Maestro ran `tools/emulator/preflight.yaml` to `exit=0`; Machine and User `JAVA_HOME` unchanged (both empty). Run through a throw-away script that dot-sources `common.ps1` (`reviews/s389-device/preflight-only.ps1`, log `preflight-only.log`), because `run-flows.ps1 -NoBuild` stopped first at a build-cache miss (exit 6, `Build-BeforeLease` runs before `Confirm-EmulatorPreflight`). The `run-flows.ps1` wiring and its exit 7 stay in Not yet verified.

#### For CLAUDE.md

- Maestro needs a live JDK: `run-flows.ps1`'s preflight (`Set-ProcessJavaHome` in `tools/emulator/common.ps1`) keeps a valid `JAVA_HOME` of major >= 17, else sets it for that process only (never the machine) to the newest JDK of major 17..21 (Maestro's docs recommend Java 17 or 21; context7 `/mobile-dev-inc/maestro-docs`), then a newer one, found under `~/.jdks`, `Program Files` and `Program Files (x86)` `Eclipse Adoptium`, `Microsoft`, `Java`, `Amazon Corretto`, `Zulu`, or Android Studio's jbr (version from the JDK's `release` file, else the folder name; below 17 ignored); else a `java` on PATH reporting major >= 17 (a dead `JAVA_HOME` is cleared for the process). It prints `JDK: ... using <path>`; with none it exits 7 with one `JDK not found` line. Self-test: `tools/emulator/test-jdk-pick.ps1`. Other Maestro launchers (`store-screenshots.ps1`, `tools/phone`) do not call it yet. (Slice 389: docs/plan/slices/389-tooling-emulator-preflight-picks-an-installed-jdk.md)

#### Not yet verified

- `run-flows.ps1` end to end with a removed `JAVA_HOME` (cached build, then `-NoBuild`): the `Confirm-EmulatorPreflight` wiring prints the chosen JDK, and with no JDK at all it exits 7. The pick and the Maestro preflight themselves passed on emulator-5554 (Verification above).
