# Slice 373 — RTST voice rows RT1 and RT2: per-row size, state and the error strip
**Product:** RTST

*Planned 2026-11-13.* Split out of [Slice 364](364-server-errors-design-pass.md) item C, whose *Waiting on the user* line said "main has no such row on Mobile or Windows". Checked 2026-11-13 against origin/main: the rows exist; they only carry no size, state or Retry of their own. So no new screen and no question for the user: the mock is built onto the existing language lists (the mock always wins).

**Touches:** `src/ReadTheStupidText.Mobile/LanguagePage.xaml`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.App/LanguageWindow.xaml`, `src/ReadTheStupidText.App/LanguageWindow.xaml.cs`, `src/ReadTheStupidText.App/PackSetupText.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `tests/ReadTheStupidText.Tests/*`, `.maestro/rtst/*` (new flow), `tools/desktop-vm/checks/*` (new or changed check)

## Where the rows are (origin/main, 2026-11-13)

- **RT1, phone:** `src/ReadTheStupidText.Mobile/LanguagePage.xaml`: `LanguageList` (line 25) is a list of language rows in a rounded card, filled from code; each language maps to a voice pack. Download state today is one shared `ProgressSection` (lines 29-36: one `ProgressBar`, `StatusLabel`, `RetryButton`) under the list.
- **RT2, Windows:** `src/ReadTheStupidText.App/LanguageWindow.xaml`: the `ListView` (lines 26-31) shows only `NativeName` (`DisplayMemberPath`); the failed state is one `StatusText` (line 36) and `RetryButton` (line 41, `x:Uid LanguageRetry`), set in `LanguageWindow.xaml.cs:130-134` from `PackSetupText.For(state, progress, FailureKind)`.
- `ControlPanelWindow.xaml` and `WaitFlyoutWindow.xaml` have no per-pack rows; nothing changes there.

## Work

Follow Slice 357's *Design (Dino Reader 2026-11-13 Missing States mock, section 2)* RT1/RT2 part ([357](357-server-error-messages.md), the RT1/RT2 lines around 165-175) and the copy note `docs/plan/notes/2026-11-13-dino-copy.md`; they win over this file.

- [ ] **RT1 (Mobile `LanguagePage`):** each language row gets its second line `Pack_RowSize` "Voice pack · {0} MB", the percent or `Wait_Retry` at the row's end, and on failure the strip under that row with `Error_Server` / `Error_VoiceGone` (404). The shared `ProgressSection` goes once every state is shown on its row.
- [ ] **RT2 (Windows `LanguageWindow`):** a templated `ListView` item (icon tile, name, `Pack_NotDownloaded` "not downloaded" second line, `LanguageRetry` at the row's end) with the error line under a failed row; the shared `StatusText` / `RetryButton` go the same way.
- [ ] Strings: `Pack_RowSize`, `Pack_NotDownloaded` in the Mobile `.resx` and App `.resw` languages (`Error_Server` / `Error_VoiceGone` are already built by Slice 364).
- [ ] Android and iOS: RT1 is the shared MAUI page, so both get it; check the iOS compile.
- [ ] Tests: unit-test the row-state text mapping; a Maestro flow for a failed row on the emulator (the existing forced-404 hook); a VM check for RT2.

## Verification

Emulator flow and VM check pass; iOS compiles. Not yet verified: a real failed download on the S21.
