### Slice 225 — Device-test tooling leftovers from the 2026-10-31-1301 night, round 1

*parked: feedback loop, waits for the user.* Planned 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1301-feedback-loop-w1.md)). Parked so no later wave tonight builds it; the user moves it to `planned`.

**Touches:** `tools/phone/common.ps1`, `tools/phone/run-checks.ps1`, `tools/phone/preflight.ps1`, `tools/phone/restore-device-state.ps1` (new), `tools/phone/checks/_lib.ps1`, `tools/phone/checks/*.ps1`, `tools/phone/test-phone-lib.ps1`, `tools/phone/test-run-checks.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/common.ps1`, `tools/emulator/lease.ps1`, `tools/CLAUDE.md`, `tools/phone/README.md`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `tests/ReadTheStupidText.Tests/` (a new DecideAll test file), `plan.md`, `docs/plan/slices/225-device-tooling-leftovers-night-2026-10-31-1301.md`

Item numbers in brackets are the pass-1 audit items in the note; "entry" numbers are the wave-1 digest.

#### Work

1. **The S21 restore is handed to the user as three adb commands** [5] (plan.md *Waiting on the user*; wave-1 final message): once the phone is reachable a script can do them. Only "turn Wi-Fi and mobile data on" needs the user.
   - [ ] `tools/phone/restore-device-state.ps1` (called by `preflight.ps1`): detects and clears a leftover `device_config activity_manager data_sync_fgs_timeout_duration`, the `FGS_INTRODUCE_TIME_LIMITS` compat flag on the dev package and any `restrict-background-blacklist` entry, then runs `reset-dev-app.ps1 -Level auto`. Verify: a self-test with a faked adb that reports each leftover expects the matching clear command; then shrink the plan.md line to the one human step.
2. **Check scripts can change device-wide state without a screen** [3] (entry 652: the brief knew the check changed `data_sync_fgs_timeout_duration` and only said "make sure it resets"). The ClaudeSkills rule now says it; make it enforceable here.
   - [ ] A self-test in `test-run-checks.ps1` that greps every `tools/phone/checks/*.ps1` for `svc wifi|data`, airplane mode, `device_config put`, `am compat enable`, `settings put global`, `cmd netpolicy` and fails unless the same file calls `Start-DeviceRestore` before the first one. Verify: the test fails on a fixture check without the restore and passes on `paused-notification.ps1`.
3. **`run-checks.ps1` has no emulator path** [7] (wave-1.md; the emulator agent's hand-back, raw transcript line 924): `Find-WirelessPhone` (`common.ps1`) accepts wireless serials only, and the emulator's RTST dev install is `uk.sirous.readthestupidtext` while the phone scripts need `.dbg`. So "prove on an emulator first" (Slice 208 *Not yet verified*) cannot be done as written.
   - [ ] `run-checks.ps1 -Serial emulator-NNNN` runs the same checks on an emulator, with one RTST dev package id on both (coordinate with Slice 221 item 2, the Dino id). Verify: `paused-notification` runs on emulator-5554 and reports PASS or FAIL, not "no phone".
4. **`run-flows.ps1` says "not installed" when both the store and `.dbg` packages are installed** [16] (wave-1-notes.md): `tools/emulator/run-flows.ps1:288` passes `"$installed"` (two `pm list` lines joined by a space) to `Resolve-InstallProblem`, whose `(?m)^package:...\s*$` regex then cannot match. Slice 221 item 2 removes the trigger for Dino, not the bug.
   - [ ] Pass the lines joined with newlines (or match per line). Verify: a self-test gives two `package:` lines and expects "installed".
   - [ ] `run-flows.ps1 -Flow <name>` also finds flows under `needs-file/` (wave-1-notes.md; `run-flows.ps1:292-294` looks only in `.maestro/<app>/*.yaml`). Verify: `-Flow` with a needs-file flow name resolves it.
5. **`.phone/run-status.json` and `build-*.json` are shared across checkouts** [17] (entries 332, 339, 344): the 10-minute check read the s209 stalled Debug build as the s214 run's, and the coordinator interrupted a healthy agent.
   - [ ] Each status file records the checkout path and run id, and the runner prints which checkout a status belongs to (or the files are keyed by checkout). Verify: a self-test with two checkouts' status files expects each run to read only its own.
6. **The runner's Debug builds failed or stalled repeatedly** [18] (entries 249, 494, 630: "stalled: log not grown 5 min", "dino debug failed"); runs fell back to the cached AOT build and nobody read `build-dino-debug.log` / `build-rtst-debug.log`. Slice 224 item 1 covers only the false stall after a finished build.
   - [ ] Read those logs from the next failure (they are overwritten per build, so capture one), find the cause, and fix it or record it; until then each result line names the build variant it ran on. Verify: the cause is written in this file with the log lines.
7. **One shared check catalogue** [29] (entries 975-995): `tools/phone/checks/_lib.ps1` `$script:Catalogue` is one list that Slices 208, 209 and 214 all appended to; 208's merge hit a code conflict there.
   - [ ] One catalogue entry per check file (a header block in each `checks/<name>.ps1`, loaded by the runner). Verify: the runner lists the same checks as before; adding a check touches only its own file.
8. **The emulator lease lets one owner start two runs** [28] (entry 232: two Dino emulator runs started in parallel by one agent). Slice 221 item 6 covers the phone lease only.
   - [ ] The emulator lease refuses a second take by the same owner while a run is live. Verify: a self-test takes it twice and expects the second to fail.

9. **Slice 208's `DecideAll` rewrite has no unit test** [pass 2, N4] (entry 280: "the change to DecideAll is private and untested"). It decides which half-finished packs are deleted at launch.
   - [ ] Unit tests for the selection: a wanted pack's partial is never deleted (any wanted language), an unselected same-language partial is deleted, translation and storyteller partials are kept. Verify: the test fails when the OCR keep rule is removed. Touches `src/ReadTheStupidText.Mobile.Shared` (the service) and `tests/`.

10. **`run-checks.ps1` does not forward `-Text` to `cold-start.ps1`** (Slice 214 medium 8, confirmed open by pass 3): only `-Preset` (as `-FirstRead`) and `-DropCaches` reach the check, and for Dino `-Preset`/`-Text` are overwritten with the "book page" label without a warning.
   - [ ] Forward `-Text` (or refuse it with a message), and warn when Dino overrides the preset. Verify: a self-test in `test-run-checks.ps1` passes `-Text "x"` and sees it in the check's arguments.

#### Verification

- The verify line under each item; items 1-5 and 7-10 need no phone.

#### Not yet verified

- Everything: planned only.
