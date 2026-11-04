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

## Verification

- `dotnet test tests/DinoReader.Tests` (order rules); emulator flow `shelf-reorder.yaml` (enter from ⋯, drag 5 before 3, Done, order is 1,2,5,3,4,6 after a restart; Cancel restores); VM check for the Windows mouse path.
