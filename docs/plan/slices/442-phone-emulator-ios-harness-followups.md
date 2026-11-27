# Slice 442 — Phone, emulator and iOS harness follow-ups from Slice 393
**Product:** Shared
**Touches:** `tools/emulator/store-screenshots.ps1`, `tools/emulator/reset-dino-language.ps1`, `tools/phone/**`, `.github/scripts/ios-sim-*.sh`, `.github/workflows/dinoreader-ios-sim.yml`

*Planned 2026-11-25 (plan-cleanup of that day).*

*Built (wave 2026-11-27), PR #1439, not merged. Device run 2026-11-27 17:19Z passed; the iOS CI Mac changes have not run yet.*

Small, low findings left open by the Slice 393 reviews, all in this repo's tooling (full text in the Slice 393 file under *From review*, *Follow-ups from PR #1071* and *Follow-ups from the check pass after the CI fix*):

- `tools/emulator/store-screenshots.ps1`: read `$LASTEXITCODE` per adb call so the demo-mode early stop can fire; fail with start-warm's exit code when `Confirm-EmulatorForDemo` cannot start the emulator.
- `tools/emulator/reset-dino-language.ps1`: do not clear the whole logcat per try (use `-T`), check `pm list packages` and the app pid first, report a missing install correctly.
- `.github/scripts/ios-sim-driver-ready.sh`, `ios-sim-store-shots.sh`, `ios-sim-boot.sh`, `dinoreader-ios-sim.yml`: time-left guard against `SCRIPT_BUDGET_SECONDS`, step timeout versus 2x330 s worst case, `kill_tree` between attempts, the 20 s settle only for Maestro callers.
- `tools/phone/lease.ps1`, `preflight.ps1`, `common.ps1`, `connect.ps1`, `test-phone-leftovers.ps1`: move `Find-Phone` below the `-Clear` block, remove the unreachable second `if ($Clear)`, `-Serial` shape check and `Select-ConnectedAddress` honouring `-Serial`, `-Release` restoring `stay_on_while_plugged_in`, a runnable preflight test with a stubbed foreign lease, try/finally in `test-phone-lib.ps1`.

Verification: each tool's own self-test (`test-store-screenshots.ps1`, `test-reset-dino-language.ps1`, `test-phone-leftovers.ps1`) stays green in `tools-selftests.yml`.

#### From review

- Skipped #7 (`reviews/pr-1439-high.md`): "`kill_driver` runs after every failed attempt including the last and uses `pkill -9 -f "maestro.*$UDID"` and `"xcodebuild.*$UDID"`, so it also kills a Maestro run another step started on the same simulator, against the header comment "only ones this script started"." - the workflow's steps run one after another in a single job, so no other step's Maestro runs while this one's pkill runs; the extra kill after the last attempt is harmless.
- Skipped #4 (`reviews/pr-1439-medium.md`): "The warm-up budget subtracts UNIT_LIMIT, a global that earlier units reassign (lines ~206, 694, 931), so for a later device it holds the last unit's limit: a spurious "no time for warm-up" or too little time for the first shot." - no failure path: the warm-up budget is computed once in that call, before UNIT_LIMIT changes.
- Skipped #5 (`reviews/pr-1439-medium.md`): "Test-PhoneSerialShape is applied only in connect, lease and preflight; run-checks.ps1 -Serial and RTST_PHONE_SERIAL still accept loopback/localhost, so lease and run can disagree on what a phone is." - the slice line names the shape check for lease, preflight and connect only; other entry points are outside this slice's items.
- Skipped #4 (`reviews/pr-1439-check-1.md`): "tools/phone/common.ps1:362, nit." - USB serials are free-form alphanumerics, so `foo` is a valid serial shape; the check exists to refuse loopback, localhost and text with spaces.

Findings applied: high #1-#6 and #8; medium #1, #2, #3, #6; check 1 #1, #2, #3 (#2: the script header already documents exit 2; `tools/CLAUDE.md` is outside Touches and does not mention this script). All other findings are the skips above.

*Open follow-ups from check pass 2 (`reviews/pr-1439-check-2.md`; left open under the convergence rule: second check pass, all nits):*

- [ ] #1 (`reviews/pr-1439-check-2.md`): `reset-dino-language.ps1:232`, a `pidof` error (`pidof: not found` on an older image) counts as "no process", so no broadcast is sent and the script exits 1 with a working app; fall back to sending the broadcast when `pidof` is unavailable.
- [ ] #2 (`reviews/pr-1439-check-2.md`): `Test-PhoneSerialShape` (`tools/phone/common.ps1:110`) rejects bracketed IPv6 `[fe80::1]:5555`, exit 2 in connect/lease/preflight; allow a bracketed host:port.
- [ ] #3 (`reviews/pr-1439-check-2.md`): `ios-sim-store-shots.sh:1160`, `warm_budget` reserves only `UNIT_LIMIT`, not preflight + unit + hierarchy as the other `time_left_for` checks do, so the warm-up can eat the time the first shot needs.
- [ ] #4 (`reviews/pr-1439-check-2.md`): `lease.ps1:59`, `-Clear -Serial X` hits the all-leases cleanup before any phone is resolved. The `-Clear` block now clears every corrupt phone lease file, which includes X's, so only the per-serial message is gone.
- [ ] `tools/emulator/test-reset-dino-language.ps1` is not wired into `tools-selftests.yml` (the workflow is outside this slice's Touches); wire it in a later slice.

#### Changed while building

- `Invoke-EmulatorAdbChecked`, a 1 s sleep before the logcat `-T` stamp, and exit 8 (a named code) when `store-screenshots.ps1` cannot start the emulator, were added from review (high #1, #6; medium #1, #2).
- `ios-sim-*.sh`: `SIM_SETTLE_SECONDS` defaults to 0, Maestro callers pass 20; workflow step budget set to 1080 s; `kill_driver` runs between attempts.
- context7 had no quota; the adb flags were checked against AOSP `logcat.cpp` through `library-docs`.

#### Device run (2026-11-27 17:19Z, all PASS)

- `reset-dino-language.ps1` on emulator-5554 matched `HOOK set-language ok en` on try 1.
- On the S21: `lease.ps1 -Take`, `-Show`, `-Take` by another holder (exit 4), loopback serial (exit 2), `-Clear`, `-Release` (`stay_on_while_plugged_in` back to 7); `connect.ps1` reported USB ready; `preflight.ps1` printed `PREFLIGHT GO`.

#### For CLAUDE.md

- None.

#### Not yet verified

- [cloud] The `ios-sim-*.sh` and `dinoreader-ios-sim.yml` changes (budget guard, `kill_driver`, settle seconds, 1080 s step budget) run on the CI Mac: check the next iOS simulator workflow run.
- [session] `reset-dino-language.ps1` exit 2 path (Dino dev app not installed) has not been run.
- [session] `store-screenshots.ps1` demo-mode early stop and exit 8 on an emulator that will not start have not been run.
