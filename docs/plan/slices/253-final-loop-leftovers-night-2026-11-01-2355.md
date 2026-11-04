### Slice 253 — Final-loop leftovers, night 2026-11-01-2355: device-test harness gaps and two unrun checks

*Parked 2026-11-02 by the final feedback loop of night 2026-11-01-2355 (note: `docs/plan/notes/2026-11-01-2355-feedback-loop.md`): `parked: feedback loop, waits for the user`. Repo code found by the loop is never built unreviewed; the user un-parks it.*

*Planned again 2026-11-03: the user un-parked it ("Yes, un-park both") before a run-all-waves run.*

*Built 2026-11-04, PR #784, not merged. Items 3, 4, 5 built; item 2 built and device-passed; item 1 built, forced-ANR check on the tablet owed; item 6 written, timing not run.*

**Touches:** `tools/emulator/start-warm.ps1`, `tools/emulator/run-flows.ps1`, `tools/CLAUDE.md`, `.maestro/dino/*.yaml`, `tools/desktop-vm/**` (item 3 dry run in the build stage), `tools/emulator/**`, `.github/workflows/tools-selftests.yml` (new self-test steps), `tests/DinoReader.Tests/**`, `docs/plan/slices/253-final-loop-leftovers-night-2026-11-01-2355.md`

#### Work

