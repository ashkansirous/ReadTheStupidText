# Slice 284 — Leftovers from night 2026-11-06-1250's final feedback loop

**Status: planned.** The user, 2026-11-07: "284 should be added to the plan." (Un-parked from `parked: feedback loop, waits for the user`.)

*Parked 2026-11-07 by the final feedback loop of night 2026-11-06-1250 (note: `docs/plan/notes/2026-11-06-1250-feedback-loop.md`): `parked: feedback loop, waits for the user`. Repo code found by the loop is never built unreviewed; the user un-parks it.* Un-parked the same day, see above.

**Touches:** `tests/ReadTheStupidText.Tests/FallbackGuardTests.cs`, this file.

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
