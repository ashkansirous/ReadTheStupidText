# Slice 284 — Leftovers from night 2026-11-06-1250's final feedback loop

**Status: *Built 2026-11-07, merged as PR #913 (c558ca8f); the Windows VM `dino-record` key-R stage has never been run.*** (was: in progress, planned.) The user, 2026-11-07: "284 should be added to the plan." (Un-parked from `parked: feedback loop, waits for the user`.)

*Parked 2026-11-07 by the final feedback loop of night 2026-11-06-1250 (note: `docs/plan/notes/2026-11-06-1250-feedback-loop.md`): `parked: feedback loop, waits for the user`. Repo code found by the loop is never built unreviewed; the user un-parks it.* Un-parked the same day, see above.

**Touches:** `tests/ReadTheStupidText.Tests/FallbackGuardTests.cs`, `tools/desktop-vm/checks/dino-record.ps1`, `tools/desktop-vm/CLAUDE.md`, `tools/emulator/run-flows.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/CLAUDE.md`, `tools/emulator/test-*.ps1`, `.github/workflows/tools-selftests.yml`, `docs/plan/slices/280-*.md`, `src/DinoReader.Core/SegmentSentenceSplitter.cs`, `src/DinoReader.App/StoryVoicing.cs`, `tests/DinoReader.Tests/SpeakerFirstTests.cs`, `.maestro/dino/first-run*.yaml`, `docs/plan/unverified.md`, this file.

