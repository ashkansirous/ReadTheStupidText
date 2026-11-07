# Slice 308 — Slice 225 items 3 and 6: `run-checks.ps1 -Serial` for emulators, and the failed-build cause

*Built 2026-11-07, merged as PR #914 (9bf1a034). Item 1 run on emulator-5554 (paused-notification PASS); item 2 cause not found, see Not yet verified.* (Planned 2026-11-07.) From [Slice 225](225-device-tooling-leftovers-night-2026-10-31-1301.md) (PR #696). Item 3 was not built: the phone checks have no emulator path. Item 6: the runner's Debug builds failed or stalled repeatedly (entries 249, 494, 630: "stalled: log not grown 5 min", "dino debug failed") and runs fell back to the cached AOT build; nobody read `build-dino-debug.log` / `build-rtst-debug.log`, so the cause is unknown. Moved from plan.md's *Outstanding work*. Tooling only.

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/phone/test-run-checks.ps1`, `tools/phone/checks/_device.ps1`, `tools/phone/checks/_lib.ps1`, `tools/phone/test-phone-lib.ps1`, `tools/phone/README.md`, `tools/phone/connect.ps1`, `tools/phone/preflight.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/tap.ps1`, `tools/phone/lease.ps1`, `tools/emulator/common.ps1`, `tools/emulator/install-debug.ps1`, `tools/CLAUDE.md`, `docs/plan/slices/308-slice-225-run-checks-serial-and-build-cause.md`

**Claim notes (wave 2026-11-07, run-all-waves):** `tools/phone/audio-timing.ps1` already has an `-Emulator`/`-Serial` path to copy. `tools/CLAUDE.md` is shared with Slice 284 in this wave (each adds its own lines). Item 2 is an investigation: if no Debug build fails or stalls in the runs made, record that with the log lines read.

## Work

- [x] 1. `Find-WirelessPhone` (`tools/phone/common.ps1`) accepts wireless serials only. Add `run-checks.ps1 -Serial emulator-NNNN` so the same checks run on an emulator, with one RTST dev package id on both (coordinate with Slice 221 item 2, the Dino id). The "wireless only" rule is for the user's real phone and stays: an emulator serial is allowed only when named. Verify: `paused-notification` runs on emulator-5554 and reports PASS or FAIL, never a device error.
- [x] 2. (investigated; cause not found, the runner now keeps `<log>.failed.log`) Read `build-dino-debug.log` and `build-rtst-debug.log` from a failing run (run the Debug builds through the runner until one stalls or fails, keep the logs), write the cause in the slice file, and fix it or record it as an environment limit.
- [x] 3. Android only for the emulator path; the iOS equivalent is Slice 312's tooling, not this slice.

## Verification

- `test-run-checks.ps1` covers `-Serial` (accepted emulator serial, rejected unknown serial); one real run on the emulator; the cause in item 2 is a quoted log line.

## Design (two strikes)

Check passes 1 and 2 both found holes in the emulator opt-in (an ambient `RTST_PHONE_ALLOW_EMULATOR`, a second phone lease keyed by `ro.serialno`, the phone-only readiness gate, a lease wait counted twice), so the area was redesigned once instead of patched. `run-checks.ps1` decides a device kind once from `-Serial` (`Get-DeviceKind`: Phone | Emulator, in `tools/phone/common.ps1`); `Get-DeviceKindPlan` returns which lease and gates apply. No environment variable: the variable is removed and `-AllowEmulator` is an explicit parameter that run-checks passes to `Find-WirelessPhone` and to each child script (`preflight.ps1`, `tap.ps1`, `reset-dev-app.ps1`; the checks pass it through `Get-AllowEmulatorArguments`), so a script run by hand or by another caller stays wireless-only. Emulator: ONLY the emulator lease (kind `emulator`, key = adb serial, as `tools/emulator` does), never the phone lease or `ro.serialno`; no per-check charger/readiness gate, no stay-on save or restore; preflight returns `N/A` before any phone-only adb read; no AOT, with one line at the start saying so and naming the checks that need it. Phone: unchanged from `origin/main`. The kind-to-lease/gates choice and the argument helper are unit-tested in `test-phone-lib.ps1`; `test-run-checks.ps1` reads the source for the single lease wait and the early return of the gate. Review finding 9 (emulator console ports are 4 digits, 5554-5682) was skipped as not a bug.

## Built (2026-11-07)

*Changed while building:*

- Item 1: `run-checks.ps1 -Serial emulator-NNNN`, device kind decided once (see Design), explicit `-AllowEmulator` to the child scripts, emulator lease only, x86_64 debug build, `-Variant aot` refused (exit 2). Builder's notes follow; where they say "not yet run" or "still open", the emulator runs below and the Design section supersede them: `run-checks.ps1 -Serial emulator-NNNN` is accepted only when named, through `-AllowEmulator` passed by `run-checks.ps1` (`Find-WirelessPhone`, `Select-ReadyPhoneSerial` in `tools/phone/common.ps1`); the other phone scripts stay wireless-only (design below). An emulator run builds and caches the x86_64 APK (RID `android-x64`, cache name `android-dbg-x64[-aot]`, `Get-CachePlatform` with `$script:CacheAbi` in `checks/_lib.ps1`; the same as `audio-timing.ps1 -Emulator`, with the `.dbg` package) and takes the emulator lease (key = adb serial, kind `emulator`) and no phone lease. A named emulator that is not running prints an emulator message, not the pairing ask. Self-tests: `test-phone-lib.ps1` (the selector) and `test-run-checks.ps1` (cache names). Still open: the "one RTST dev package id on both" requirement (the emulator scripts install the plain `uk.sirous.readthestupidtext` id for RTST, run-checks the `.dbg` one, so an `install-debug.ps1` APK is not reused by run-checks), the real `paused-notification` run on emulator-5554, and whether `preflight.ps1` (charger, lock state) passes on an emulator.
- Item 2, cause not found in the logs on disk. `<worktrees>\.phone` holds Debug build logs from 2026-11-01 (`build-dino-debug.log`, `build-rtst-debug.log`, `build-rtst-debug-s241-e97de5.log`, `build-rtst-debug-s236-6bb714.log`); all four ended `0 Error(s)` / `Time Elapsed 00:02:54.77` to `00:04:20.14`, status `"state": "ok"`. No `*.failed.log` exists (Save-FailedBuildLog came after those runs), so no failed or stalled Debug build was ever kept. The only non-build line in the process logs is `Waiting before starting build-rtst-android-dbg-arm64: a quiet-machine timing run is in progress...`; `Wait-ForActivity` (`tools/emulator/activity.ps1`) prints it every 15 s, so a quiet-machine wait does not trip the 5-minute log-not-grown stall by itself. Cause of entries 249/494/630 stays unknown; the next failed or stalled build will leave `<log>.failed.log` (Slice 225 item 6 code, `Save-FailedBuildLog`, which has not yet run on a failing build), to read at the device stage. No build or emulator run was started here.
- Found on the device: a `Get-PackageUid` name collision (`checks/_lib.ps1` shadowed `common.ps1`'s) left the dev app's uid unread, so `paused-notification` never blocked or unblocked the network. Fixed by renaming the parser to `Get-PackageUidFromDump`. Not checked: whether phone runs on main were hit by the same collision.

*Emulator runs:*

- run2 FAIL (`reviews/s308-run2-paused-notification.log`: "network block not restored (no uid)"), which showed the collision.
- run3 PASS after the fix (`reviews/s308-run3-paused-notification.log`, 100 s).
- `-Variant aot` exits 2: PASS. Emulator lease taken and released: PASS.

*From review* (findings files under `D:/ReadTheStupidText-worktrees/reviews/`; skips in the fixers' briefs held the reasons for those not quoted below):

- `reviews/s308-high.md`: all 7 applied.
- `reviews/s308-medium.md`: 1, 2, 4, 5, 6, 8 applied.
- Skipped #3 (`reviews/s308-medium.md`): "pre-wait can be doubled", two lease waits only lengthen a pre-wait.
- Skipped #7 (`reviews/s308-medium.md`): "spacing reads like unary plus", style nit.
- `reviews/s308-check1.md`: 1-8 applied through the two-strikes redesign (see Design).
- Skipped #9 (`reviews/s308-check1.md`): "accepts only 4-5 digit ports", emulator console ports are 4 digits (5554-5682).
- `reviews/s308-check2.md`: 1, 3, 4, 6 applied.
- Skipped #2 (`reviews/s308-check2.md`): "emulator serial pattern", same reason as check1 #9.
- Skipped #5 (`reviews/s308-check2.md`): "gone offline/locked mid-run", an emulator dying mid-run surfaces as an adb error naming the serial.
- Skipped #7 (`reviews/s308-check2.md`): "ignores -AllowEmulator when -Serial is not emulator-shaped", only an emulator-shaped serial opts in, by design.
- Skipped #8 (`reviews/s308-check2.md`): "Write-AotNote (776) prints only after the exit", message only.
- Skipped #1-#5 (`reviews/s308-check3.md`): "No finding changes PHONE-run behaviour", all low; the phone path matches origin/main.
- Skipped #1 (`reviews/s308-check4.md`): "-Restore runs phone stay-on restore for emulator serials", it writes only the emulator's own setting.
- Skipped #2 (`reviews/s308-check4.md`): "emulator holder text formatted as phone", message text only.
- Skipped #3 (`reviews/s308-check4.md`): "shadows common.ps1's two-arg one", pre-existing, outside the diff.
- Skipped #4 (`reviews/s308-check4.md`): "different signatures here and in reset-dev-app.ps1", pre-existing, not loaded together.
- Skipped #5 (`reviews/s308-check4.md`): "no test scans dot-source graph", style.
- Skipped #6 (`reviews/s308-check4.md`): "uses literal 'aot'; run-checks.ps1 exits bare 2", style.

#### For CLAUDE.md (lifted 2026-11-07)

- `run-checks.ps1 -Serial emulator-NNNN` runs the phone checks on an emulator; the emulator is allowed only when named, and `-AllowEmulator` is an explicit parameter passed to each child script (`tools/phone/common.ps1`). No environment variable opts in; phone scripts run by hand stay wireless-only. An emulator takes the emulator lease only, never the phone lease; `-Variant aot` is refused. (Slice 308: docs/plan/slices/308-slice-225-run-checks-serial-and-build-cause.md)
- A function name defined in both `tools/phone/common.ps1` and a `tools/phone/checks/*.ps1` file shadows silently after dot-sourcing (Slice 308: `Get-PackageUid` left the dev app's uid unread). Check for an existing name before adding a helper. (Slice 308: docs/plan/slices/308-slice-225-run-checks-serial-and-build-cause.md)

#### Not yet verified

- Slice 225 item 2 cause (entries 249/494/630): no failed Debug build log exists on disk; the next failed build leaves `<log>.failed.log` to read.
