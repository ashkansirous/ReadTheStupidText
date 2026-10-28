# Slice 176 — Reading text that is only spaces must not throw

*Built 2026-10-28, not yet verified on the user's phone or an iOS run.* Batch 38 (test gaps found 2026-10-28), both apps, Android and iOS (and the Windows reader, if it
shares the cause).

**Touches:** `src/ReadTheStupidText.Infrastructure/**/CompositeSpeechReader.cs` (Windows composite, added while building), `src/ReadTheStupidText.Application/**` except `Reading/ReadTimingTracker.cs` (the read path and
`SpeechTextChunker`, where the whitespace guard belongs), `src/ReadTheStupidText.Mobile.Shared/**` except `Testing/**`
and `Platforms/*/Testing/**` (the neural readers, only if the throw is there), `tests/ReadTheStupidText.Tests/**`,
`tests/DinoReader.Tests/**` (new test files only, not the shared `*Texts.cs` or locale JSON)

## Why

On the user's phone (2026-10-28), a read of `'   '` (spaces only) through the Debug `read` hook logged
`HOOK read start`, `ok`, then `HOOK read fault NullReferenceException`. The app survived, but a user who copies a
blank line and presses Read gets an error instead of nothing. Empty text is already refused (`HOOK read failed`);
text that is only whitespace slips past that check and fails further in.

## Work

- [x] Unit test first: a read of whitespace-only text (spaces, tabs, newlines, non-breaking space) ends as
  "nothing to read" with no exception, through the shared read path both apps use.
- [x] Fix where the text enters the read path (one guard, shared), not in each reader; the reading state goes back
  to idle with no error. Same on Android and iOS because the path is shared; check whether the Windows tray read
  has the same hole and cover it with the same guard.
- [x] Phone: the `read text='   '` hook ends in `HOOK read ok` with no fault and the app idle.

## Verification

- `dotnet test` for both suites, the new tests included.
- Phone over wireless adb: whitespace-only, tab-only and newline-only reads log no fault.
- iOS: the shared fix compiles in the iOS build; desktop: a whitespace-only hotkey read in the VM leaves no error
  row.
</content>

## Built

*Cause:* `SpeechTextChunker.Split` returned one empty chunk `[""]` for whitespace-only text and the Supertonic readers passed it to the engine.

*Changed while building:* `Application/Reading/BlankText.cs` (`IsBlank`: whitespace plus Unicode format/control characters; `IsInvisible`). The chunker returns no chunks for blank text and drops invisible-only paragraphs and sentences (ranges kept right); `SpeechChunkPlan.ForSegments`. Both `CompositeSpeechReader` classes (Mobile.Shared and Infrastructure) end a blank speak by stopping the active read and raising Idle + Completed without reaching an engine. `ReadAloudService`: a blank selection calls `CancelPendingAutoRead` (only a pending auto/clipboard entry, never a read already speaking). `BlankText` is used in the Android/iOS head-start guards and `ReaderPage.EarlyStart`. Touches grew to the Windows composite.

*From review:* review 2 tried a silent no-op for a blank speak and reverted it, because Dino's `ReadingSession` waits on `Completed`. Reviews: `reviews/176-review1.md`, `176-review2.md`.

*Verified:* RTST 1480 and Dino 1642 tests pass; Android, iOS (compile) and Windows App builds; emulator: blank, tab, newline and zero-width-space hook reads log no fault and end Idle; a blank read mid-read; a Dino read from the last sentence returns to play.

#### Not yet verified

- The user's phone (disconnected): the `read text='   '` hook.
- iOS run (no Mac); the iOS change is compile-verified only.
- The two `CompositeSpeechReader` classes have no unit tests (they need MAUI Preferences / real engines).

#### For CLAUDE.md (lifted 2026-10-28)

- Application (`src/ReadTheStupidText.Application/Reading/`): blank text is `BlankText.IsBlank` (whitespace plus format/control characters), never `string.IsNullOrWhiteSpace`; a blank speak on a `CompositeSpeechReader` still raises `Completed`. (Slice 176: docs/plan/slices/176-whitespace-read-crash.md)
