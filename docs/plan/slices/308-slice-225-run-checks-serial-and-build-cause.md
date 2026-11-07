# Slice 308 — Slice 225 items 3 and 6: `run-checks.ps1 -Serial` for emulators, and the failed-build cause

*Planned 2026-11-07.* From [Slice 225](225-device-tooling-leftovers-night-2026-10-31-1301.md) (PR #696). Item 3 was not built: the phone checks have no emulator path. Item 6: the runner's Debug builds failed or stalled repeatedly (entries 249, 494, 630: "stalled: log not grown 5 min", "dino debug failed") and runs fell back to the cached AOT build; nobody read `build-dino-debug.log` / `build-rtst-debug.log`, so the cause is unknown. Moved from plan.md's *Outstanding work*. Tooling only.

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/phone/test-run-checks.ps1`, `tools/phone/checks/_device.ps1`, `tools/phone/README.md`, `tools/emulator/common.ps1`, `tools/emulator/install-debug.ps1`, `tools/CLAUDE.md`, `docs/plan/slices/308-slice-225-run-checks-serial-and-build-cause.md`

## Work

- [ ] 1. `Find-WirelessPhone` (`tools/phone/common.ps1`) accepts wireless serials only. Add `run-checks.ps1 -Serial emulator-NNNN` so the same checks run on an emulator, with one RTST dev package id on both (coordinate with Slice 221 item 2, the Dino id). The "wireless only" rule is for the user's real phone and stays: an emulator serial is allowed only when named. Verify: `paused-notification` runs on emulator-5554 and reports PASS or FAIL, never a device error.
- [ ] 2. Read `build-dino-debug.log` and `build-rtst-debug.log` from a failing run (run the Debug builds through the runner until one stalls or fails, keep the logs), write the cause in the slice file, and fix it or record it as an environment limit.
- [ ] 3. Android only for the emulator path; the iOS equivalent is Slice 312's tooling, not this slice.

## Verification

- `test-run-checks.ps1` covers `-Serial` (accepted emulator serial, rejected unknown serial); one real run on the emulator; the cause in item 2 is a quoted log line.