**Claim notes (wave 2026-11-07, run-all-waves, no user to ask):** Touches widened from a ground-in-code premise check (items 2-6 named files outside it). Item 4 decision: remove the dead `SplitSegment.SpokenText` (a device voice keeps today's behaviour, no colon lead-in), since nothing reads it. Item 3: the scripts create and remove their own uniquely named `device-test-running-<id>` mark in the worktree's git dir; the agent-side readers live in ClaudeSkills and are not changed here. Item 6: investigate the `first-run` flake from its flow and logs; fix the wait if a cause is found, else record what was read.

## Work

1. **`FallbackGuardTests` misses new, untracked files.** `TrackedSourceFiles` (`tests/ReadTheStupidText.Tests/FallbackGuardTests.cs:271`)
   lists only `git ls-files -- src`, so a new `.cs` file with an unmarked `catch` passes locally and fails in CI once committed
   (night 2026-11-06-1250 wave 2: `ReadingSpeedPreference.cs`, Slice 278 part B, CI fix recorded in
   `docs/plan/slices/278-dino-store-books-only.md`). Fix: also list untracked, not-ignored files
   (`git ls-files --others --exclude-standard -- src`). Verify: add an untracked file under `src/` with a bare `catch`; the test
   fails locally; remove it; the test passes.
2. **Windows VM `dino-record` passes without recording.** Its last key-R stage passes while the microphone-consent prompt
   holds focus, so no recording starts (wave 3, digest-b entries 2153, 2252). Make the stage assert a recording state
   (or dismiss the prompt first). Verify: the VM check fails when no recording starts.
3. **Device-run worktree marks are not enforced.** device-test agents skipped or deleted another run's mark three times
   (digest-b 332, 392, 742) and fixers exited idle on marks four times. Have `tools/emulator/run-flows.ps1` and
   `tools/desktop-vm/Invoke-DesktopTest.ps1` create and remove their own uniquely named mark. Verify: two runs on one
   worktree each remove only their own mark.
4. **Slice 280 dead `SplitSegment.SpokenText`.** check1 finding 6 left it unused, so a device voice no longer gets the
   colon lead-in (digest-b 143, 190). Remove it, or decide a device voice speaks the colon.
5. **Debug builds and production analytics (Slice 280 check3 finding 5).** The unverified.md line under Slice 280 can be
   settled by reading the call path from `DinoTestCommands.cs` to the usage counter against `UsageEligibility.cs`
   (`DebugBuild` reason), then removing the line.
6. **Slice 275 `first-run` flake.** Failed once ("StoryTile not visible within 30 s"), passed on rerun, not investigated
   (digest-b 127). Look at it next to Slice 275 item 4 (the `-NoBuild` cache-key miss).

## Design (two strikes): run marks

The per-run worktree mark (`New-WorktreeRunMark` / `Remove-WorktreeRunMark`, the same two functions in `tools/emulator/run-flows.ps1` and `tools/desktop-vm/Invoke-DesktopTest.ps1`) drew findings in three review passes, so it is one design, not patches. (1) The mark is created as the first statement inside the try whose finally removes it; `$runMark` starts `$null` and the finally removes it if non-null. (2) Every cleanup step in that finally (Stop-Activity, mark removal, Exit-VmLock, crash-evidence copy, env restore) runs in its own try/catch, so one throw never skips the others. (3) A mark is stale only if its pid is dead, or its process StartTime is readable and later than the mark's write time (a reused pid); an unreadable StartTime (access denied) counts as live. Each old mark is judged in its own try, and creating our own mark never depends on reading other processes (only a non-git folder returns `$null`). (4) `tools/emulator/test-worktree-mark.ps1` checks by AST, for both scripts, that the try holding the `New-WorktreeRunMark` assignment as its first statement has a finally calling `Remove-WorktreeRunMark` on the same variable, and that an unreadable-StartTime pid keeps its mark.

## Built (wave 2026-11-07)

Verified: emulator run (key e3c8abd0a5a7): `first-run` flow PASS in 33.5 s; the run-flows mark was created during the run and removed after; a foreign live mark survived. The item 6 flake did not reproduce. Tests: RTST 2153 pass, Dino 3834 pass, tools self-tests pass in pwsh and Windows PowerShell 5.1.

- Item 1: `FallbackGuardTests.TrackedSourceFiles` also lists `git ls-files --others --exclude-standard -- src` (helper `GitListFiles`).
- Item 2: `dino-record` key-R stage now also requires the Stop button (a recording state); trace line alone no longer passes. Not run on the VM.
- Item 3: `run-flows.ps1` and `Invoke-DesktopTest.ps1` create/remove their own `device-test-running-<owner>-<pid>-<id>` mark; self-test `tools/emulator/test-worktree-mark.ps1` (passes in powershell 5.1 and pwsh). The self-test is in `.github/workflows/tools-selftests.yml` and covers both owners, the stale (dead pid) removal and that the remove call sits in a `finally`. A mark whose pid is dead is stale and removed by the next run (documented in `tools/CLAUDE.md`). `Invoke-DesktopTest.ps1` creates its mark only right before the try that removes it.
- Review fixes (high pass): `dino-record` key-R waits for the Stop button instead of one 1 s sleep; `FallbackGuardTests.SourceListing_IncludesAnUntrackedNotIgnoredFile` covers the untracked-file listing; Slice 280's record points here for the removed `SpokenText`. Skipped: the two mark functions stay duplicated in the two scripts (a shared lib would sit in `tools/emulator/common.ps1`, which Slice 308 also edits).
- Item 4 decision, once more: a device voice keeps today's behaviour (no colon lead-in); this is a product decision taken without the user, still open in Slice 280's *From review*.
- Waiting on the user: Should a device voice (no pre-made clip) speak the speaker-first tag with a colon lead-in, as Slice 280 first designed? On main, SpokenText was already unread, so today's behaviour (no colon) is kept; removing the dead property changes nothing on a device.
- Review fixes (medium pass): the key-R stage also passes when the app's microphone-refused alert ("Microphone is off") or the no-microphone alert ("The recording didn't work...") shows, and fails when neither it nor the Stop button shows; a mark is stale when its pid is dead or that process started after the mark was written; the temp-repo `git` calls in the untracked-file test drop `GIT_DIR`, `GIT_INDEX_FILE` and `GIT_WORK_TREE`.
- Item 4: `SplitSegment.SpokenText` removed (nothing read it; a device voice speaks `Segment.Text`, no colon); the two test lines that used it now assert the colon form via `SpeakerFirst.Colon`.
- Item 5: settled by reading, line removed from unverified.md. Path: `ReportPieceFallback` (ReadingSession.BeginBeat) -> `UsageCounter.RecordFallback` -> `UsageQueueSender.Enqueue` -> `PostAsync`, the only post path; `PostAsync` takes the route from `UsageChannels.PathFor(UsageGate.Channel)`; `UsageGate.IsDebug()` is `#if DEBUG` -> `UsageEligibility.CheckBuild` returns `DebugBuild` first, so `Check` is not None and `ChannelOf` is `None` (Beta also needs `CheckBuild` clean) -> path null, batch built but not posted (traced "not sent: DebugBuild"). Unit tests: `UsageEligibilityTests` / `UsageChannelTests` cover debug.
- Item 6: not fixed, no clear cause. Read: after `clearState` and `skip-first-run`, the `StoryTile` waits on `StarterStories.InstallOnceAsync`, which awaits `TestBooks.PendingAsync` (four test books imported one after another through the store client) on a cold emulator; the flow allows 25 s for `GalleryTitle` then 30 s for `StoryTile`. The failed run left no log (no findings file for it) and it passed on rerun, so a slow cold-start import is a likely but unproven cause; a longer wait was not added on a guess. If it recurs, keep the `first-run` Maestro log and the app `HOOK` lines, then check import time. Item 4 of Slice 275 (the `-NoBuild` key miss) is unrelated to this flow: no step of `first-run` builds.

*Changed while building:* item 4 removed the dead property (no colon for a device voice) instead of deciding it with the user; the mark design (above) and the `dino-record` key-R stage (Stop button, or the app's microphone alerts, dismissed after) grew over the review passes. Items 1-6 are recorded above.

*From review:* files `reviews/s284-high.md`, `reviews/s284-medium.md`, `reviews/s284-check1.md`, `reviews/s284-check2.md`, `reviews/s284-check3.md`. Applied: high 1, 2, 3, 5, 6, 8; medium 2, 3, 5, 6 (comment); check1 1-7; check2 1, 3, 4, 8.
- Skipped #4 (`reviews/s284-high.md`): "Treat as: confirm the slice records this as the still-open decision, not as a regression." Reason: removing `SpokenText` changes no behaviour; the open decision is recorded under *Waiting on the user*.
- Skipped #7 (`reviews/s284-high.md`): "A shared dot-sourced lib" would remove the duplication. Reason: the two mark functions stay duplicated, a shared lib would sit in `tools/emulator/common.ps1`, which Slice 308 also edits.
- Skipped #1 (`reviews/s284-medium.md`): "removing SplitSegment.SpokenText drops Slice 280's colon lead-in". Reason: main never read `SpokenText` (premise check: only tests used it), so removing it changes nothing on a device; whether to add a colon lead-in is under *Waiting on the user*.
- Skipped #4 (`reviews/s284-medium.md`): "copy-pasted into Invoke-DesktopTest.ps1 and run-flows.ps1". Reason: duplication accepted, same as high #7.
- Skipped #8 (`reviews/s284-check1.md`): "no longer gets the colon lead-in that Slice 280 set on purpose". Reason: main never read `SpokenText` (premise check: only tests used it), so removing it changes nothing on a device; whether to add a colon lead-in is under *Waiting on the user*.
- Skipped #2 (`reviews/s284-check2.md`): "alert is looked for only after the 8s Stop wait ends". Reason: the order of the alert check only changes wait time, not the verdict.
- Skipped #5 (`reviews/s284-check2.md`): "copy-pasted into two scripts (~28 lines each)". Reason: duplication accepted, same as high #7.
- Skipped #6 (`reviews/s284-check2.md`): "stale check compares process StartTime with mark LastWriteTime". Reason: a run's own mark is always written after its process starts, so only a reused pid can start later, which is the intended stale case.
- Skipped #7 (`reviews/s284-check2.md`): "the verdict depends on the local checkout". Reason: scanning untracked files is item 1's intent.
- Skipped #9 (`reviews/s284-check2.md`): "self-test hard-codes pid 4 as a live process". Reason: pid 4 is the System process on every Windows host.
- Skipped #1-4 (`reviews/s284-check3.md`): "Wait is on the Stop button only, not Stop OR either alert." Findings 1-4 all concern the `dino-record` key-R alert handling, which drew findings in three passes and can only be judged by a VM run; none was possible on the night of 2026-11-07 (7.6 GB free memory, under the 8 GB VM rule). To judge on that run.
- Skipped #5 (`reviews/s284-check3.md`): "duplicated in run-flows.ps1 and Invoke-DesktopTest.ps1". Reason: duplication accepted, as high #7.
- Skipped #6 (`reviews/s284-check3.md`): "Mark has no heartbeat and liveness is judged from pid plus StartTime". Reason: marks older than 90 minutes are already ignored by readers.

#### For CLAUDE.md (lifted 2026-11-07)

- `tools/CLAUDE.md` already holds the stale-mark rule (a mark whose pid is dead is removed by the next run); nothing further to lift. (Slice 284: docs/plan/slices/284-final-loop-leftovers-night-2026-11-06-1250.md)

#### Not yet verified

- Windows VM `dino-record` key-R stage (Stop button or the microphone alert, alert texts read from the host resx, dismiss): never run. Judge check3 findings 1-4 on that run.
