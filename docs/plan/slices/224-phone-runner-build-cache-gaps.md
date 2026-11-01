### Slice 224 — Phone runner: build-cache gaps from the 2026-10-31-1000 night, round 1

*Built (wave 2026-11-01), PR #682.* Written 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1000-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01.

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/checks/_lib.ps1`, `tools/phone/test-run-checks.ps1`, `tools/phone/checks/_device.ps1`, `tools/phone/lease.ps1` (widened at claim: catalogue header, `-NoBuild` exit before the lease), `tools/CLAUDE.md`, `docs/plan/slices/224-phone-runner-build-cache-gaps.md`

#### Work

1. **False "stalled" after a finished build** (wave-1 digest entries 221 and 326): during the Slice 220 S21 run, `run-checks.ps1` failed with "stalled: build log has not grown for 5 minutes" after the Dino compile had finished. `.phone/build-dino-debug.json` still said `building`, and its recorded pid (27560) was no longer running. The cause is not proven: a later agent could not reproduce it on a cached build (`_lib.ps1` build wait, `BuildStallMinutes = 5`).
   - [x] A `building` status whose builder pid is gone reads as "build ended" (then the cache is checked: a ready APK means go, none means FAIL "builder exited without an APK"), and the status file is rewritten. Verify: a self-test in `test-run-checks.ps1` gives the wait a `building` file with a dead pid and a cached APK and expects go, not stalled. Built: `Test-BuilderExited` (the pid must also have begun no later than the status's `startedUtc`, so a reused pid is not the builder), the cache is re-read after the pid check, and the file is rewritten by `Write-EndedBuildStatus`. Tested with temp files and this process's own pid; `Get-BuildView`'s call of them in `run-checks.ps1` is not run by a self-test.
2. **A cache miss starts a build inside a timed run** (entries 210, 221, 226): the brief said "reuse the cached build". It named the RTST build, but `short-lines` runs in Dino, so `run-checks.ps1 -App dino` started Dino Debug and AOT builds. The whole 10-minute budget went on builds, and nothing was measured.
   - [x] `-NoBuild` (also settable with an env variable, so a brief can set it once): on a cache miss, exit at once with `cache missed for <app> <variant>, key <k>: run -Build first`, before the lease and before any build. Verify: a self-test with an empty cache and `-NoBuild` exits with that line and starts no process. Built: under `-NoBuild` no build process starts at all (an uncached variant no check needs is recorded as not built), and a miss prints the line and the table (every check SKIP), writes `run-status.json` and exits 8 (`ExitCacheMissed`), not 6. Tested by running the script itself with `RTST_RUN_STATUS_FILE` on a temp file (the child looks up the cache in an empty temp folder through `RTST_BUILD_CACHE_LOOKUP_ROOT`, so it always misses and exits 8, whatever this checkout's cache holds).
   - [x] The header line (`runs on: <check> = <app> <variant>`, printed before the table) prints the app and variant each check runs on, taken from the catalogue. Verify: a self-test in `test-run-checks.ps1` calls the header function for `-App dino -Checks book-end` and expects `dino debug` (the runner has no dry-run switch). A skipped check is listed as `skipped:`, not as run.

#### Changed while building

- A `queued` status with a dead pid is treated as ended too, not only `building`.
- Dead-builder detection also needs the live process's start time to be no later than `startedUtc` + 120 s; otherwise the pid was reused and the builder is dead. The status file is rewritten only if it is still the same build.
- `-NoBuild` exits 8 (`ExitCacheMissed`) with the table (all SKIP) and `run-status.json`, not a bare exit 6 line.
- Fixed a stray CR in `run-checks.ps1`'s header comment. Known leftover on main, not fixed here: a backspace byte in the same header comment (`build-` shows as `^Huild-`).
- Self-tests in `test-run-checks.ps1`: dead pid, queued, null start time, stale rewrite, and the `-NoBuild` child through the `RTST_BUILD_CACHE_LOOKUP_ROOT` seam. All `tools-selftests.yml` steps pass in pwsh and Windows PowerShell 5.1.

#### From review

- High (`D:/ReadTheStupidText-worktrees/reviews/s224-high.md`): applied 1 (`-NoBuild` starts no background build), 2 (re-read the cache after the pid check), 3 (reused pid via start time), 5 (exit 8, table and `run-status.json`), 6 (end-to-end tests / wording). Applied 8 (`tools/CLAUDE.md` bullet). Skipped 4 (header lists skipped plans like pending ones; low) and 7 (one-line methods; low).
- Medium (`s224-medium.md`): applied 1 (re-read and compare pid and `startedUtc` before the rewrite), 2 (null `StartTime` treated as unknown), 3 (`queued` with a dead pid). Finding 4 is not a bug (the miss test and the run both go through `Get-NoBuildMiss`, which already treats AOT as serving Debug). Skipped 5 (`Stop-CacheMissed` recomputes the key; low), 6 (duplicated `-NoBuild` condition; low), 7 (120 s slack constant placement; low).
- Medium check 1 (`s224-medium-check.md`): applied 1 (the `-NoBuild` child self-test can no longer reach a phone). Skipped 2-8 as low: 2 (a failed Debug record with only AOT cached has no failure path today, so a test would be speculative), 3 (repeated key lookups), 4 (duplicated condition), 5 (`StartTime` try/catch and the slack window), 6 (`WriteAllText` not atomic), 7 (`$PidAlive` arity comment), 8 (`queued` builder may be waiting on a lock).
- Medium check 2 (`s224-medium-check2.md`): all 8 findings low or style, none applied: 1 (test-only env seam in `Find-VariantApk`), 2 (repeated key lookups), 3 and 4 (dead-pid rewrite and `ToUniversalTime` kind on a non-UTC timezone, unconfirmed), 5 (partial-cache test), 6-8 (style).

#### Verification

- The verify line under each item, in `test-run-checks.ps1` (no phone needed).

#### For CLAUDE.md

- `tools/CLAUDE.md`: `run-checks.ps1 -NoBuild` (or `RTST_NO_BUILD=1`) exits 8 on a cache miss before the lease and starts no build; use it in timed briefs. A `building`/`queued` status with a dead or reused pid is rewritten as ended. (Slice 224: docs/plan/slices/224-phone-runner-build-cache-gaps.md)

#### Not yet verified

- A real phone run of the runner with a dead-builder status file, and with `-NoBuild` on a warm cache (no device run this wave; host tooling only).
