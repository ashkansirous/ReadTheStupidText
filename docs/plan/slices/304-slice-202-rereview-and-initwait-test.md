# Slice 304 — Slice 202: rerun the medium review on `ReaderPage` as merged, test the `InitWait`-before-text order

*Planned 2026-11-07.* [Slice 202](202-reader-language-change-crash.md) merged on an incomplete second review pass (the forked review never finished). Slice 213's unreviewed half is Slice 216 item 9. Also: the `InitWait`-before-text order is kept only by a comment (`ReaderPage.xaml.cs:76` calls `InitWait()`), with no test. Moved from plan.md's *Outstanding work*.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/304-slice-202-rereview-and-initwait-test.md`

## Work

- [ ] 1. Run `code-review-medium` (worktree path, narrow brief naming `ReaderPage*.cs` as merged) over the merged state of Slice 202; write the findings file under `<worktree parent>/reviews/`. Triage and apply what is correctness, crash or loading-state.
- [ ] 2. Make the order testable: move the "wait view is set up before the text is shown" rule into a small pure type or a method with a seam, and add a test that fails when text is shown before `InitWait`. Android and iOS share `ReaderPage`, so one test serves both.

## Verification

- The findings file exists and each finding is applied or skipped with a reason from the file; the order test fails when the two calls are swapped.
