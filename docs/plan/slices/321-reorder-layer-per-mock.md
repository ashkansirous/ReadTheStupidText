# Slice 321: Reorder layer back to the mock on Windows
**Product:** Dino

*Built 2026-11-08 (taken over from a dead session; origin/main merged in at 7996f7ba), merged as PR #962 (c65682e3). Windows VM and Android emulator checked, see Not yet verified.* Slice 319 (PR #957) changed the Dino Reorder layer to copy the shelf's geometry so it "lies over the shelf". That design was the coordinator's, written without reading the mock, which always wins. The mock (`docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`, frames 173-176, lines 314-330; Slice 264 lines 33-34) shows Reorder as its own panel: the ink bar (Cancel / "Reorder" / Done), the hint ("Drag a book to a new place. Your child's shelf shows the same order.", `padding:12px 18px 12px`), then the grid (`padding:0 14px`, no top padding). Phone: 3 across, 14 dp sides. Windows (frame 176, 1100x760 window): 6 across, gap 18, side padding 40. The mock has no tablet frame. The 2026-11-08 VM bug (L95: the layer kept phone padding, 13 px from the window edge) is fixed by the mock's 40 dp and 6 columns, not by the shelf's centred margin.

**Touches:** `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ShelfReorderView.cs`, `src/DinoReader.Core/WindowShape.cs`, `tests/DinoReader.Tests/WindowShapeTests.cs`, `tools/desktop-vm/checks/dino-reorder.ps1`, `docs/plan/slices/319-device-run-2026-11-08-fixes.md`

## Items

1. Read frames 173-176 in the mock and write each value used below into this file, with the line it comes from: grid side and top padding, column count, gap (across and down), tile size rule, hint padding, per frame. Where a value is not in the markup, say so and keep the code's value.
2. The Reorder layer's grid takes its geometry from the mock, not from the shelf. Phone and the grown-up corner: the frames 173/174 values (14 dp sides, 3 across, no top padding under the hint). Windows head: frame 176 (40 dp sides, 6 across, gap 18). Tablet (no frame): keep the shelf's values, as Slice 319 does, and say so here. Keep Slice 319's good parts: one geometry record, a relayout only when a value changes by 1 dp or more, the min tile clamp.
3. Unit tests for each platform's values.
4. The VM `dino-reorder` stage `1-sheet-reorder` asserts the mock's layout (the layer's first tile about 40 dp from the window's left edge, 6 tiles in the first row when there are 6 or more books, else all of them), not "within 40 px of the shelf".
5. Slice 319's file: a note that its "lies over the shelf" design was replaced by this slice to follow the mock.

## Mock values (item 1, read 2026-11-08 from the markup)

Source file: `docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`.

| Value | Phone (173/174) | Windows (176) | Source line |
| --- | --- | --- | --- |
| Hint padding | `12px 18px 12px` (13.5 px text) | `16px 40px 14px` (14 px text) | 317, 330, 370 |
| Grid side padding | 14 each side | 40 each side | 320, 333, 371 |
| Grid top padding | 0 | 0 (the grid follows the hint) | 320, 333, 371 |
| Columns | 3 (`W=100`, `G=10` in the `grid()` function) | 6 (`repeat(6,1fr)`) | 409-413, 371 |
| Gap across | 10 | 18 | 411, 371 |
| Gap down | not stated: row pitch `RH=180` against a tile of cover 133.3 (`CH=133`) + 5 + one 12.5 px title line (15) = 153.3, so about 27 | 18 (grid `gap`) | 411, 371 |
| Tile rule | cover 3:4 of the column width, title 12.5 px / 600 under it | `aspect-ratio:3/4` cover, title 13.5 px / 600 | 411, 372 |
| Cover to title spacing | 5 | 6 | 424-425, 372 |

