# Slice 264 — Dino: grown-ups reorder the shelf (iPhone-style reorder mode)

*Built 2026-11-05, merged as PR #799 (2fe5a6ea); Windows drag, Done and iOS not yet tried on a device, see Not yet verified.* Dino Reader; Android and iOS alike; the Windows head gets the same mode with the mouse.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ShelfReorder*.cs` (new), `src/DinoReader.Core/ShelfOrderGrid.cs`, `src/DinoReader.App/AspectRatioView.cs`, `src/DinoReader.App/TileOverlay.xaml`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/Platforms/Windows/DinoCursor.cs`, `src/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/Platforms/Windows/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/LayoutCycleTrace.cs`, `src/DinoReader.Core/DinoKeyMap.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/ShelfOrder*.cs` (new), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/shelf-reorder.yaml` (new), `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/264-dino-gallery-reorder-mode.md`.

## Decisions (user, 2026-11-04, the request and RefineScope)

- Two ways into reorder mode, both in grown-up mode only: the book's ⋯ sheet gets a **Reorder** row, and **pressing a book for 1 second** (like iOS).
- Reorder mode looks like the iPhone home-screen edit mode: every book **wiggles** in place; dragging a book lifts it; the spot it would land in **lights up**; the other books **slide with an animation** to make room.
- Dropping a book before another inserts it there and shifts the rest down: order 1, 2, 3, 4, 5, 6 with 5 dropped before 3 becomes **1, 2, 5, 3, 4, 6**.
- **Done** and **Cancel** at the top. Done keeps the new order; Cancel puts every book back where it was.
- One order for the whole shelf: the children's view and the grown-ups' view show the same order. A newly added book (store, own book, restore) goes to the front.
- Windows: the same mode, entered from ⋯ or a 1-second mouse press, dragged with the mouse.
- Layout never mirrors (Slice 96); the header keeps Dino's header rules (`GrownUpHeader` is for pages; reorder mode is a `GalleryPage` state like grown-up mode).

## Work

- [ ] Core: `ShelfOrder` (stored order of book ids, move-before rule, new books first, unknown ids last, removed ids dropped), unit tests including the 1,2,3,4,5,6 example; `FileStoryLibrary.ListAsync` returns books in that order.
- [ ] `BookSheetPlan`: a Reorder row (grown-up mode, every book).
- [ ] `GalleryPage`: reorder state with wiggle, lift, lit-up target spot, animated shifting, Done and Cancel; long press 1 s enters it; leaving grown-up mode (`GrownUpMode.Leave`) cancels it.
- [ ] Android task: drag on the emulator. iOS task: the same gestures on iPhone (MAUI drag/drop and long press on iOS; check with `library-docs` that the gesture APIs used work on both before building).
- [ ] Strings: Reorder, Done, Cancel in all 15 languages.

#### Design (Dino Reader Batch 47 Onboarding mock, frames 172-176, added 2026-11-04)

Source: `docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`, 5 frames: 172 book sheet (line 289), 173 wiggle (314), 174 drag (327, a live loop of 5 phases at 1.2 s; the grey caption line at its foot is only a caption), 175 back in the corner (341), 176 Windows 1100x760 (368; window chrome is illustration). The mock agrees with the plan and adds the details below.

