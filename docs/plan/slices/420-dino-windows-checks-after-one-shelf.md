# Slice 420 — Dino: the Windows VM checks Slice 418 left failing (open book, page turn, desktop polish, reorder)

**Product:** Dino

*Built 2026-11-20, PR to follow. Emulator and Windows VM checks passed 2026-11-20 (see Verified below); the iOS cover-tap check and two store-shot flows are not yet run.*

Source: the Slice 418 Windows VM rerun on 2026-11-20 09:13-09:22 (worktree `s418d`, branch `fix/s418-vm`; logs were
`tmp/run1.log`, crash evidence `D:/ReadTheStupidText-worktrees/_desktop-vm/crash-evidence/dino-20261120-092028`). 9 of 15
checks passed (dino-onboarding-keys, dino-onboarding, dino-onboarding-skip, dino-launch, dino-settings, dino-keyboard,
dino-gate, dino-record, dino-window). Four failed. Each one is written down here and fixed in this slice, not during the
run. Slice 418 (`docs/plan/slices/418-dino-one-shelf-with-a-lock.md`) owns the behaviour; its Decisions are the rule.

**Touches:** `tools/desktop-vm/checks/dino-open-book.ps1`, `tools/desktop-vm/checks/dino-page-turn.ps1`,
`tools/desktop-vm/checks/dino-desktop-polish.ps1`, `tools/desktop-vm/checks/dino-reorder.ps1`,
`tools/desktop-vm/checks/dino-keyboard.ps1`, `tools/desktop-vm/checks/_lib.ps1`,
`src/Dino/DinoReader.App/ShelfReorderView.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`,
`src/Dino/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `docs/plan/slices/420-dino-windows-checks-after-one-shelf.md`

## Work

- [x] 1. **dino-open-book** fails: "no 'Ready for my child' button on the shelf". Slice 418 removed that button (one shelf;
  a tap on a cover reads it). Change the check to open a book from the locked shelf by a cover tap, as L1 shows.
  *Finding (check):* the button was `Gallery_ReadyForChild` of the old corner, dropped on purpose by 418 (Replaces: the Child's shelf pill
  and Grown-ups button). `dino-open-book.ps1` now calls `Enter-KidsShelf` (a tap on the lock when the chip shows, no-op when locked)
  and taps the first `StoryTile`. Built, not run on the VM.
- [x] 2. **dino-page-turn** fails only because item 1 never opens a book. Rerun after item 1; fix it only if it still fails.
  *Finding:* no change; it starts with "not on the reading page (run the open-book check first)". Its rerun belongs to the VM run.
- [x] 3. **dino-desktop-polish** fails: Enter on the focused book did not open its sheet; the trace reads
  "Dino keyboard: Enter not used (GalleryPage, Corner)" then "Enter or Space opened a book". Find out from the code whether
  Enter on an unlocked cover should open the ••• sheet (old corner behaviour) or read the book (Slice 418: "Tapping a book
  still reads it"). If reading is right, change the check to open the sheet the way the app does now (the ••• button);
  if the app dropped a keyboard path to the sheet, restore it. Then reach the rail stage and check the 270-wide rail (WL1/WL2).
  *Finding (both):* Enter reading the book is right: `GalleryPage.xaml.cs` `OpenTileAsync` (about line 1447) reads a tapped or Enter-ed cover
  since 418 (decision 6, mock L4-L6 "A tap on the cover still reads"); before 418 (`9e1cd0fb^1`, same method) `_mode.IsGrownUp ? OpenBookSheetAsync : ReadAsync`
  opened the sheet. The trace "Enter or Space opened a book" is that read, so the check's wait for `BookSheetClose` could never pass.
  But Enter on a focused book was also the **keyboard's only way to the sheet** (the ••• is a tap-only `Grid` in `TileOverlay.xaml`, no tab stop; right-click is mouse only),
  and 418's Replaces line does not list it as dropped: an app gap. Restored through the one key route (`Platforms/Windows/CLAUDE.md`: keys go `DinoKeyboard.cs` -> `DinoKeyMap` -> `IDinoKeyTarget`): `DinoKeyboard.Map` turns the Menu key and Shift+F10 into `DinoKey.ContextMenu`,
  `DinoKeyMap` resolves it on the corner screen to `DinoKeyAction.OpenBookMenu` (unit-tested), and `GalleryPage` `IDinoKeyTarget.Handle` calls `OpenFocusedBookMenu` (`GalleryPage.Desktop.cs`), which opens the sheet of the
  book that has the keyboard (unlocked, not lapsed: a greyed book has no dots, decisions 8 and 10; the right-click path got the same guard) with `_openingFromKeyboard`, so closing it
  returns the keyboard to the book; trace line "Dino shelf: context key opened the book sheet". Windows-only code, so Android and iOS have nothing to match (no hardware context key on the shelf there).
  Check: `dino-desktop-polish.ps1` presses Shift+F10 (`Press-ShiftF10`) and waits for that trace line and `BookSheetClose`; the Tab-and-Enter stage on the locked shelf is unchanged (Enter reads).
  The 270-wide rail (WL1/WL2) is asserted by `Test-RailWidth` (see item 4's last line); the stages after the sheet (Settings arrows, tap on lock) are unchanged and now reachable.
- [x] 4. **dino-reorder** fails at 1-sheet-reorder, 2-hold-1s and 3-drag-done-enter: the reorder layer did not open from
  the hold and showed 0 tiles, while the sheet's "reorder row chosen" path did open the view in the trace. Find out whether
  Slice 418's one-shelf change lost a way into reorder that the corner had (a **Replaces** gap) or the check expects a hold
  Slice 382 removed; restore the lost path, or update the check to the shipped one.
  *Finding (check, no app loss found):* the hold is intact: `TileOverlay.xaml.cs` `HoldMilliseconds = 1000` -> `OnHoldRequested` (`GalleryPage.xaml.cs` about line 1325) is unchanged since
  `9e1cd0fb^1`, still needs the shelf unlocked and no sheet open; Slice 382 removed only the second hold *inside* the layer. The crash-evidence trace
  (`crash-evidence/dino-20261120-092028`) shows 1-sheet-reorder opened the layer (09:20:23.480 "sheet reorder row chosen") and the layer closed at 09:20:27.375, 3.9 s later, before the
  check's 5 s wait ended, so 1-sheet-reorder threw on an assertion (its message was not kept; the trace cannot tell which) and its catch pressed Esc. Then `5a-esc-cancels`
  pressed Esc with no layer open: on the one shelf Esc asks "Close Dino?" (418), and that page sat over the shelf for 2-hold-1s (no hold reached a tile; the layer never opened) and 3-drag-done-enter (0 tiles).
  Changes in `dino-reorder.ps1`: 5a presses Esc only while the layer is open (else fails with that reason); `Open-Reorder-FromSheet` opens the sheet from the book's ••• button
  (`TileMore`), not by clicking the cover (a cover tap reads since 418). Still unknown until the VM run: which assertion 1-sheet-reorder tripped (its failure message names the numbers).
  **App bug, shipped in 1.136.0 (Slice 418), fixed here:** `TileOverlay.xaml` `Backdrop` swallowed a bare-cover tap on the unlocked shelf and raised `OpenRequested`, so a mouse or touch tap
  on an unlocked cover opened the sheet, while Enter and `OpenTileAsync` read the book (418 decision 6 and mock L4-L6: a tap reads, only the ••• opens the sheet). Now `TileOverlay.OnBackdropTapped`
  raises a new `ReadRequested` (`TileOverlay.xaml.cs`), wired in `GalleryPage.xaml` to `OnReadRequested` (`GalleryPage.xaml.cs`, same guards as a cover tap, then `OpenTileAsync`); the dot still raises `OpenRequested`,
  the 1 s hold is untouched. Shared MAUI code, so Android, iOS and Windows agree. No pure rule to unit-test (it is event wiring). Callers that tapped a cover expecting the sheet now use the dots:
  `.maestro/dino/record-per-character.yaml`, `record-per-language.yaml`, `store-shot-ios-2-record.yaml` and `needs-file/store-shot-phone.yaml`, `store-shot-tablet.yaml` (`tapOn id: TileMore above: <title>`), and the VM checks `dino-keyboard` (sheet-esc, both opens), `dino-record` (intro) and `dino-reorder`
  through `Find-FirstTileMore` in `_lib.ps1`. The other flows that wait for `BookSheet*` already tap `TileMore`.
  Owed (nothing of this was run): the emulator flows `record-per-character`, `record-per-language`, `store-shot-ios-2-record` (iOS simulator), `needs-file/store-shot-phone` and `store-shot-tablet` (the relative `above:` selector, its nearest-`TileMore` choice, is untried and
  to be confirmed against the Maestro docs and a run), `corner-read-now`, and the new cover-tap-reads path (a tap on an unlocked cover reads the book); the Windows VM run of all of Verification.
  Rail (item 3): `dino-desktop-polish.ps1` `Test-RailWidth` (after the gate) asserts the widest rail row is at most 230 dp (mock 222) with rows ending within 258 dp (mock 250 plus 8 slack), and the first book 302 dp or more from the window's left; untried on the VM.
- [x] 5. **dino-keyboard**: its Ctrl+L and Ctrl+1/2/3 stages passed only as part of the check's single PASS line. Make the
  check log each stage by name so a run shows they ran.
  *Done:* `dino-keyboard.ps1` runs each stage through `Invoke-Stage`; the PASS line starts "stages passed: gate-by-keyboard; ink-title-bar; sheet-esc; esc-from-grown-up-page;
  Ctrl+1/2/3 tabs and Esc on Books; Esc on the shelf asks to close; Ctrl+L lock and unlock; reading-page keys; cream title bar", and a failure names the stage and those before it.

- [x] 6. **dino-desktop-polish, Settings arrows (VM run 2026-11-20 12:13):** "the Down arrow did not move the focus in Settings (before 'ControlType.Button|Language: English|', after the same)", trace
  "Dino keyboard: Down not used (GalleryPage, Corner)". *Old behaviour:* before 418 (`9e1cd0fb^1`) `DinoKeyMap` also answered Up and Down on the corner with `None`; the arrows moved the focus natively
  (`DinoFocusRing.SetArrowsFor` sets `XYFocusKeyboardNavigation` Enabled for a `GalleryPage` with no overlay open, `DinoFocusRing.cs` line 60), so "not used" was the normal trace. No file under `Platforms/Windows`
  or `SettingsPage` lost a key route in 418 (`git diff 9e1cd0fb^1 HEAD` on them shows only an alert wrapper and the Menu-key edit of this slice). *Cause: not proven.* The run reached this stage for the
  first time (the earlier stages failed first at 418), so there is no run showing the native arrows working on the one shelf; I found no code path that turns them off. *Fix:* the arrows now go through
  the one key route like Right does: `DinoKeyMap.OnCorner` maps Up and Down on the corner to `FocusRowUp` and `FocusRowDown` (unit-tested), `GalleryPage` `IDinoKeyTarget.Handle` passes them on only
  while the Settings tab shows (`MoveSettingsFocus`), and `GalleryPage.Desktop.cs` `MoveSettingsFocus` calls `FocusManager.TryMoveFocus` and marks the key handled, so it never moves twice; a slider, list or text
  field inside a Settings sub-page, and the Books grid, keep their native arrows (the page returns false); trace line "Dino settings: arrow Down moved the focus: True". Windows-only code: Android and iOS have no arrow keys here.
  `dino-desktop-polish.ps1` `Get-FocusedKey` now includes the element's position, so two rows with one name cannot read as an unmoved focus. Runtime change on Windows: untried on the VM.
- [x] 7. **Regression check for the shipped cover-tap bug (418 decision 6):** emulator flow `.maestro/dino/unlocked-cover-reads.yaml` (hook `enter-grown-up`, tap the first `StoryTile` by index because the title text
  sits on `TileMore`, expect `ReadingSurface`, `BookSheetReadNow` not visible, Back to the unlocked shelf) and the VM stage `Test-UnlockedCoverReads` in `dino-desktop-polish.ps1` (a real mouse click on an unlocked cover reads,
  no `BookSheetClose`, Back returns to the chip). The flow's first draft selected the cover by title and failed at the tap; neither new check has run to green yet.
  `Test-RailWidth` also prints its measured numbers (`RAILWIDTH ...`) on a pass, and the check's FAIL message now lists every earlier stage's result.
- [x] 8. **VM run 2026-11-20 12:37 (`crash-evidence/dino-20261120-123931`), dino-desktop-polish: "Back from the reading page did not return to the unlocked shelf (waited 10 s)".**
  *Finding (check, cause not fully proven; app auto-lock ruled out):* the trace puts the unlock at 12:38:39.2 (gate complete) and the cover click's reading page at 12:38:40.97 (title bar tone Cream), and the failure came about 12 s after the
  unlock, so the 30 s auto-lock (418 decisions 13-14) had not run: it would have written a Cream line for the locked shelf and `Test-UnlockedCoverReads` is the first stage after the rail, a few seconds from the unlock. After 12:38:40.97 the
  trace has no line at all (a return to the shelf writes an Ink line), so the app never took the Back. The check pressed `ReadingBack` the moment it first showed, while the page was still being pushed; a `PopAsync` mid-push is the likely loss
  (not shown by a log). Fix in the check only: it waits 1.5 s for the push to end before the Back, and a failure now names the state (`Get-ShelfStateAfterBack`: reading page still open, shelf back but locked, or shelf without chip), so the next run settles it. If the next run says
  "still open" with the wait in, the app drops a Back after open and that is an app bug to fix. Stages after the cover stage (Shift+F10 sheet, Settings arrows) have no hook to lengthen the 30 s on the VM (the hooks are Debug and Maestro only); they run a few seconds apart, and the Settings tab and sheet taps reset the clock.
- [x] 9. **dino-reorder 1-sheet-reorder "1 layout-cycle trace lines after open".** *Finding (check):* the stage counts trace lines matching `LayoutCycle|layout cycle`; the app writes `LayoutCycleTrace listener ready=True ...` (`LayoutCycleTrace.cs`) on every
  launch, and `Initialize-Shelf` relaunched the app after the check had taken its "before" count (the trace shows that line at 12:38:58.415 and no `[LayoutCycleTracing]` line between 12:38:58 and 12:39:26; the "Reorder trace: relayout" lines are not cycles). So the one "cycle" was the start-up line. `dino-reorder.ps1`
  `Count-LayoutCycles` leaves out the listener line, and the "before" count is taken inside the stage just before opening. 5a failed only as a knock-on and needs no change.
- [x] 10. **RAILWIDTH line.** `Test-RailWidth`'s `Write-Host` is not part of a check's result, which is why the run output has no `RAILWIDTH` line; the same numbers are in the check's result text (the passed-stage list on a failure, `$rail` in the PASS line), as the polish failure shows: "rail 270 dp: widest rail row 194 dp, rows end at 244 dp, first book at 322 dp". No change.
- [x] 11. **Settings Up/Down stage.** `dino-keyboard.ps1` has the named stage "Settings Up/Down arrows move the focus between rows" (Ctrl+3, Tab to a row, Down must change the focused element, Up must bring it back, then Ctrl+1); the arrows go out as extended keys like Left and Right. `dino-desktop-polish.ps1` keeps its own Down check. Untried on the VM.

#### Built and tested (2026-11-20)

- Checks parse under pwsh and Windows PowerShell 5.1; `test-check-dryrun.ps1` all passed; line endings and control characters clean.
- Windows head (Debug, x64) and Android head build with 0 errors; `DinoReader.Tests` 4848 passed, 0 failed, 2 skipped (was 4843 at 418; before the review fixes; they added one `DinoKeyMapTests` test for the context key);
  `Shared.Tests` 1736 passed. Five `.maestro` flows changed (item 4): flow lint ran; no emulator run. Not run, all owed: the emulator and simulator flows listed in item 4, and the Windows VM (all of Verification).

#### VM finding (2026-11-20): Settings Down crashed the app

Pressing Down in Settings threw a COMException ("In a WinUI Desktop app, the app must call TryMoveFocus ... with FindNextElementOptions ... SearchRoot") from `GalleryPage.MoveSettingsFocus`, the parameterless `FocusManager.TryMoveFocus` (evidence `_desktop-vm/crash-evidence/dino-20261120-124939`). Fixed in `GalleryPage.Desktop.cs` (`TryMoveSettingsFocus`): the overload with `FindNextElementOptions { SearchRoot = xamlRoot.Content }` (context7, Windows App SDK `FocusManager.TryMoveFocus(direction, options)`); a throw is caught, reported as `FallbackArea.Input` / `FallbackReason.FocusMoveFailed` (new) and leaves the key unhandled. No other parameterless `TryMoveFocus`/`FindNextElement` call exists in `src/`. Untried on the VM: re-run the Settings Up/Down stage.

#### Built, tests and device runs (2026-11-20, final)

- Final test counts: `DinoReader.Tests` 4850 passed, `Shared.Tests` 1736 passed; Windows and Android heads build; flow lint 149 flows, 0 findings. (The 4848 above was before the review fixes.)
- Emulator 2026-11-20: `unlocked-cover-reads`, `lock`, `corner-read-now`, `record-per-character`, `record-per-language` PASS.
- Windows VM 2026-11-20 (build `0e45ba7853d1`): all 12 Dino checks PASS, including the cover-click stage and the Settings Up/Down arrow stages (the earlier Settings Down crash is fixed).

#### From review

- `reviews/s420-high.md`: 12 findings (3 important, 9 nits), all 12 fixed.
- `reviews/s420-medium.md`: 5 nits, none applied:
- Skipped #1 (`reviews/s420-medium.md`): "Comment names removed OnListContextRequested" - stale comment only, no behaviour.
- Skipped #2 (`reviews/s420-medium.md`): "records no UiUsage.Tap(UiControls.BookMore)" - the keyboard-opened sheet not counting a BookMore tap is analytics-only, no behaviour.
- Skipped #3 (`reviews/s420-medium.md`): "OpenTileAsync counts a Book tap before the IsLapsed early return" - pre-existing, not from this change.
- Skipped #4 (`reviews/s420-medium.md`): "never run; on a multi-column tablet a neighbouring dot could win" - the selectors were replaced by id plus title and the flows passed (emulator flows run; the two store-shot flows were not run, see Not yet verified).
- Skipped #5 (`reviews/s420-medium.md`): "OnKeyUp maps F10 to ContextMenu with no Shift test" - an F10 key-up without Shift has no effect because the key-down decides.
- A check pass `reviews/s420-check.md` was still being written when this was recorded; its findings are not recorded here (not read).

#### Not yet verified

- [session] The Maestro flows `needs-file/store-shot-phone` and `needs-file/store-shot-tablet` (tap on a book's dots by title) were not run.
- [cloud] iOS: a cover tap reads the book on the unlocked shelf (shared MAUI code, run only on the Android emulator and Windows).

## Verification

- Windows VM: `pwsh -NoProfile -File tools/desktop-vm/Invoke-DesktopTest.ps1 -App dino`, all 15 checks pass, and the
  dino-keyboard output names the Ctrl+L and Ctrl+1/2/3 stages.
- If item 3 or 4 changes app code: `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`, and the Android
  emulator flows `lock` and `corner-read-now`, since the shelf code is shared. Item 4 changed it: also the five flows it lists
  (`record-per-character`, `record-per-language`, `store-shot-ios-2-record`, `needs-file/store-shot-phone`, `store-shot-tablet`).
