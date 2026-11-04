# Slice 264 — Dino: grown-ups reorder the shelf (iPhone-style reorder mode)

*Planned 2026-11-04.* Dino Reader; Android and iOS alike; the Windows head gets the same mode with the mouse.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ShelfReorder*.cs` (new), `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/ShelfOrder*.cs` (new), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/shelf-reorder.yaml` (new), `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/264-dino-gallery-reorder-mode.md`.

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

## Verification

- `dotnet test tests/DinoReader.Tests` (order rules); emulator flow `shelf-reorder.yaml` (enter from ⋯, drag 5 before 3, Done, order is 1,2,5,3,4,6 after a restart; Cancel restores); VM check for the Windows mouse path.
