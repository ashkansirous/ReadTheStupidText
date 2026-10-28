# Slice 176 — Reading text that is only spaces must not throw

*In progress (wave 2026-10-28b), draft PR.* Batch 38 (test gaps found 2026-10-28), both apps, Android and iOS (and the Windows reader, if it
shares the cause).

**Touches:** `src/ReadTheStupidText.Application/**` except `Reading/ReadTimingTracker.cs` (the read path and
`SpeechTextChunker`, where the whitespace guard belongs), `src/ReadTheStupidText.Mobile.Shared/**` except `Testing/**`
and `Platforms/*/Testing/**` (the neural readers, only if the throw is there), `tests/ReadTheStupidText.Tests/**`,
`tests/DinoReader.Tests/**` (new test files only, not the shared `*Texts.cs` or locale JSON)

## Why

On the user's phone (2026-10-28), a read of `'   '` (spaces only) through the Debug `read` hook logged
`HOOK read start`, `ok`, then `HOOK read fault NullReferenceException`. The app survived, but a user who copies a
blank line and presses Read gets an error instead of nothing. Empty text is already refused (`HOOK read failed`);
text that is only whitespace slips past that check and fails further in.

## Work

- [ ] Unit test first: a read of whitespace-only text (spaces, tabs, newlines, non-breaking space) ends as
  "nothing to read" with no exception, through the shared read path both apps use.
- [ ] Fix where the text enters the read path (one guard, shared), not in each reader; the reading state goes back
  to idle with no error. Same on Android and iOS because the path is shared; check whether the Windows tray read
  has the same hole and cover it with the same guard.
- [ ] Phone: the `read text='   '` hook ends in `HOOK read ok` with no fault and the app idle.

## Verification

- `dotnet test` for both suites, the new tests included.
- Phone over wireless adb: whitespace-only, tab-only and newline-only reads log no fault.
- iOS: the shared fix compiles in the iOS build; desktop: a whitespace-only hotkey read in the VM leaves no error
  row.
</content>
