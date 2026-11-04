### Slice 253 — Final-loop leftovers, night 2026-11-01-2355: device-test harness gaps and two unrun checks

*Parked 2026-11-02 by the final feedback loop of night 2026-11-01-2355 (note: `docs/plan/notes/2026-11-01-2355-feedback-loop.md`): `parked: feedback loop, waits for the user`. Repo code found by the loop is never built unreviewed; the user un-parks it.*

*Planned again 2026-11-03: the user un-parked it ("Yes, un-park both") before a run-all-waves run.*

*In progress (wave 2026-11-04), draft PR.*

**Touches:** `tools/emulator/start-warm.ps1`, `tools/emulator/run-flows.ps1`, `tools/CLAUDE.md`, `.maestro/dino/*.yaml`, `tools/desktop-vm/**` (item 3 dry run in the build stage), `tools/emulator/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/253-final-loop-leftovers-night-2026-11-01-2355.md`

#### Work

- [ ] **1. The "System UI isn't responding" dialog in the tools, not the briefs.** Five emulator and phone runs were lost to it that night (242 item 6 tablet runs 1-2, 232D tablet, 234B run 1, 234C run 1). The fix (`settings put global hide_error_dialogs 1`, close system dialogs before each flow, one flow per `run-flows.ps1` call) is text in `tools/CLAUDE.md:46` (tablet only) and came only from the coordinator's briefs; `run-flows.ps1:134` dismisses the dialog only after a failure. Put the setting and the pre-flow dialog close into `start-warm.ps1` / `run-flows.ps1` for every AVD, and reconcile `tools/CLAUDE.md:42` ("send all requested flows in one call"). Verify: a flow run on the tablet AVD with a forced ANR dialog passes with no brief text.
- [ ] **2. Screenshots in the Dino flows.** 232 C/D/E, 234 A/B/C and 235 A/B were built against design frames, and every "against frames N by eye" check sits in `unverified.md` because no flow takes a screenshot. Add `takeScreenshot` steps at each designed screen and a contact sheet (device shot beside the mock frame) the user can glance at. Verify: one run writes the sheet for the shelf, reading screen and Settings.
- [ ] **3. New VM checks meet the VM before the gate.** 235 A and B needed 11 VM runs, most for harness bugs in new `tools/desktop-vm/checks/*.ps1` (expected size larger than the 1024x768 guest, tooltip taken for its button, fixed waits). Give the builder stage a dry run of a new check against the last cached Dino package. Verify: a deliberately wrong expected size fails in the dry run.
- [ ] **4. AutomationId set twice, by a test.** "AutomationId may only be set one time" crashed 232D on a device; the rule went into `src/DinoReader.App/CLAUDE.md`, and one wave later the 232E builder repeated it (caught by a reviewer only). Add a source-scan test failing on `AutomationId =` outside a constructor or builder. Verify: the test fails on a copy of the 232E bug.
- [ ] **5. Why 247's self-tests passed locally and failed in CI.** The 247 fixer reported every self-test green; CI's `selftests` failed in `test-speed.ps1` (`Test-AppCrashed` not visible in the crash probe's closure). The next fixer reproduced it with the CI command form and did not find why the earlier local runs passed. Find the difference (profile, module scope, the form of the call) and write it in `tools/CLAUDE.md`.

- [ ] **6. Make the slow emulator flows faster (the user's ask, twice: "figure out why emulator took 12 minutes", 2026-11-01; "you were building faster emulator tests forever", 2026-11-02 09:55).** Slice 247 only measured. The numbers are in `D:/ReadTheStupidText-worktrees/reviews/s247-timing-2026-11-02.md` (kept by the loop for this item): in `read-back-reread-background` a `repeat times: 3` is 83 s of a 105 s flow; Maestro start-up costs about 11 s per flow; the other four slow flows take 31-48 s, about half of it Wait. Change the flows (shorter repeat, waits on a condition instead of a time, one Maestro session for several flows if it holds) and report before/after seconds per flow. The user was told on 2026-11-02 that the measuring had not run; it had (Slice 247 record, check 1).

#### Not yet verified

- See the Slice 253 block in `docs/plan/unverified.md` (Freddy's name spellings by a native speaker; the parallel illustration workflow's first run).
