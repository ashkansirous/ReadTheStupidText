# Slice 420 — Dino: the Windows VM checks Slice 418 left failing (open book, page turn, desktop polish, reorder)

**Product:** Dino

*Planned.*

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

- [ ] 1. **dino-open-book** fails: "no 'Ready for my child' button on the shelf". Slice 418 removed that button (one shelf;
  a tap on a cover reads it). Change the check to open a book from the locked shelf by a cover tap, as L1 shows.
- [ ] 2. **dino-page-turn** fails only because item 1 never opens a book. Rerun after item 1; fix it only if it still fails.
- [ ] 3. **dino-desktop-polish** fails: Enter on the focused book did not open its sheet; the trace reads
  "Dino keyboard: Enter not used (GalleryPage, Corner)" then "Enter or Space opened a book". Find out from the code whether
  Enter on an unlocked cover should open the ••• sheet (old corner behaviour) or read the book (Slice 418: "Tapping a book
  still reads it"). If reading is right, change the check to open the sheet the way the app does now (the ••• button);
  if the app dropped a keyboard path to the sheet, restore it. Then reach the rail stage and check the 270-wide rail (WL1/WL2).
- [ ] 4. **dino-reorder** fails at 1-sheet-reorder, 2-hold-1s and 3-drag-done-enter: the reorder layer did not open from
  the hold and showed 0 tiles, while the sheet's "reorder row chosen" path did open the view in the trace. Find out whether
  Slice 418's one-shelf change lost a way into reorder that the corner had (a **Replaces** gap) or the check expects a hold
  Slice 382 removed; restore the lost path, or update the check to the shipped one.
- [ ] 5. **dino-keyboard**: its Ctrl+L and Ctrl+1/2/3 stages passed only as part of the check's single PASS line. Make the
  check log each stage by name so a run shows they ran.

## Verification

- Windows VM: `pwsh -NoProfile -File tools/desktop-vm/Invoke-DesktopTest.ps1 -App dino`, all 15 checks pass, and the
  dino-keyboard output names the Ctrl+L and Ctrl+1/2/3 stages.
- If item 3 or 4 changes app code: `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`, and the Android
  emulator flows `lock` and `corner-read-now`, since the shelf code is shared.