- [ ] **1. (built, device check owed) The "System UI isn't responding" dialog in the tools, not the briefs.** Five emulator and phone runs were lost to it that night (242 item 6 tablet runs 1-2, 232D tablet, 234B run 1, 234C run 1). The fix (`settings put global hide_error_dialogs 1`, close system dialogs before each flow, one flow per `run-flows.ps1` call) is text in `tools/CLAUDE.md:46` (tablet only) and came only from the coordinator's briefs; `run-flows.ps1:134` dismisses the dialog only after a failure. Put the setting and the pre-flow dialog close into `start-warm.ps1` / `run-flows.ps1` for every AVD, and reconcile `tools/CLAUDE.md:42` ("send all requested flows in one call"). Verify: a flow run on the tablet AVD with a forced ANR dialog passes with no brief text.
- [x] **2. Screenshots in the Dino flows.** 232 C/D/E, 234 A/B/C and 235 A/B were built against design frames, and every "against frames N by eye" check sits in `unverified.md` because no flow takes a screenshot. Add `takeScreenshot` steps at each designed screen and a contact sheet (device shot beside the mock frame) the user can glance at. Verify: one run writes the sheet for the shelf, reading screen and Settings.
- [x] **3. New VM checks meet the VM before the gate.** 235 A and B needed 11 VM runs, most for harness bugs in new `tools/desktop-vm/checks/*.ps1` (expected size larger than the 1024x768 guest, tooltip taken for its button, fixed waits). Give the builder stage a dry run of a new check against the last cached Dino package. Verify: a deliberately wrong expected size fails in the dry run.
- [x] **4. AutomationId set twice, by a test.** "AutomationId may only be set one time" crashed 232D on a device; the rule went into `src/DinoReader.App/CLAUDE.md`, and one wave later the 232E builder repeated it (caught by a reviewer only). Add a source-scan test failing on `AutomationId =` outside a constructor or builder. Verify: the test fails on a copy of the 232E bug.
- [x] **5. Why 247's self-tests passed locally and failed in CI.** The 247 fixer reported every self-test green; CI's `selftests` failed in `test-speed.ps1` (`Test-AppCrashed` not visible in the crash probe's closure). The next fixer reproduced it with the CI command form and did not find why the earlier local runs passed. Find the difference (profile, module scope, the form of the call) and write it in `tools/CLAUDE.md`.

- [~] **6. Make the slow emulator flows faster (the user's ask, twice: "figure out why emulator took 12 minutes", 2026-11-01; "you were building faster emulator tests forever", 2026-11-02 09:55).** Slice 247 only measured. The numbers are in `D:/ReadTheStupidText-worktrees/reviews/s247-timing-2026-11-02.md` (kept by the loop for this item): in `read-back-reread-background` a `repeat times: 3` is 83 s of a 105 s flow; Maestro start-up costs about 11 s per flow; the other four slow flows take 31-48 s, about half of it Wait. Change the flows (shorter repeat, waits on a condition instead of a time, one Maestro session for several flows if it holds) and report before/after seconds per flow. The user was told on 2026-11-02 that the measuring had not run; it had (Slice 247 record, check 1).

#### Not yet verified

- See the Slice 253 block in `docs/plan/unverified.md` (Freddy's name spellings by a native speaker; the parallel illustration workflow's first run).
- Item 1: a forced "System UI isn't responding" dialog on the tablet AVD with the dialog guard, a flow passing with no brief text (not run).
- Item 6: before/after seconds per slow flow (command in the Built block, item 6; not run).
- Item 1: the dialog guard's restore after a lease loss (a killed run leaves `hide_error_dialogs=1`, cleared by the next `run-flows.ps1`; not exercised on a device).

#### Built (fast checks only, 2026-11-04; nothing run on an emulator or VM)

Marks: `[x]` built and fast-checked; `[~]` script changes written, the device run still owed (it comes after review).

- **1 (built, device check owed; unproven).** `tools/emulator/dialog-guard.ps1` (dot-sourced by `speed.ps1`): `run-flows.ps1` sets `hide_error_dialogs 1` for every AVD before the flows, restores the old value (or deletes the setting) in the `finally` on every exit the script controls (the prior value is captured before the change; a process kill cannot be caught, so the next run reads a leftover `1` as "not set" and clears it), and `Wake-Emulator` sends CLOSE_SYSTEM_DIALOGS before each flow; `DIALOGGUARD` log lines. Not put in `start-warm.ps1` on purpose: it also makes the clean snapshot, which would bake the setting in. `tools/CLAUDE.md` line "Send ALL requested flows in one call" now holds for the tablet as well. Self-test `test-dialog-guard.ps1`. Owed on a device: a flow on the tablet AVD with a forced ANR dialog passes with no brief text.
- **2 (written).** `takeScreenshot` in `dino-landscape-shelf` (frames 110, 111), `dino-landscape-reading` (113) and `dino-landscape-settings` (118); `tools/emulator/contact-sheet.ps1` + `screenshot-frames.json` build one HTML page, device shot beside the mock frame (`docs/design/dino-reader/frames/<n>.png` when exported, else the mock file and frame to open: the mock is `.dc.html`, no PNGs exist yet). Self-test `test-contact-sheet.ps1`. Only the shelf, reading and Settings are done; 232 C/D/E, 234 and 235 screens are added the same way (a manifest line + a flow step). Owed: one run, then `contact-sheet.ps1 -Since (Get-Date).AddMinutes(-15)`. Maestro's exact PNG folder is found by searching under `<worktrees>\.tools\maestro-home` and `maestro-out` (docs: files go to the flow's `takeScreenshot` artifact folder); not seen on a disk yet.
- **3 (built).** `tools/desktop-vm/_dryrun.ps1`, called by `Invoke-DesktopTest.ps1` right after the package is found and before the VM starts; self-test `test-check-dryrun.ps1` proves a deliberately wrong expected size (1280 wide) fails and that every existing check passes. It reads the checks (and the MSIX manifest) on the host; it does not run UIA, which needs the guest. Obvious choice: fixed waits of 3 s or more only WARN (existing `rtst-selection-hotkey` has a 5 s one).
- **4 (built).** `tests/DinoReader.Tests/AutomationIdAssignmentTests.cs` scans `src/DinoReader.App`: a statement `x.AutomationId = ...` is allowed in a constructor or on a local the method created; field, `this` and parameter targets fail. One allow-list entry, `BookSheet.QuietRow:row` (every caller passes a fresh row). Proof: `The_scan_fails_on_a_copy_of_the_232E_bug` (a `HostIn(Grid pane)` that sets the id on its parameter) fails the scan with 3 findings. A heuristic line scan, not a Roslyn analysis. `src/DinoReader.App/CLAUDE.md` is outside Touches, so its line 124 was not edited; add "(checked by AutomationIdAssignmentTests)" at the index pass.
- **5 (found, written in `tools/CLAUDE.md`).** The 247 closure bug passes under `-File`, dot-sourcing and a session that loaded `common.ps1`, and fails only under `-Command ./test-speed.ps1`, which is how `shell: pwsh` / `shell: powershell` run a step in `tools-selftests.yml`. Reproduced on the pre-fix file (`git archive 9c07fae0~1`): `pwsh -File` all passed, `pwsh -Command` and `powershell -Command` 2 failed (the probe tests), `powershell -File` passed. Why `-File` differs is not proven (scope of the script's functions seen from a closure's module), only the difference. I did not find out how the 247 fixer called it.
- **6 (written).** `read-back-reread-background`: `repeat` stays at 3 (a cut to 2 was reverted in review: no device data, and the repeat is what shows a leak; the 83 s repeat is about 28 s a round, inferred from the timing note, not measured). Waits already end on a condition (`extendedWaitUntil`); the other four slow flows' waits are readiness waits that passed well under their timeouts, so none was shortened. One Maestro session for several flows was not attempted (it changes how `run-flows.ps1` reads one flow's result and crash). Before/after command (outside this builder's remit, on a device after review): `run-flows.ps1 -App dino -NoBuild -Flow first-run,first-run-pack-start,open-book-voice-id,read-back-reread-background,dino-backup-restore-recordings > tmp/run.log 2>&1`, compare the `PASS <flow> (n s)` lines with `reviews/s247-timing-2026-11-02.md` (read-back-reread-background 105.1 s before).

Evidence of the device run (outside the repo): `D:/ReadTheStupidText-worktrees/reviews/s253-device-run/contact-sheet.html` and `run1.log`.

Device run 2026-11-04 (default AVD RTST_Test, emulator-5554, build d552d3ca193c, 10-minute cap): **item 2 PASS** (shelf 71.5 s, reading 60.6 s, settings 84.6 s all passed; `contact-sheet.ps1 -Since` = 4 screens, 0 without a device shot; PNGs `shot-110/111/113/118` found under `<worktrees>/.tools/maestro-out/i1/.maestro/tests/<timestamp>/<flow name>/takeScreenshot/`). **Item 1 partly proven:** `hide_error_dialogs` was `null` before, set to 1 for the flows (`DIALOGGUARD ... now 1`), restored to `null` after (`settings get` = null); forced-ANR flow run NOT RUN (out of time; tablet AVD not used). **Item 6 NOT RUN** (out of time). Note: a first run started with a stray `&` was left holding the emulator lease and the second run waited on it (`LEASE waiting on run-flows-6560`) until it cleared.

The three new self-tests (`test-dialog-guard.ps1`, `test-contact-sheet.ps1`, `desktop-vm/test-check-dryrun.ps1`) are steps in `.github/workflows/tools-selftests.yml` (both shells). Review fixes (2026-11-04): contact-sheet finds the Maestro folder the way run-flows does; the corner shot is taken after the "voices are ready" note is dismissed; the repeat cut was reverted (measure with the command above first).

Also noted from the device run: it left a stray `run-flows` (stopped), and a killed run leaves `hide_error_dialogs=1` on the AVD until the next `run-flows.ps1` clears it. The dialog-guard set and restore were checked on RTST_Test (PASS).

From review (files in `D:/ReadTheStupidText-worktrees/reviews/`):

`s253-high.md` (9 findings):
- 1 (self-tests not CI steps): applied, the three self-tests are steps in `tools-selftests.yml`.
- 2 (contact-sheet shot root differs from run-flows): applied, it finds the Maestro folder the way run-flows does.
- 3 (corner shot before the voices-ready dialog is dismissed): applied, the shot moved after the dismiss.
- 4 (repeat cut 3 to 2 without data): applied as a revert, `repeat` stays 3.
- 5 (item 1 ticked without the forced-ANR verify): applied, item 1 marked built with the device check owed.
- 6 (prior value assigned inside the try; kill leaves the setting): applied (Built block: prior value captured before the change, restore in `finally`, a leftover `1` cleared by the next run).
- 7 (`_dryrun.ps1` oversized-size rule matches one literal line shape, disabled by any clamp word): skipped, low: a host-side heuristic meant to catch the plain case (the wrong-size proof test passes); a full parser is not worth it for a pre-VM check.
- 8 (`_dryrun.ps1` fixed-wait scan misses `Start-Sleep 5`, `-s`, variables): skipped, low (fixed waits only WARN).
- 9 (AutomationId scan is a line heuristic, scans only `src/DinoReader.App`): skipped, low (the Built block calls it a heuristic; the 232E copy fails the scan).

`s253-medium.md` (8 findings):
- 1 (PNG folder unproven): skipped, proven by the device run (PNGs found under `.tools/maestro-out/i1/.maestro/tests/<ts>/<flow>/takeScreenshot/`).
- 2 (second emulator of `Start-SecondHalf` unguarded): applied as no change plus a comment.
- 3 (prior value `1` deleted at run end): skipped (reason not recorded by the builder; the Built block says a leftover `1` is read as "not set").
- 4 (restore after lease loss, `Restore-DialogGuard` does not check the lease): applied.
- 5 (`_dryrun.ps1` Get-Content without Test-Path): applied.
- 6 (size scan only exact `$xWidth = 1280` lines): skipped, same as high 7.
- 7 (EnclosingMember line heuristic): skipped, same as high 9.
- 8 (misplaced comment above Get-MaestroToolsDir): applied.

`s253-check-medium.md` (8 findings, all low or medium, none applied): 1 PNG location, now proven by the device run; 2 size check one line shape; 3 UIA lookup flagged only on one line; 4 defaulted parameters accepted as local; 5 one-tab indent assumed; 6 pre-existing `hide_error_dialogs=1` deleted; 7 killed second-half run leaves 5556 at 1 until the next run; 8 Close-SystemDialogs closes any system surface.

#### For CLAUDE.md

- Keep what the builder wrote in `tools/CLAUDE.md` (dialog guard, all flows in one call, `-Command` versus `-File` self-test difference). (Slice 253: docs/plan/slices/253-final-loop-leftovers-night-2026-11-01-2355.md)
- `src/DinoReader.App/CLAUDE.md`, on the AutomationId rule (line 124): add "checked by `AutomationIdAssignmentTests`" (`tests/DinoReader.Tests/AutomationIdAssignmentTests.cs`). (Slice 253: docs/plan/slices/253-final-loop-leftovers-night-2026-11-01-2355.md)
