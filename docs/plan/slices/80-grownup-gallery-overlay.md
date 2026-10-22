# Slice 80 — Dino Reader: grown-up mode is the kids' gallery with an edit overlay

*Built 2026-10-22 (wave 2026-10-22), PR #241. Dino 393 tests pass; Dino Android and Windows x64 build; emulator RTST_Test passed. Not yet verified on Windows Dino or TalkBack.*

**Batch:** 13
**Decisions:** 87
**Depends on:** Slice 77 (covers), Slice 78 (the wizard is the editor)
**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryText.cs`, `src/DinoReader.App/StoryCover.xaml`, `src/DinoReader.App/StoryCover.xaml.cs`, `src/DinoReader.App/StoryTile.cs`, `src/DinoReader.App/TileOverlay.xaml` (new), `src/DinoReader.App/TileOverlay.xaml.cs` (new), `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.App/GrownUpMode.cs` (new), `src/DinoReader.App/ManagePage.xaml` (removed), `src/DinoReader.App/ManagePage.xaml.cs` (removed), `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/App.xaml.cs` (grown-up reset only), `src/DinoReader.Core/GalleryLayout.cs` (new), `src/DinoReader.App/MauiProgram.cs` (registration line only), `src/DinoReader.App/ReadingPage.xaml.cs` (`GoToManageAsync` only), `src/DinoReader.App/CoverStep.xaml.cs` (`StoryTile.From` call only), `src/DinoReader.Core/AwayTracker.cs` (`ExternalActivityCap`), `tests/DinoReader.Tests/LibraryHardeningTests.cs`, `src/DinoReader.App/Resources/Strings/**`, `src/DinoReader.App/CLAUDE.md`, `tests/DinoReader.Tests/GalleryTests.cs`
**Story issue:** —

## Goal

Passing the gate no longer opens a separate "Manage stories" page of blank colour blocks
(image 4). It shows the very same gallery — covers and titles — with an edit overlay on each
tile (Edit, Cast, Delete) and the dashed "Add a story" tile. Edit opens the Slice 78 wizard
on that story. "Done" (or leaving the app, as today) drops back to kids' mode.

## Why

`ManagePage` reuses `StoryCover` without the title label and relies on small Edit/Cast
labels under tiles that show no cover; it is a second gallery layout to keep in step with
the first. The user: "all the books are showing horribly. There is no way to edit them and
they are not showing any covers like the kids view."

## Decisions locked with the user (2026-10-22)

- **One gallery, one tile component.** Grown-up mode is a state of `GalleryPage`, not a
  page. `ManagePage` is removed.
- Overlay controls are large tap targets (≥ 44 dp), on the tile, with labels, so a parent
  on a phone can use them; the trash asks "Delete *Title*?" before deleting (as today).
- The gate (`GrownUpGate`: hold, then a sum) and `IGrownUpPage`'s leave-the-app reset are
  unchanged; the lock icon toggles the mode, and the mode is never persisted.

## Steps

- [x] `GrownUpMode` (observable `IsGrownUp`) registered as a singleton; `GalleryPage` binds
      the overlay visibility and swaps the lock icon for a **Done** button while on.
- [x] `TileOverlay`: a translucent band at the foot of the cover with **Edit** and **Cast**
      buttons and a red trash badge top-right; appears on every `StoryCover` in the gallery
      only in grown-up mode; the title label beneath the tile stays. In grown-up mode a tap
      on the cover itself opens Edit (not the reader); in kids' mode it reads.
- [x] The dashed **Add a story** tile is the first grid item in grown-up mode (it opens the
      wizard) and absent in kids' mode.
- [x] `StoryActions`: Edit → `StoryWizardPage` with the story loaded; Cast → `CastPage`;
      Delete → confirm, `IStoryLibrary.Delete`, tile animates out.
- [x] Remove `ManagePage`; move its strings into this slice's block in `Strings/*.resx`;
      `GalleryText` gains the Done / Edit / Cast / Delete keys.
- [x] Slice 73 follow-up folded in: the gallery re-lays out cleanly after toggling night mode
      (the tiles are the same control in both modes now, so verify, and fix if it persists).
- [x] `src/DinoReader.App/CLAUDE.md`: one rule — grown-up mode is a `GalleryPage` state;
      there is no Manage page; editing goes through `StoryWizardPage`.
- [x] `GalleryTests`: tile order and the Add tile's presence per mode; overlay hidden in kids'
      mode.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator (`device-test`): kids' mode shows covers + titles, tap reads. Lock → gate → the
  same grid with overlays and the Add tile; Edit opens the wizard on that story; Cast opens
  the cast page; Delete confirms and removes; Done returns to kids' mode; background the app
  and return → kids' mode. TalkBack reads "Edit Font check", "Delete Font check".
- Windows Dino build: the same with mouse; tiles reflow on resize.

## Built

*Changed while building:* `GalleryPage` serves both modes, driven by the `GrownUpMode` singleton (`INotifyPropertyChanged`; the overlay binds to it). `GalleryLayout` (in `DinoReader.Core`, tested) lays out the grid; tiles fill their column at equal width. Every grown-up action re-checks the mode after each await. `DinoApp` owns the reset (`CloseGrownUpPagesAsync`, with a too-late return; a new window starts in kids' mode; an external-activity guard is capped at `AwayTracker.ExternalActivityCap` = 10 min). The reading-screen lock enters grown-up mode after popping to the gallery. Four new strings, five dead ones removed.

*From review:* `D:\ReadTheStupidText-worktrees\reviews\80-review-1.md`: all fixed except item 11 (line endings; git normalises). `80-review-2.md`: all 10 fixed.

*Emulator (RTST_Test), PASS:* kids' mode reads; the gate leads to the overlay; Edit, Cast, Add, Delete and Done work; content descriptions present; delete fade; backgrounding over 2 min returns to kids' mode; rotation consistent; reading-screen lock enters grown-up mode; equal tile widths in portrait and landscape.

#### For CLAUDE.md (lifted 2026-10-22)

- The `src/DinoReader.App/CLAUDE.md` grown-up-mode rule is already updated on this branch. Missing from it: every grown-up action re-checks `GrownUpMode` after each await (the mode can end mid-action); `DinoApp` owns the reset. (Slice 80: docs/plan/slices/80-grownup-gallery-overlay.md)

#### Not yet verified

- Windows Dino: nested taps and window recreate.
- TalkBack reading "Edit <title>" and "Delete <title>".
- The 10-min `AwayTracker.ExternalActivityCap` timer (no test; lives in `DinoApp`).
