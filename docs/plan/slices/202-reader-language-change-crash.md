# Slice 202: changing the app language with text in the reader crashes the app

*Built (fast checks green; device check owed).*

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `tests/ReadTheStupidText.Tests/**`.

## Problem (real S21, Android 15, 2026-10-30)

With text in the RTST phone reader (e.g. "Hello there."), changing the app language in Settings crashes the app:
`NullReferenceException` in `ReaderPage.get_PlayerDimmedByWait` <- `RenderEmptyPlayer` <- `RenderReading` <- `OnEditorTextChanged`.
Reproduced twice (fa, then sv); switching with an empty reader is fine. Logs:
`D:\ReadTheStupidText-worktrees\.phone\s21night4-farsi-crash.log`, `s21night4-lang-change-crash.log`.

Likely cause: `ReaderPage.Wait.cs:76` reads `_waitOverlay`/`_wait` (declared `null!`, created in `InitWait`, line 59-63) when the
page is rebuilt for the new language and the restored text fires `OnEditorTextChanged` before `InitWait` has run.

## Fix

- Make the rebuild order safe: the wait controller exists before anything can render the player (move `InitWait` ahead of the text
  restore / editor wiring), and `PlayerDimmedByWait` is false while the wait parts don't exist yet. Check every other `null!` field the
  same render path touches.
- Shared MAUI page, so Android and iOS get the same fix.
- If the order rule can be pulled into a pure unit-testable piece, test it; otherwise write the scripted device check (switch language
  with text in the reader, app stays up) without running it.

## Verification

- Fast checks green. Device (after review): on the S21, type text, switch language twice, the app stays up and reads.