- **Sheet row (172, `BookSheetPlan`).** Frame 100 plus one full-width row "Reorder": 48 high, radius 24, 2 dp `#c9c0f5` border, `Brand` 15/600, six-dot grip icon left (gap 8), placed between Record/Cast and "Remove from the shelf". Every book kind gets it. Tap closes the sheet and opens 173 with this book already lifted.
- **Reorder state (173, `GalleryPage`).** Not a page. The `Ink` bar stays; Cancel (white 15/600, left), title "Reorder" 19/600 centred, Done (white pill, `Ink` text, 44 high, right). The ⋯ dots, Add / Store and the coral way back hide. Hint 13.5 `Body`: "Drag a book to a new place. Your child’s shelf shows the same order." Tapping a book only lifts it. Back = Cancel; `GrownUpMode.Leave` cancels.
- **Motion (173-174).** Wiggle ±1.6° rotation, 0.26 s ease-in-out alternate, each cover offset 0.07 s from the next. Lift: scale 1.10 (0.2 s), no wiggle, shadow `0 16 26 rgba(43,37,64,.32)` (about 0.32 alpha `Ink`), title hidden, 10 dp up. Target spot: `Brand` at 14% fill with a 3 dp dashed `Brand` ring, same size as a cover. Others slide 0.35 s, ease-out `cubic-bezier(.2,.8,.3,1)`, wrapping rows. Dragging near the top or bottom edge scrolls the grid. Example: 1..6 with 5 before 3 gives 1,2,5,3,4,6.
- **Reduce motion.** No wiggle; each cover gets a 2 dp dashed outline.
- **Done (175).** Saves and returns to the grown-ups' corner, not the kids' shelf. Cancel lands there too with the old order. The ink bar, ⋯ dots (30 dp white circles at top 4 / right 4), Add a book / Store (52 high) and the coral "Back to my child’s shelf" (56, `Coral`) return. Grid is 3 across, radius 12, titles 12.5/600. New books go to the front; the order survives a restart.
- **Windows (176).** 1100x760, 6 across, gap 18, side padding 40, cover radius 14, title 13.5/600; header Cancel is a 40 high pill (`white` 12%), Done a 40 high white pill. Enter from ⋯ > Reorder or by holding the mouse button on a cover for 1 s (open-hand cursor while held, closed hand dragging). Esc = Cancel, Enter = Done. Keyboard: with a cover focused, Space lifts, arrow keys move, Space drops. The lifted cover in the frame is scaled 1.08 and rotated -3°.
- **Files.** `GalleryPage.xaml(.cs)`, `ShelfReorder*.cs`, `BookSheetPlan.cs`, `AppResources*.resx`.
- **New strings (15 locales).** "Reorder" (row and title), "Drag a book to a new place. Your child’s shelf shows the same order." Cancel, Done and "Back to my child’s shelf" (`Gallery_BackToShelf`) exist.

**Open point: reorder header vs `GrownUpHeader`.** `src/DinoReader.App/CLAUDE.md` says every grown-up page header is `GrownUpHeader` (back left, one Done right). Reorder is a `GalleryPage` state (slice says so) with Cancel left and a white Done pill, which `GrownUpHeader` cannot draw. **Default:** a state-specific bar on `GalleryPage`, never mirrored, light only; not a page, so the rule does not apply.
**Open point: lift scale.** Phone 1.10 (174), Windows 1.08 (176). **Default:** 1.10 on both.
**Open point: keyboard reorder.** Space/arrow keys, Esc and Enter are not in the slice. **Default:** build them on Windows only; add a pointer-free path to Reorder for TalkBack/VoiceOver (the ⋯ sheet row already is one).
**Open point: the 1 s press on Android/iOS.** Slice and mock agree (1 s); mock adds nothing for the phone beyond the sheet row. **Default:** as the slice.
**Mock decides (slice silent):** hint line; nothing opens on tap in reorder mode; Cancel also lands in the corner.

## Parked (night 2026-11-04-1441 wave 1) - RESOLVED 2026-11-05

Resolved: the LayoutCycleException cause was found (next section) and fixed (see *Changed while building*); the reorder mode opens on the VM with 0 layout-cycle lines. The deterministic order (ThenBy Id), the second-open layout and the 1 s press regression from check pass 4 are fixed too. Original parked text, kept as history:

Windows head crashes when reorder mode opens (three VM rounds; the third design fix did not cure it). Draft PR #799, WIP pushed on feat/s264-shelf-reorder. The WinUI hook names it: `Microsoft.UI.Xaml.LayoutCycleException: Layout cycle detected` about 50 ms after `page open end`, no stack (logs: D:/ReadTheStupidText-worktrees/reviews/s264-vm-diag3/ and s264-vm-diag4/). Tried: no MAUI Shadow on the lift (diag2, still crashed); Relayout only on a width change and surface height from content only (diag4, still crashed). Android works (1 s press, drag, Done, Cancel all pass on the emulator). Next: find the element in the cycle (bisect by turning off wiggle, lift Border, lit spot and the header pill one at a time on the VM), then fix. Also open from check pass 4 (reviews/s264-check4.md): default shelf order of same-time books is not deterministic (add ThenBy Id in FileStoryLibrary.cs:49), _lastLayoutWidth not reset in Open (tiles at 0,0 on a second open), async void OnPointerPressed without try/catch. Emulator run 5 (reviews/s264-emu5/): the 1 s press no longer enters reorder (it opens the book sheet; regression from the hold-cancel-on-tap change after run 4, where the press passed); the flow passes every step but its expect-log markers need a trailing comma/colon (run-flows.ps1:94 matches whole tokens); default order was the same on two fresh launches.

