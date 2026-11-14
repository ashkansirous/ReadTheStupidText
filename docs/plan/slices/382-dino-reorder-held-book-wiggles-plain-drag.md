### Slice 382 — Dino reorder: the held book wiggles, a drag needs no hold in edit mode, covers are sharp on first open
**Product:** Dino

*Planned.*

**Touches:** `src/Dino/DinoReader.App/ShelfReorderView.cs`, `src/Dino/DinoReader.App/ShelfReorderTiles.cs`, `src/Dino/DinoReader.App/StoryCover.xaml`, `src/Dino/DinoReader.App/StoryCover.xaml.cs`, `src/Dino/DinoReader.Core/ShelfDragSession.cs`, `src/Dino/DinoReader.Core/ReorderPan.cs` (new, only if the drag start rule is pulled out as pure code), `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/shelf-reorder.yaml`, `tools/desktop-vm/checks/dino-reorder.ps1`, `src/Dino/DinoReader.App/CLAUDE.md`

**Why.** The user reported this on 2026-11-14, Dino 1.129.0 (Pixel 10 Pro), with a screenshot of the Reorder screen showing blurred covers:
- "the one that I kept my finger on, does not wiggle"
- "the first try, the images were faded, but it fixed the second try"
- "after it goes to edit mode, I still need to keep my finger down on the books before it is picked up, instead of simple drag and drop."

**Evidence (2026-11-14).**
- Code, read from origin/main: the wiggle is an `Animation` on `Rotation` (`ShelfReorderTiles.cs` 60-66, 131-152). Lifting a tile stops its wiggle on purpose (`ShelfReorderView.cs` 411-414). This follows the mock's "Lift: scale 1.10, no wiggle" (Slice 264, `docs/plan/slices/264-dino-gallery-reorder-mode.md`, Design, Motion). The user's answer below replaces that one detail.
- A drag starts through MAUI's `DragGestureRecognizer` (`ShelfReorderView.cs` 163-165). On Android and iOS that is the system drag-and-drop, which itself needs a long press. So even in edit mode every book needs a second hold.
- The hint today is "Press and hold a book, then drag it to a new place. …" (`Gallery_ReorderHint`). The mock's hint is "Drag a book to a new place. Your child's shelf shows the same order." (Slice 264, Design).
- Covers come from the shared `StoryCover` control (`ShelfReorderTiles.cs:80`). Why they look blurred on the first open was not found in code. Item 4 finds it first.
- Not reproduced on a device yet: the emulator run ran out of its 10 minutes before the reorder screen.

**Decisions (user, 2026-11-14).**
- The book under the finger keeps wiggling and grows a little (the 1.10 lift scale), so it is clearly picked up. It does not stop wiggling.
- Once the shelf is in edit mode, touching a book and moving the finger drags it straight away, with no hold. A hold is needed only once, to enter edit mode. The hint text says that, in all 15 languages.
- Covers show sharp straight away. If a cover takes more than 500 ms, it shows a loading state, never a blurred or faded picture.
- Android and iOS in this slice; the Windows head keeps working (mouse drag and keyboard path).

**Work.**
- [ ] 1. Smallest end-to-end first, Android: in edit mode, a touch-down on a cover followed by a move starts the drag at once. Use a pan gesture (or the platform touch events) instead of the system drag-and-drop for touch. Check the MAUI `PanGestureRecognizer` / pointer APIs on Android and iOS with `library-docs` first. Keep all the existing drag rules: the lit target spot, others sliding, edge scrolling, Cancel restores the old order, Done saves (Slices 264, 298, 321, and `ShelfDragSession`'s rules in `src/Dino/DinoReader.Core/CLAUDE.md`). A tap still only lifts. A scroll of the grid that starts between covers still scrolls.
- [ ] 2. iOS task: the same drag on iPhone.
- [ ] 3. The lifted / dragged book keeps the wiggle at the same angle and speed as the others, at scale 1.10 with the lift shadow. Reduce motion still means no wiggle anywhere (the dashed outline stays).
- [ ] 4. Find why covers are blurred on the first open: read `StoryCover` and how the reorder tiles load their image (thumbnail, placeholder or fade). Write the cause here with file:line, then fix it so the cover is sharp at once. If the picture is not ready within 500 ms, show a loading state on the tile instead of a blurred picture.
- [ ] 5. Hint text: the mock's line "Drag a book to a new place. Your child's shelf shows the same order." in all 15 locales (`Gallery_ReorderHint`).
- [ ] 6. Pure parts (when a touch becomes a drag, for example a move threshold) in Core with unit tests.
- [ ] 7. Update `.maestro/dino/shelf-reorder.yaml` to drag with a plain swipe in edit mode (no hold), check the dragged book is still wiggling (a log line from the wiggle), and check the covers on the first open after a fresh install (a log line when a cover shows its picture, with the time it took). Keep `tools/desktop-vm/checks/dino-reorder.ps1` passing.

**Verification.**
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator, fresh install: covers are sharp on the first open; in edit mode a plain swipe moves a book; the held book wiggles; Done saves and Cancel restores.
- S21: the same.
- iOS: compile on Windows; simulator workflow or TestFlight for the same checks.
- Windows VM: `dino-reorder.ps1` passes.
