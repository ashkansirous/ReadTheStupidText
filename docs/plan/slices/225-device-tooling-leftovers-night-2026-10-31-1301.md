### Slice 225 — Device-test tooling leftovers from the 2026-10-31-1301 night, round 1

*Planned.* Written 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1301-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01.

**Touches:** `tools/phone/common.ps1`, `tools/phone/run-checks.ps1`, `tools/phone/preflight.ps1`, `tools/phone/restore-device-state.ps1` (new), `tools/phone/checks/_lib.ps1`, `tools/phone/checks/*.ps1`, `tools/phone/test-phone-lib.ps1`, `tools/phone/test-run-checks.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/common.ps1`, `tools/emulator/lease.ps1`, `tools/CLAUDE.md`, `tools/phone/README.md`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `tests/ReadTheStupidText.Tests/` (a new DecideAll test file), `tools/phone/lease.ps1`, `tools/phone/checks/paused-notification.ps1`, `tools/emulator/lint-flows.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `.gitattributes`, `.github/workflows/tools-selftests.yml`, `src/DinoReader.App/StagedBookUpdates.cs`, `src/DinoReader.App/StarterStories.cs`, `tests/DinoReader.Tests/`, `plan.md`, `docs/plan/slices/225-device-tooling-leftovers-night-2026-10-31-1301.md`

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

10. **No self-test for `run-checks.ps1 -FirstText`** (Slice 214 medium 8). Corrected by the final feedback loop (pass 3b, B3): the forwarding is already on main (`tools/phone/run-checks.ps1` declares `-FirstText` and passes it as `-Text`; `checks/cold-start.ps1` warns that `-Preset`/`-Text` do not apply to Dino). The earlier "confirmed open" line was wrong; only the test is missing.
   - [ ] A self-test in `test-run-checks.ps1` passes `-FirstText "x"` and sees it in the check's arguments.

11. **Nothing gives the phone its screen timeout back after a run** (final feedback loop, audit pass 1 item 9; supervisor session entry 1849, the user: "why is my phone's screen on while you are not testing anything on it?"). The pre-flight needs `stay_on_while_plugged_in` on (`tools/phone/preflight.ps1 -Fix` sets 7) and nothing switches it back, so the screen stays on through the run and after it. Likely the cause, not proven: the value was already 7 at the first pre-flight, so who set it is unknown. The answer at the time was a guess ("most likely") and asked the user to change it by hand.
   - [ ] `preflight.ps1` records the value it found in the night's `preflight.md`; releasing the phone lease at the end of a run (`tools/phone/lease.ps1 -Release`, last holder) restores that value and turns the screen off (`input keyevent 26` only if the screen is on). Verify: a self-test with a fake adb sees the restore call with the recorded value.

12. **A stale `dev.mobile.maestro` driver breaks `uiautomator dump` on the emulator** ("Killed"; final feedback loop pass 3b, B6, from the killed wave session 644eaf2a): the Slice 205 emulator agent had to force-stop it, and that check ended without its TalkBack step partly because of it. Nothing documents it.
   - [ ] The dump helper (and `run-flows.ps1` before a dump) force-stops `dev.mobile.maestro` first; one line in `tools/CLAUDE.md`. Verify: a self-test with a fake adb sees the force-stop before the dump.

13. **Two Slice 215 review findings no item owns** (pass 3b, A4; `docs/plan/slices/215-device-guard-gaps.md` "Not fixed (outside Touches)"): medium 8, `lint-flows.ps1` `Test-LineEndings` depends on autocrlf; and high 10, an empty `elseif` branch in `Invoke-DesktopTest.ps1` makes the skip message print from whichever pre-flight call comes first.
   - [ ] A `.gitattributes` line pins `*.yaml` line endings; the `elseif` gets its message. Verify: `lint-flows.ps1` passes with `core.autocrlf` true and false.

14. **The wireless-cut guard has no caller** (final feedback loop pass 3a, item 1): `Assert-WholeDeviceNetworkCutAllowed` (`tools/phone/common.ps1`) is defined and self-tested, but no check or runner calls it; `tools/CLAUDE.md`, the phone README and the user were told `tools/phone/` refuses such cuts. Only the ClaudeSkills hook and one check's own grep stand in the way.
   - [ ] Fix the comment above it in `common.ps1` that says such cuts are refused. Call it from the shared helper every check uses before a device-wide change (or from `run-checks.ps1` for checks that declare one), and add a self-test where a fake check asks for a whole-device cut on an ip:port serial and is refused. Verify: `test-phone-lib.ps1` and `test-run-checks.ps1` pass.

15. **Slice 208's reworked `paused-notification` check, edited after both reviews and merged unread** (final feedback loop: the late-edit review, `reviews/late-edits-208-209-214.md` in the worktrees folder, local only). It can no longer cut the adb link, but: (1, medium, unproven) `cmd netpolicy add restrict-background-blacklist` likely blocks only on metered networks, so on unmetered Wi-Fi the check may never see a pause (`checks/paused-notification.ps1` near line 122, `common.ps1` near 647); (2) the 240 s detached heal is never cancelled and can undo a slow run's or a rerun's settings; (3) if `Get-PackageUid` throws, the finally block runs `netpolicy remove` for uid 0; (4) the heal does not move the pack back, so the README and Slice 208 overstate it.
   - [ ] Prove (1) on an emulator with an unmetered network, or switch to a block that works there; cancel the heal when the check ends; skip the remove when no uid was found; make the heal restore the pack or fix the docs. Verify: `test-phone-lib.ps1` self-tests for 2-4; 1 on the emulator.

16. **Slice 209 Debug-only hook leftovers** (same review, low): `src/DinoReader.App/StagedBookUpdates.cs` near line 96 does not restore the lowered recorded version when the forced entry is dropped; `StarterStories.cs` near line 143, the force-apply mark can outlive a failed stage and force a later ordinary update in a Debug build.
   - [ ] Restore the version on drop and clear the mark when staging fails. Verify: unit tests in `tests/DinoReader.Tests` (or the Mobile test project) for both.

17. **No PowerShell lint** (final feedback loop pass 4b, from the killed session fa5dbe0d): the Slice 216 builder reported "PSScriptAnalyzer not installed, lint NOT RUN" for its scripts, and the repo has no PowerShell lint anywhere, though `tools/` is mostly PowerShell.
   - [ ] Run PSScriptAnalyzer (installed on demand) over `tools/**/*.ps1` in `tools-selftests`, with a settings file that starts from the current warnings as a baseline. Verify: the job fails on a new error-level finding.

18. **A Dino test run exited 2 with 0 failed** (pass 4b, same session): `dotnet test` printed "non-success exit code 2" while reporting no failed test; the cause was never found.
   - [ ] Rerun `dotnet test tests/DinoReader.Tests` and read the exit reason (a crashed test host, a skipped project or a warning treated as error); fix it or record it. Verify: the run exits 0 when every test passes.

#### Verification

- The verify line under each item; items 1-5, 7-10, 11 (fake adb), 12 (fake adb), 13, 14, 16, 17 and 18 need no phone; 15 needs an emulator.

#### Not yet verified

- Everything: planned only.
