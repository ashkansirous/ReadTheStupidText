# Slice 387 — Dino: the shelf title "My stories" is cut to "My sto" by the Grown-ups button
**Product:** Dino

*In progress (wave 2026-11-15), draft PR.*

**Touches:** `src/Dino/DinoReader.App/ShelfHeader.cs` (the shelf header is built in code), `src/Dino/DinoReader.Core/ShelfTitleRules.cs`, `src/Dino/DinoReader.App/CornerShell.cs` (`PillButton`), `tests/Dino/DinoReader.Tests/ShelfTitleRulesTests.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml` and its code-behind, `docs/plan/slices/387-dino-shelf-title-cut-by-grownups-button.md`

## Why

The first iOS store-screenshot run on main (Slice 291, run 43650776897) shows the shelf title "My stories" (`Gallery_Title`) cut to "My sto" by the Grown-ups button on the iPhone 6.9" and 6.5" shots: `D:/ReadTheStupidText-worktrees/reviews/s291-shots/ios/dino-ios-sheet.png`.

## Work

- [ ] 1. Find the header layout that lets the button cover the title, and fix it so the title shows whole or shortens with an ellipsis before the button, on iOS and Android alike (same app).
- [ ] 2. Check a long title (the German and Farsi strings) and a large text size, so the fix does not move the cut.

## Verification

- A contact-sheet look at the shelf on an iPhone 6.9" simulator shot (`store-screenshots.yml`) and on the Android emulator: the title is not cut by the button.
