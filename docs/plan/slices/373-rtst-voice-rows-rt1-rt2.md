# Slice 373 — RTST voice rows RT1 and RT2: per-row size, state and the error strip
**Product:** RTST

*Built 2026-11-13 (wave 55), PR #1090, not merged; emulator checks pass, Windows failed row not yet verified.* Planned 2026-11-13. Split out of [Slice 364](364-server-errors-design-pass.md) item C, whose *Waiting on the user* line said "main has no such row on Mobile or Windows". Checked 2026-11-13 against origin/main: the rows exist; they only carry no size, state or Retry of their own. So no new screen and no question for the user: the mock is built onto the existing language lists (the mock always wins).

**Touches:** `src/ReadTheStupidText.Mobile/LanguagePage.xaml`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.App/LanguageWindow.xaml`, `src/ReadTheStupidText.App/LanguageWindow.xaml.cs`, `src/ReadTheStupidText.App/PackSetupText.cs`, `src/ReadTheStupidText.App/PackRowText.cs` (new, pure row rule), `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `tests/ReadTheStupidText.Tests/*` (`PackRowTextTests.cs`, `LocalizedPackStatusRowTests.cs`, `ReadTheStupidText.Tests.csproj`), `.maestro/rtst/*` (new flow), `tools/desktop-vm/checks/rtst-language-rows.ps1` (new check), `tools/desktop-vm/Invoke-DesktopTest.ps1` and `tools/desktop-vm/README.md` (register the check in the default RTST list), `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs` (public `IsInstalled`), `src/ReadTheStupidText.Mobile/Testing/*` (new Debug-only hook), `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs` (the Debug-only forced-failure seam), `docs/plan/slices/357-server-error-messages.md` and `docs/plan/slices/364-server-errors-design-pass.md` (tick their RT1/RT2 lines)

## Where the rows are (origin/main, 2026-11-13)

- **RT1, phone:** `src/ReadTheStupidText.Mobile/LanguagePage.xaml`: `LanguageList` (line 25) is a list of language rows in a rounded card, filled from code; each language maps to a voice pack. Download state today is one shared `ProgressSection` (lines 29-36: one `ProgressBar`, `StatusLabel`, `RetryButton`) under the list.
- **RT2, Windows:** `src/ReadTheStupidText.App/LanguageWindow.xaml`: the `ListView` (lines 26-31) shows only `NativeName` (`DisplayMemberPath`); the failed state is one `StatusText` (line 36) and `RetryButton` (line 41, `x:Uid LanguageRetry`), set in `LanguageWindow.xaml.cs:130-134` from `PackSetupText.For(state, progress, FailureKind)`.
- `ControlPanelWindow.xaml` and `WaitFlyoutWindow.xaml` have no per-pack rows; nothing changes there.

## Work

Follow Slice 357's *Design (Dino Reader 2026-11-13 Missing States mock, section 2)* RT1/RT2 part ([357](357-server-error-messages.md), the RT1/RT2 lines around 165-175) and the copy note `docs/plan/notes/2026-11-13-dino-copy.md`; they win over this file.