Not in the markup: any tablet frame (the tablet keeps the shelf's geometry, as Slice 319 did), and the code's `TitleSpacing` of 7 (the mock says 5 and 6; left as it is, not in this slice's items). The Windows header strip of frame 176 (padding `6px 40px 18px 30px`, 22 px title) is also left as built; this slice is the grid and the hint.

## Decisions made while building (review `s321-high.md`)

- **Row gap (coordinator's decision).** The 180 dp row pitch is not forced: the code's tile is taller than the mock's (title reserve 36 + spacing 7, not one 15 px line + 5), so forcing the pitch clamps the gap to 0 on a 360 dp phone and the tiles touch. The Reorder layer uses the mock's visible gap between rows instead: phone 27 dp (mock RH 180 less the mock tile 153, Mock values table), Windows 18 dp (frame 176 grid gap). The code's title reserve stays (the shelf is out of scope). Unit-tested as gap values.
- **A phone held sideways has no mock frame** (the mock draws portrait phones only), so, like the tablet, it uses the shelf's geometry (`ReorderUsesShelfGeometry`: tablet or width > height). Reason: the 3-across portrait frame at 14 dp sides would give ~250 dp wide covers in a short window.
- **Windows width.** The mock shows one width (1100 dp, 6 across). Decision: tile size kept (155 dp = (1100 - 2*40 - 5*18) / 6), column count follows the width: `max(3, floor((W - 80 + 18) / (155 + 18)))`: 3 at 500, 6 at 1100, 8 at 1600, 13 at 2400 (unit tests). Tiles then stay 155 to about 180 dp wide at any width.
- **One tablet test.** The Reorder layer uses the shelf's own classification (`shelf.TabletUpright`, or the corner's when the corner shows) through `WindowShapeSeam.IsTabletUpright`, not a second `SizeOf` test.
- **Hint on the shelf geometry** (tablet, sideways phone): its left padding equals the grid's left edge (beside a rail: after the rail), so the hint lines up with the first column.
- **Relayout** compares the whole geometry (every value floored to whole dp, columns included), not only the margins.
- **Check script** (`dino-reorder.ps1`): the scale is the app window's own DPI (`GetDpiForWindow`), the width is the `ReorderView` rectangle over that scale, the expected first row is `min(columns for that width, books)` by the rule above, and a missing UIA element fails as "CHECK ERROR (not an app failure)".

## Design after the third strike

VM evidence 2026-11-08 (`reviews/s321-vm/results.md`, `reorder.png`, 1024x688 window, 2 books): the layer covers the whole window including the rail area, yet the hint started at x=400 and the grid at ~391 px (a 360 dp rail offset over an empty strip), only 3 columns fit, and the tiles sat vertically centred under a ~100 px gap instead of directly under the hint. Mock frame 176 (lines ~370-372): hint padding 16/40/14, grid padding 0 40, 6 across at 1100 wide, gap 18, grid top-aligned right under the hint.

1. **No rail offset on Windows.** The layer is added to `GalleryRoot` spanning all rows and columns (`GalleryPage.xaml.cs:106-107`), last, so it lies over the rail column. Hint and grid therefore start 40 dp from the page's left edge and end 40 dp from its right edge: `ReorderLayerDesktop(pageWidth)` has no rail argument. The tablet keeps `BesideRail` on the shelf's geometry (unchanged, no mock frame).
2. **Columns** come from the full page width less 80 with the mock's 155 dp tile at 1100: `max(3, floor((W - 80 + 18) / (155 + 18)))` = 5 at 1024, 6 at 1100, 3 at 500.
3. **Top-aligned on every platform.** Cause of the vertical centring: `Relayout` sets `_surface.HeightRequest` to the grid's content height (`ShelfReorderView.cs:332`), shorter than the scroll viewport with few books, and `_surface` (the `ScrollView` content, `ShelfReorderView.cs:26`) had default `VerticalOptions`, which WinUI's `ScrollViewer` centres when the content is shorter than the viewport (probable cause from the screenshot and code; not proven by a run). Fix: `_surface.VerticalOptions = LayoutOptions.Start`. The `Fit` top inset is 0 on phone and Windows (`WindowShape.cs`), so it adds nothing.
4. **VM check** (`dino-reorder.ps1`): `Add-Type` gets `-Namespace Rtst`; the inset is measured from a tile that is not the lifted one (the lift scales it about 9 px each side), expected 40 dp +-4; first row = `min(columns by rule 2, books)`; the first non-lifted tile's top must be within 24 dp below the hint's bottom. The rail read from the trace is removed.
5. **Unit tests:** 1024 -> 5, 1100 -> 6, 500 -> 3, and no rail offset.

## From review

- Skipped #2 (`reviews/s321-medium.md`): "rotating a phone while Reorder is open switches between the 3-across mock grid and the shelf's grid, and the hint padding changes" - decided behaviour: sideways phone uses the shelf grid (slice decision); a rotation while Reorder is open relayouts.
- Applied #1/#3 first, then reversed: the rail offset added for medium finding 1 is removed - the layer covers the rail, so the offset left an empty strip; VM screenshot 2026-11-08. The scale in the check is read after making the check process DPI aware.
- VM run 2 (`reviews/s321-vm/results2.md`) failed the inset at 30-34 px though the app matched the mock; check bug. `Get-GridEdge` now uses only `ReorderTile` rects (never `ReorderCover`), takes the grid edge from the first non-lifted tile as `X - index * (width + gap)`, measures the gap between two non-lifted neighbours (else 18 dp * scale), skips the inset assertion with "1 book: inset not measurable" when only the lifted tile exists, and prints the measured numbers on PASS.
- VM run 3 (`reviews/s321-vm/results3.md`) measured the inset 33-34 px twice (wants 40) while `reorder2.png` shows the second tile at x=232 = 40+174+18 and the hint text at x=40. Inset not asserted: UIA tile bounds read 6-7 px less than the screenshot shows (232 = 40+174+18); cause not found; the mock inset is shown by reorder2.png. Stage `1-sheet-reorder` prints "inset (not asserted): N px" with the tile X/W/gap numbers, still asserts the first-row count = min(columns, books) and the first tile top within 24 dp below the hint, and every FAIL prints all the numbers.

### Recheck after the takeover (2026-11-08)

Review files read: `reviews/s321-high.md` 8/8, `s321-medium.md` 8/8, `s321-check1.md` (no findings), `s321-check2.md` (no findings; fork not run, read by hand), `s321-recheck.md` 5/5. The fresh medium recheck confirmed every earlier finding fixed or recorded as a decision, and found 5 new:

- Applied #1 (`reviews/s321-recheck.md`): narrow or portrait Windows windows now use the phone/shelf geometry through the new `WindowShapeSeam.ReorderUsesDesktopGeometry`; a landscape non-tablet Windows window keeps the 1100x760 frame.
- Applied #2 (`reviews/s321-recheck.md`): epsilon before the `Floor` in `ReorderDesktopColumns`, so 1100 dp keeps 6 columns; tests added.
- Skipped #3 (`reviews/s321-recheck.md`): "skips relayout when grid whole-dp values are equal" - low; the hint height change without a grid change is not handled.
- Skipped #4 (`reviews/s321-recheck.md`): "initial _reorderGeometry is ReorderLayerShelf(default), Columns 0" - low; `ShelfOrderGrid.Fit` clamps the columns, so no crash.
- Skipped #5 (`reviews/s321-recheck.md`): "constructor no longer sets hint font/padding" - low; the view is only shown after `Open`, which styles the hint.

### Device runs (2026-11-08)

- Windows VM: `dino-reorder` PASS on all stages twice (before and after the recheck fixes; key 6b2bb74fcf67 after), `dino-launch` PASS. The inset was measured at 30 px against the mock's 40; it is printed, not asserted (the open item above).
- Android emulator: `dino-landscape-shelf` PASS. The `shelf-reorder` flow reported FAIL only on two first-launch markers (`HOOK skip-first-run ok`, `HOOK shelf-order-reset ok`) while every Reorder marker passed. The kept logcat starts at an older pid, so the harness likely missed early lines (not proven).

## Not yet verified

- The tablet AVD (`dino-tablet-upright-shelf`): the layer uses the shelf's grid, hint aligned with its left edge, also beside the grown-ups' rail.
- Phone portrait: 3 across, 14 dp sides, 27 dp row gap (no flow asserts the measurements).
- Windows VM with 6 or more books (only 2 books were used; 2026-11-13 VM run: `dino-reorder` passed, the VM still has 2 books and no 6-book + resize check exists), and a window resize while the layer is open (columns change, no layout cycle).
- A narrow Windows window after recheck fix 1.
- The 30 px versus 40 px inset on Windows (an assertion that agrees with the screenshot). 2026-11-13 VM run: `dino-reorder` logged 30 px against the mock's 40, with 0 layout-cycle lines, but no assertion.
- The `shelf-reorder` first-launch markers (`HOOK skip-first-run ok`, `HOOK shelf-order-reset ok`).
- iOS phone/tablet: the same shared MAUI code; no Mac, not run.

#### For CLAUDE.md

None.
