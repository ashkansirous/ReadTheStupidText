### Slice 224 — Phone runner: build-cache gaps from the 2026-10-31-1000 night, round 1

*parked: feedback loop, waits for the user.* Planned 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1000-feedback-loop-w1.md)). Parked so no later wave tonight builds it; the user moves it to `planned`.

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/checks/_lib.ps1`, `tools/phone/test-run-checks.ps1`, `tools/CLAUDE.md`, `docs/plan/slices/224-phone-runner-build-cache-gaps.md`

#### Work

1. **False "stalled" after a finished build** (wave-1 digest entries 221 and 326): during the Slice 220 S21 run, `run-checks.ps1` failed with "stalled: build log has not grown for 5 minutes" after the Dino compile had finished. `.phone/build-dino-debug.json` still said `building`, and its recorded pid (27560) was no longer running. The cause is not proven: a later agent could not reproduce it on a cached build (`_lib.ps1` build wait, `BuildStallMinutes = 5`).
   - [ ] A `building` status whose builder pid is gone reads as "build ended" (then the cache is checked: a ready APK means go, none means FAIL "builder exited without an APK"), and the status file is rewritten. Verify: a self-test in `test-run-checks.ps1` gives the wait a `building` file with a dead pid and a cached APK and expects go, not stalled.
2. **A cache miss starts a build inside a timed run** (entries 210, 221, 226): the brief said "reuse the cached build". It named the RTST build, but `short-lines` runs in Dino, so `run-checks.ps1 -App dino` started Dino Debug and AOT builds. The whole 10-minute budget went on builds, and nothing was measured.
   - [ ] `-NoBuild` (also settable with an env variable, so a brief can set it once): on a cache miss, exit at once with `cache missed for <app> <variant>, key <k>: run -Build first`, before the lease and before any build. Verify: a self-test with an empty cache and `-NoBuild` exits with that line and starts no process.
   - [ ] The table header prints the app and variant each check runs on, taken from the catalogue. Verify: a self-test in `test-run-checks.ps1` calls the header function for `-App dino -Checks book-end` and expects `dino debug` (the runner has no dry-run switch; `short-lines` exists only on the Slice 220 branch, #638).

#### Verification

- The verify line under each item, in `test-run-checks.ps1` (no phone needed).

#### Not yet verified

- Everything: planned only.