- [x] **RT1 (Mobile `LanguagePage`):** each language row gets its second line `Pack_RowSize` "Voice pack · {0} MB", the percent or `Wait_Retry` at the row's end, and on failure the strip under that row with `Error_Server` / `Error_VoiceGone` (404). The shared `ProgressSection` goes once every state is shown on its row.
- [x] **RT2 (Windows `LanguageWindow`):** a templated `ListView` item (icon tile, name, `Pack_NotDownloaded` "not downloaded" second line, `LanguageRetry` at the row's end) with the error line under a failed row; the shared `StatusText` / `RetryButton` go the same way. The "not downloaded" line shows on every row whose pack is not installed (Windows can tell: `VoicePackSetup.IsInstalled`, which reads the installed packs), as the mock draws it. The mock's voice-name prefix ("Thorsten · Deutsch · ") is not built: a language row's title is already the language, and no per-language voice name is available without the catalog.
- [x] Strings: `Pack_RowSize`, `Pack_NotDownloaded` in the Mobile `.resx` and App `.resw` languages (`Error_Server` / `Error_VoiceGone` are already built by Slices 357 / 364). `Wait_Retry` is not in RTST Mobile yet (premise check 2026-11-13): add it to the Mobile `.resx` languages and `AppResources.cs` with the copy note's text (`docs/plan/notes/2026-11-13-dino-copy.md`). The row's size comes from the catalog's `PackInfo.SizeBytes` (`src/ReadTheStupidText.Application/Reading/PackInfo.cs`), no Application change.
- [x] Android and iOS: RT1 is the shared MAUI page, so both get it; check the iOS compile.
- [x] Tests: unit-test the row-state text mapping; a Maestro flow for a failed row on the emulator; a VM check for RT2 (row template and the "not downloaded" line; the failed row on Windows only if an existing Windows hook reaches it, else under *Not yet verified*).
- [x] Debug-only test hook (premise check 2026-11-13: no forced-404 hook exists): an RTST test command in `src/ReadTheStupidText.Mobile/Testing/RtstTestCommands.cs`, e.g. `fail-pack-download kind=gone|server`, that makes the next voice-pack download fail with that kind, inside `#if DEBUG` as `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md` says. The flow uses it.

## Verification

Ran (fast checks, third review round): the Mobile Android build, the App x64 build, `ReadTheStupidText.Tests` (including `PackRowTextTests` for the pure row rule: other row, failed run row, starting run row, previous language's failure) and `Shared.Tests`. Not run yet: the emulator flow (`language-row-failed-download`), the VM check (`rtst-language-rows`, in the default RTST list) and the iOS compile; device runs come later. Not yet verified: a real failed download on the S21; the Windows failed row (not downloaded line, error line, Retry): the VM has no forced-failure hook on Windows, so the VM check sees only the idle rows (also in `docs/plan/unverified.md`).

**Final run (2026-11-13, supersedes the "Not run yet" part above):** `ReadTheStupidText.Tests` 628 pass, `Shared.Tests` 1654 pass; Android, App x64 and iOS builds 0 errors. Emulator (cached build 39394264b826, emulator-5554): `language-row-failed-download` PASS (`HOOK fail-pack-download ok gone`, `PACKDL forced-failure pack=supertonic-3 kind=NotFound`), `language-continue-two-step` PASS, `set-language` PASS. The new flow was not shown failing first: the hook does not exist on main, so it cannot pass there. Log: `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/rtst-language-row-failed-download.log` (outside the repo).

*Changed while building:*
- Windows row state is one design, not a patch per symptom: the pure `PackRowText.Compose` / `RunStateRule` (`src/ReadTheStupidText.App/PackRowText.cs`). The run row is `VoicePackSetup.CurrentLanguage` at `StartSetup`; it shows Resolving until the new run publishes, and a stale Failed/Idle is ignored while starting. One installed scan (`VoicePackSetup.InstalledLanguages`, replaces 15 `IsInstalled` calls) with a fallback report on failure; the last good scan is kept; Retry only on the run row.
- Mobile: the size table and the "complete" flag are written on the UI thread, and sizes are refetched on the next appearance after a cancel.
- Debug hook `fail-pack-download kind=gone|server lang=<code>` (Mobile, `#if DEBUG`, `MobileVoicePackService.cs` seam, `RtstTestCommands.cs`).
- Premise correction: `Wait_Retry` already existed in the Mobile `.resx` files; only `AppResources.cs` got a line.

*From review:*
- `reviews/s373-high.md` (10 findings): applied 1-5, 7-9; 6 and 10 listed below.
- Skipped #6 (`reviews/s373-high.md`): "the leading name/language part is dropped" - no per-language voice name source exists without the catalog; the row title is already the language.
- Skipped #10 (`reviews/s373-high.md`): "accept if the mock is the rule" - the mock specifies the percent only, so the row matches the mock.
- `reviews/s373-medium.md` (7 findings): applied 1-5 and 7; 6 listed below.
- Skipped #6 (`reviews/s373-medium.md`): "Retry Click handler inside a DataTemplate" - the WinUI XAML compiler resolves template handlers at build time, so a dead handler fails the build.
- `reviews/s373-check1-medium.md` (10 findings): applied 1-7, 9, 10; 8 listed below.
- Skipped #8 (`reviews/s373-check1-medium.md`): "Forced-failure arm is static" - the hook is Debug-only.
- `reviews/s373-check2-medium.md` (9 findings): applied 1-3; 4-9 listed below.
- Skipped #4 (`reviews/s373-check2-medium.md`): "Installed rescan runs only on transition to Ready" - packs change only through a download that ends in Ready.
- Skipped #5 (`reviews/s373-check2-medium.md`): "Inconsistent row text." - the row text is the design.
- Skipped #6 (`reviews/s373-check2-medium.md`): "unreachable normally, inconsistent if it fires" - Retry shows only on the run row.
- Skipped #7 (`reviews/s373-check2-medium.md`): "those rows never get sizes" - a language with no catalog entry has no size to show.
- Skipped #8 (`reviews/s373-check2-medium.md`): "LoadSizesAsync is fire-and-forget" - the table and flag are written on the UI thread.
- Skipped #9 (`reviews/s373-check2-medium.md`): "Static one-shot fail-pack-download state" - the hook is Debug-only.
- `reviews/s373-check3-medium.md` (2 kept findings, 5 dropped by the reviewer itself): both kept findings applied; none skipped.
- `reviews/s373-check4-medium.md` (7 findings, all low): none applied, all listed below; each unconfirmed, perf-only or Debug-only with no reproduction path.
- Skipped #1 (`reviews/s373-check4-medium.md`): "could show on the run row if StartAsync publishes nothing" - unconfirmed, no such path found.
- Skipped #2 (`reviews/s373-check4-medium.md`): "raises PropertyChanged on every row" - performance only.
- Skipped #3 (`reviews/s373-check4-medium.md`): "re-renders all rows on each progress tick" - performance only.
- Skipped #4 (`reviews/s373-check4-medium.md`): "armed forced failure persists in statics" - Debug-only.
- Skipped #5 (`reviews/s373-check4-medium.md`): "reads _settings.Language off the UI thread" - unconfirmed, no reproduction.
- Skipped #6 (`reviews/s373-check4-medium.md`): "run row in Ready state returns stateLine(run)" - unconfirmed, no reproduction.
- Skipped #7 (`reviews/s373-check4-medium.md`): "each appearance re-requests the catalog" - low impact, no reproduction.

#### For CLAUDE.md

- The Windows language-row state is one pure rule, `src/ReadTheStupidText.App/PackRowText.cs` (`Compose` / `RunStateRule`): the run row is `VoicePackSetup.CurrentLanguage` at `StartSetup`, a stale Failed/Idle is ignored until the new run publishes, Retry shows only on the run row, and the installed set comes from one scan (`VoicePackSetup.InstalledLanguages`, last good scan kept). Change the rule and its tests (`PackRowTextTests`), never patch `LanguageWindow.xaml.cs` per symptom. (Slice 373: docs/plan/slices/373-rtst-voice-rows-rt1-rt2.md)
- Debug-only `fail-pack-download kind=gone|server lang=<code>` (`src/ReadTheStupidText.Mobile/Testing/RtstTestCommands.cs`, seam in `MobileVoicePackService.cs`, `#if DEBUG`) makes the next download of that language fail; use it in flows instead of a real failure. (Slice 373: docs/plan/slices/373-rtst-voice-rows-rt1-rt2.md)

#### Not yet verified

- VM check `rtst-language-rows` not run: free memory 7 GB was under the 8 GB the VM needs, so the VM was not started.
- The Windows failed row (not downloaded line, error line, Retry): the VM has no forced-failure hook on Windows.
- Layout check: no `layout-check ok` line was produced.
- iOS on a device (iOS only compiled).
- A real failed download on the S21.
