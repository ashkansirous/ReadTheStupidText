# Slice 304 — Slice 202: rerun the medium review on `ReaderPage` as merged, test the `InitWait`-before-text order
**Product:** RTST

*Built 2026-11-07 (wave 2026-11-07, night 2026-11-07-1412), merged as PR #908 (7acd2e1b). Emulator run 2026-11-07 (key 16293c9a70b0): paste-and-read, language-continue-two-step, set-language and play-before-detection PASS; the rest is under Not yet verified.* Planned 2026-11-07. [Slice 202](202-reader-language-change-crash.md) merged on an incomplete second review pass (the forked review never finished). Slice 213's unreviewed half is Slice 216 item 9. Also: the `InitWait`-before-text order is kept only by a comment (`ReaderPage.xaml.cs:76` calls `InitWait()`), with no test. Moved from plan.md's *Outstanding work*.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs` (review fixes), `src/ReadTheStupidText.Mobile/ReaderOpenOrder.cs` (new pure type, compiled into the tests via a linked Compile item), `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/304-slice-202-rereview-and-initwait-test.md`

## Work

- [x] 1. Run `code-review-medium` (worktree path, narrow brief naming `ReaderPage*.cs` as merged) over the merged state of Slice 202; write the findings file under `<worktree parent>/reviews/`. Triage and apply what is correctness, crash or loading-state.
- [x] 2. Make the order testable: move the "wait view is set up before the text is shown" rule into a small pure type or a method with a seam, and add a test that fails when text is shown before `InitWait`. Android and iOS share `ReaderPage`, so one test serves both.

## Verification

- The findings file exists and each finding is applied or skipped with a reason from the file; the order test fails when the two calls are swapped.

## Built

- Item 2: `ReaderOpenOrder.Open(initWait, showText)` is a static pure method; the `ReaderPage` constructor calls `InitWait` and the first text set only through it. `ReaderOpenOrderTests` records the call order and asserts `initWait` ran first and `showText` once. The test guards the type, not the constructor line: a constructor that stops calling `Open` is not caught (a MAUI page cannot be built in the unit tests).
- Item 1 (medium re-review of merged `ReaderPage*.cs`, findings: `reviews/s304-202-medium.md`): applied 1 (late detection failure on a retired or superseded page returns early), 2 (`PrepareForRebuild` cancels the page's own `_headStartCts` and `_languageRefreshCts`), 4 and 5 (a language picked in Review holds until the text is edited; detection does not overwrite it for the replaced text; any edit clears it with or without a detector), 7 (`OnReaderStallChanged` checks `_retired`).
- Skipped: 3 (cold first Play still shows the overlay: performance, not correctness), 8 (wasted work only, no wrong result), 6 (`ISpeechReader` exposes no stalled state, so there is nothing to restore; a user would call the missing overlay wrong, so it needs a follow-up fix that adds a stalled state, see Not yet verified), 9 (the rebuild caller is not in this slice's files).

## Not yet verified

- Review-picked language holds after Review's Read, and an edit clears it (device).
- Scan or import of the same text keeps the language (device).
- Language-change rebuild shows a loading state when it takes over 500 ms (review 9; caller in Settings/AppShell, not read by this slice).
- Stalled-read overlay is not restored after leaving and returning (review 6): `ISpeechReader` exposes no stalled state (only the `StallChanged` event). A user would call this wrong, so it needs a follow-up fix that adds a stalled state.
- No scripted flow yet for Review-pick-then-Read.

## Review notes (high pass on the first build, `reviews/s304-high.md`)

Findings files are under `D:/ReadTheStupidText-worktrees/reviews/`. The fixer's per-finding triage for the high pass was not in the brief; what the built code shows:

- Review 1 and 3 (separate `WaitReady()` flag, runtime throw): addressed by the redesign. `ReaderOpenOrder.Open(initWait, showText)` takes both steps, with no flag and no throw (`ReaderOpenOrder.cs:11`, called at `ReaderPage.xaml.cs:88`).
- Review 2 (tests cover the helper, not the constructor): not fixed, a known limit, stated under Built (a MAUI page cannot be built in the unit tests). The Verification line holds for the helper only. Same point as medium 3 and check 4.
- Review 4 (simplicity, closure and new type): applied in part by the same redesign: the instance class, its flag and the separate calls are gone; one static `Open` remains so the order has a tested owner.
- Review 5 (item 1 not done): applied; item 1 was run and ticked, see Built.

## Review notes (medium pass on the built slice, `reviews/s304-medium.md`)

- Review 1: applied. `ReplaceEditorText` runs the language refresh (`ScheduleReadingLanguageRefresh`, `OnReadingLanguageMayHaveChanged`) itself when the new text equals the editor text, since no `TextChanged` fires then.
- Review 2: skipped. Cancellation is cooperative and the CTS has no timer to dispose.
- Review 3: skipped. Known limit, already recorded in this file.
- Review 4: skipped. The text is set before the `Init*` calls on origin/main too; not new.

Second medium pass (`reviews/s304-check1-medium.md`):

- Check 1 and 2: applied together. In `ReplaceEditorText`, equal text with no picked language changes nothing about the language (as on origin/main) and only restarts the head start; equal text with a pick sets the pick and schedules the language refresh once, with one head-start restart.
- Check 3: skipped. Cooperative cancel, already triaged.
- Check 4: skipped. Known limit: the test proves the helper's order, not that the `ReaderPage` constructor uses it.

Third medium pass (`reviews/s304-check2-medium.md`):

- Check 1, 3 and 4: skipped, repeats of earlier findings (init order unconfirmed, CTS not disposed, helper-only test).
- Check 2: skipped, low. In the equal-text-with-pick path the head start uses the new key at once while the debounced language refresh follows.
- Check 5: skipped, low. The `_retired` early return in `OnReaderStallChanged` also skips the stall-end branch, so a retired page's overlay is not cleared; only matters if a retired page can be shown again.
