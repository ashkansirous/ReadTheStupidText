# Slice 298 — Slice 264 gallery reorder follow-ups

*Planned 2026-11-07.* Findings from check6 of [Slice 264](264-dino-gallery-reorder-mode.md), not built. Moved from plan.md's *Outstanding work*. All code is shared MAUI, so Android and iOS get each fix together; the gesture items need a look on both.

**Touches:** `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/ShelfOrder.cs`, `src/DinoReader.Core/HoldGesture.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.Core/CLAUDE.md`, `tests/DinoReader.Tests/**`, `docs/plan/slices/298-slice-264-gallery-reorder-follow-ups.md`

## Work

- [ ] 1. `FileStoryLibrary.SaveOrderAsync`: check every shelf-order id with `StoryIds.IsValid`, and refuse the save on an invalid one. Test.
- [ ] 2. `GalleryPage.xaml.cs` `TrySaveOrderAsync` catches only IO errors; catch what `SaveOrderAsync` can throw (or let the library return a result) and report a fallback. Keep the `FallbackGuard` baseline from going up.
- [ ] 3. `DropCompleted` without `_dropAccepted` may undo a valid drop: decide the undo only when no drop was accepted. Test with a fake drop sequence.
- [ ] 4. The static `ScrollStarted` handler and recycled overlays (`TileOverlay.xaml.cs`): make the handler instance-bound, unsubscribe on close.
- [ ] 5. The 800 ms tap suppression is not cleared on close: clear it when the reorder mode closes.
- [ ] 6. `FileStoryLibrary.MergeShown` can duplicate a stored id: de-duplicate while merging. Test.
- [ ] 7. Other `ListAsync` callers still get shelf order: list which callers need the library's own order and give them it, or document why shelf order is right.
- [ ] 8. `HoldGesture.Cancel` after `Fired` drops the swallow on a relayout: keep the swallow until the touch ends. Test in `HoldGesture` tests.

## Verification

- Unit tests for items 1, 3, 6, 8 green; emulator and an iOS check of reorder: hold, drag, drop, scroll, close, and a relayout during a hold.