## Cause found 2026-11-04 ~21:10 (diagnosis run in the main session, not a wave; report `D:/ReadTheStupidText-worktrees/reviews/s264-cycle-cause.md`, logs `reviews/s264-vm-diag5/`, `s264-vm-diag6/`)

- **Proven by WinUI's own trace** (`DebugSettings.LayoutCycleTracingLevel = High`, read from OutputDebugString by a new `Platforms/Windows/LayoutCycleTrace.cs` into `window-trace.log`): the loop is the cover's `AspectRatioView`. Each Arrange pass invalidates its Measure again, 8 times, then WinUI throws. `ArrangeOverride` does `if (Math.Abs(bounds.Width - lastMeasuredWidth) > 0.5) InvalidateMeasure();`; the reorder tile is 189.6 dp wide (`ShelfOrderGrid.Fit`), layout rounding measures the cover at 189 and arranges it at 190, so the difference of 1 re-triggers forever. The normal shelf has whole-number widths, which is why only reorder mode crashed. The lift shadow was ruled out again (diag6: no shadow, same crash).
- **Fix (done, see *Changed while building*):** whole-number tile width in `ShelfOrderGrid.Fit`, and a threshold above layout rounding in `AspectRatioView` (more than 1 px), with a unit test on the rule. Then the Windows reorder check on the VM.
- **The VM package is a Release build** (`Build-TestPackage.ps1` publishes `-c Release`). The trace listener and the High level are in the WIP uncommitted and not Debug-only: gate them (Debug or a test switch) before merge, or keep them behind the device-test switch on purpose and say so.
- **Android 1 s press (likely, from the code):** `TileOverlay.xaml:18` wires `PointerExited` and `PointerReleased` to `OnPointerEnded` → `CancelHold()`, and `OnPointerMoved` cancels past 12 dp; Android probably sends Exited or Moved during a still press. Confirm with one emulator run logging Pressed/Moved/Exited/Released for a 1.5 s press, then fix.
- **Status: planned again** (resume from branch `feat/s264-shelf-reorder`, worktree `s264`, draft #799); the WIP has the trace files uncommitted.

## Changed while building (review round, 2026-11-05)

- **Hold gesture:** the 1 s press is one pure Core state machine, `HoldGesture` (Idle -> Pressed -> Fired or Cancelled, every release back to Idle; a tap is swallowed only when that same press fired and the reorder layer did not take the release; a move past 12 dp, the list scrolling, hide, unload or a new binding cancels). `TileOverlay` only feeds it events and a timer; every transition is unit-tested.
- **Remeasure threshold:** `AspectRatioView` keeps 0.5 dp on Android/iOS and uses 1 dp only on Windows, one rule `ShelfOrderLayoutRules.NeedsRemeasure(..., isWindows)`. `SaveOrderAsync` now holds the library gate across read, merge and replace.
- **Windows LayoutCycleException fixed:** whole-dp tiles (`ShelfOrderGrid.Fit`, the left margin split evenly) plus the `NeedsRemeasure` rule above. `LayoutCycleTrace`, the `App.xaml.cs` hook, `ReorderTrace`, `SheetLog` and `TraceSize` exist only in DEBUG or `DINO_TEST_BOOKS` builds.
- **Android 1 s press regression fixed:** `PointerExited` no longer cancels the hold. `StoryList.Scrolled` cancels pending holds.
- **Order rules:** list order is `ThenBy Id`. `SaveOrderAsync` runs under one gate and merges with the stored order (`ShelfOrder.MergeShown`: books not shown keep their place); stale ids are pruned. A cancelled drag, or a drop outside the grid, restores the pre-drag order. The intro wizard and backup sort explicitly (first added / newest); the shelf uses the stored order. Cancel is ignored while Done is saving.
- **Known behaviour (kept):** a slow finger move during the 1 s press on a shelf that does not scroll still opens reorder, like the iPhone home screen. Android sends no pointer-move during the press (likely, not proven).

## From review

- `reviews/s264-high.md` (10 findings): applied 1, 2, 3, 5, 6, 9. Skipped 4 (loosening `AspectRatioView` for all platforms: the trace proved a 1 dp rounding loop; later limited to Windows by medium 1). Skipped 7 (triple debug-log helper), 8 (`ForgetInOrderAsync` duplicates the write sequence) and 10 (methods over 30 lines): refactors.
- `reviews/s264-medium.md` (10 findings): applied 1, 2, 3, 5, 6, 8, 9. Kept 4 on purpose (the diagnostic trace stays in test builds; header fixed). Skipped 7 (books added during the mode are missed by Done): reorder is modal. Skipped 10 (`MergeShown` O(n^2)): n is small.
- `reviews/s264-check5.md` (7 findings): applied 1, 2, 3, 5. Skipped 4 and 7 (style). 6 (touch press on `PointerGestureRecognizer`) is device-only, see Not yet verified.
- `reviews/s264-check6.md` (8 findings): none applied; recorded as follow-ups: (1) shelf-order ids not checked with `StoryIds.IsValid` in `FileStoryLibrary.SaveOrderAsync`; (2) `TrySaveOrderAsync` catches only IO errors, any other save failure shows no alert; (3) `DropCompleted` without `_dropAccepted` may undo a valid drop (unconfirmed on devices); (4) static `ScrollStarted` handler and recycled overlays; (5) the 800 ms tap suppression is not cleared on close; (6) `MergeShown` can put an id at the front with a duplicated stored id; (7) other `ListAsync` callers (FirstBooksPage, NarratorPage, TranslationPackPrompt, StoryBackupReader) still get shelf order; (8) `HoldGesture.Cancel` after Fired drops the swallow on a relayout. 6-8 are low.

## Device runs (2026-11-05)

- VM (`reviews/s264-vm7`, `s264-vm8`, key ee54a281b83a): reorder opens from the ⋯ sheet with 0 layout-cycle lines, a 1.3 s mouse hold enters, Esc cancels.
- Emulator (`reviews/s264-emu8`, key ee54a281b83a): `shelf-reorder.yaml` PASS (107 s, all markers), a 1.5 s touch press enters reorder, a normal tap after Done is not swallowed. Earlier emulator failures (`s264-emu6`, `s264-emu7`) came from a degraded emulator (black screencaps, unrelated flows failing); after a restart main's control flow passed.

#### For CLAUDE.md (lifted 2026-11-05)

- (`src/DinoReader.App/CLAUDE.md`) A shared view that remeasures on a width difference (`AspectRatioView`) must use a threshold above layout rounding on Windows (`ShelfOrderLayoutRules.NeedsRemeasure`: 1 dp Windows, 0.5 dp Android/iOS) and tiles must have whole-dp widths (`ShelfOrderGrid.Fit`), or WinUI throws `LayoutCycleException`. (Slice 264: docs/plan/slices/264-dino-gallery-reorder-mode.md)
- (`src/DinoReader.App/CLAUDE.md`) The 1 s hold lives in Core `HoldGesture`; `TileOverlay` only feeds it. `PointerExited` must not cancel it (Android sends it during a still press); scrolling cancels it. (Slice 264: docs/plan/slices/264-dino-gallery-reorder-mode.md)
- (`src/DinoReader.Core/CLAUDE.md`) Shelf order is stored in a file and saved through `FileStoryLibrary.SaveOrderAsync` under the library gate, merged with `ShelfOrder.MergeShown`; callers that need newest-first or first-added sort explicitly. (Slice 264: docs/plan/slices/264-dino-gallery-reorder-mode.md)
- Diagnostic traces (`LayoutCycleTrace`, `ReorderTrace`) stay behind DEBUG or `DINO_TEST_BOOKS`; the VM package is a Release build. (Slice 264: docs/plan/slices/264-dino-gallery-reorder-mode.md)

#### Not yet verified

- Windows mouse drag actually moving a tile, Done/Enter saving it and the order after a restart, Esc after a drag (a synthetic drag moved nothing; no drag trace lines).
- A real scroll during a press cancelling the hold (the 4-book test shelf does not scroll).
- iOS gestures (`PointerGestureRecognizer` touch on iOS).
- Windows keyboard reorder (Space/arrows).
- The 6-book 1,2,5,3,4,6 example on a device (unit-tested only).

## Verification

- `dotnet test tests/DinoReader.Tests` (order rules); emulator flow `shelf-reorder.yaml` (enter from ⋯, drag 5 before 3, Done, order is 1,2,5,3,4,6 after a restart; Cancel restores); VM check for the Windows mouse path.
