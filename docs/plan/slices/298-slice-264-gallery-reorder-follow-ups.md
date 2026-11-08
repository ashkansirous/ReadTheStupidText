# Slice 298 — Slice 264 gallery reorder follow-ups

*Built 2026-11-07 (wave 2026-11-07), merged as PR #903 (ca585cff). Emulator flows pass; the gesture checks below are not yet verified on a device.* *Planned 2026-11-07.* Findings from check6 of [Slice 264](264-dino-gallery-reorder-mode.md), not built. Moved from plan.md's *Outstanding work*. All code is shared MAUI, so Android and iOS get each fix together; the gesture items need a look on both.

**Touches:** `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/ShelfOrder.cs`, `src/DinoReader.Core/HoldGesture.cs`, `src/DinoReader.Core/ShelfDragSession.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.App/ShelfReorderView.cs`, `tests/ReadTheStupidText.Tests/FallbackGuardBaseline.txt`, (item 7 only if a caller must change: `src/DinoReader.App/{BackupPage,FirstBooksPage,IntroWizardPage.Art,StarterStories,StagedBookUpdates,StoreClient}*.cs`), `src/DinoReader.Core/CLAUDE.md`, `tests/DinoReader.Tests/**`, `docs/plan/slices/298-slice-264-gallery-reorder-follow-ups.md`

## Work

- [x] 1. `FileStoryLibrary.SaveOrderAsync`: check every shelf-order id with `StoryIds.IsValid`, and refuse the save on an invalid one. Test.
- [x] 2. `GalleryPage.xaml.cs` `TrySaveOrderAsync` catches only IO errors; catch what `SaveOrderAsync` can throw (or let the library return a result) and report a fallback. Keep the `FallbackGuard` baseline from going up.
- [x] 3. `DropCompleted` without `_dropAccepted` may undo a valid drop: decide the undo only when no drop was accepted. Test with a fake drop sequence.
- [x] 4. The static `ScrollStarted` handler and recycled overlays (`TileOverlay.xaml.cs`): make the handler instance-bound, unsubscribe on close.
- [ ] 5. Not done, decided: origin/main behaviour kept (the 800 ms tap suppression is not cleared on close). Reason: every way of clearing the window re-opened the hold-release tap (high 5, medium 5, check1 1, 2, 8), while keeping it only ignores a tap within 800 ms of opening the mode.
- [x] 6. `FileStoryLibrary.MergeShown` can duplicate a stored id: de-duplicate while merging. Test.
- [x] 7. Other `ListAsync` callers still get shelf order: list which callers need the library's own order and give them it, or document why shelf order is right.
- [x] 8. `HoldGesture.Cancel` after `Fired` drops the swallow on a relayout: keep the swallow until the touch ends. Test in `HoldGesture` tests.

## Verification

- Unit tests for items 1, 3, 6, 8 green; emulator and an iOS check of reorder: hold, drag, drop, scroll, close, and a relayout during a hold.

## Built (2026-11-07)

- 1: `SaveOrderAsync` throws `ArgumentException` on any id failing `StoryIds.IsValid` and writes nothing; test in `ShelfOrderTests`.
- 2: `GalleryPage.TrySaveOrderAsync` also catches `ArgumentException` (still a `no-fallback:` catch: the person is told the save failed; baseline unchanged, `FallbackGuardTests` green).
- 3: `ShelfOrder.ShouldUndoDrag(dropAccepted, dragStillRunning, hasOrderBeforeDrag)`; `ShelfReorderView.FinishDrag` undoes only while the drag is still running (a drop already ended it) and clears the snapshot. Fake drop sequence tests in `ShelfOrderTests`.
- 4: `TileOverlay` keeps a weak static list of live overlays (add on Loaded, remove on Unloaded); `NotifyScrollStarted` cancels each. No static event holds a recycled cell.
- 5: not done (see the item): `GalleryPage` tap suppression is as on origin/main; no swallow flag, no `CloseReleaseSwallow`, no close-tap flow.
- 6: `MergeShown` de-duplicates the shown ids (stored duplicates were already skipped); tests.
- 7: **Decision: shelf order stays for every caller; no caller changed.** Each `ListAsync` caller is a count (`BackupPage` load), already sorts explicitly (`BackupPage.SaveAsync`, `IntroWizardPage.Art`), builds a set or distinct list (`FirstBooksPage`, `StoreClient`, `StarterStories` known ids, `StoryBackupReader.KnownSourcesAsync`), or looks up by id or source id where duplicates are removed first or any match is the same book (`StarterStories`, `StagedBookUpdates`, `StoryBackupReader`, `DinoTestCommands`). None depends on newest-first, so the safe choice is to change nothing and keep the library's single contract (the grown-up's order). The gallery is the only caller that wants it.
- 8: `HoldGesture.Cancel` leaves a Fired hold in Fired (the swallow stays until Release or the next Press); tests.
- 3 (drag session, check pass 1): `ShelfDragSession.Reset` runs only in `ShelfReorderView.Close` (not in `ClearEntries`), so a rebuild mid-drag keeps the session and a cancel still restores the pre-drag order; `Begin` overwrites the state. A Drop after Completed counts only within `ShelfDragSession.LateDropWindowMilliseconds` (1000, clock passed in); a later stray Drop does nothing. Unit-tested.
- 2 (check pass 1, no change): every id the gallery shows passes `StoryIds.IsValid`: the shelf comes from `FileStoryLibrary.ListAsync`, which reads each folder through `TryReadAsync` (`FileStoryLibrary.cs:518` skips a folder whose name is not a valid id; `:535` skips a story whose stored id differs from the folder name, and `StoryJson.FromFile` (`StoryJson.cs:100`) throws on an invalid id). No bundled or legacy id reaches the reorder layer, so `SaveOrderAsync` keeps its throw and the `ArgumentException` catch stays as a guard.

**Not yet verified on a device (Android and iOS):** hold, drag, drop, scroll during a hold, close, a rotation/relayout during a hold, a cancelled drag after a rotation. No scripted flow covers items 3, 4, 8 (they need the system drag or a 1 s pointer hold).

**Review notes (check pass 2):**
- 6: `HoldGesture.Release` and `ShouldSwallowTap` take the current tick (callers pass `Environment.TickCount64`); a swallow left by a fired hold's release expires `HoldGesture.SwallowWindowMilliseconds` (500) after that release, so a stale one never eats a later tap however the cell is recycled; `Press` still clears it. Unit-tested.
- 1: skipped, decided in the slice (the 1 s late-drop window is a guess with no platform ordering confirmed).
- 2: skipped, the 1 s late-drop window is the decided trade-off (a stray Drop inside it re-applies the mid-drag order).
- 3: skipped, decided in the slice (the `ArgumentException` catch stays; ids reaching the gallery are valid).
- 4: skipped, trivial (`MergeShown` is O(n^2) on a shelf of a few dozen books).
- 5: skipped, low (the weak list is small and only touched on the UI thread).
- 7: skipped, the old recognizer's Completed restores the pre-drag id order, which is the right order for the rebuilt entries.

**Changed while building:** new pure `ShelfDragSession` (`src/DinoReader.Core/ShelfDragSession.cs`): Drop and DropCompleted are handled in either order; a Drop after Completed counts only within 1000 ms (clock passed in); `Reset` runs only on `ShelfReorderView.Close` and `Begin`. `HoldGesture.Release` and `ShouldSwallowTap` take the tick; a swallow left by a released fired hold expires 500 ms after the release. Item 5 not done (see Work).

**From review** (files in `D:/ReadTheStupidText-worktrees/reviews/`; each file read to its last finding). Each finding's outcome:

*s298-high.md (7):*
- 1 (HoldGesture.Cancel on rebind/hide leaves a stale swallow): applied, `Cancel` clears the swallow and a scroll uses `CancelKeepingSwallow`.
- 2 (stale comment on `CancelHold`): applied, comment fixed.
- 3 (DropCompleted before Drop reverts an accepted drop): applied, drag end moved into the pure `ShelfDragSession`, which handles both orders.
- 4 (UndoAfter test helper re-implements the view): applied, `ShelfDragSession` tests replace the copy of the view logic.
- Skipped #5 (`reviews/s298-high.md`): "the condition lives in two places and can drift" - style (`hasOrderBeforeDrag` duplicated condition).
- Skipped #6 (`reviews/s298-high.md`): "New ArgumentException guard validates only shown ids, not stored ids" - stored ids are only written by our own `SaveOrderAsync`.
- Skipped #7 (`reviews/s298-high.md`): "Register()/Unregister() each RemoveAll over the whole Live list on every Loaded/Unloaded" - shelves are tens of books.

*s298-medium.md (6):*
- 1 (stale `_endedWith` Drop after cancel): applied, Drop after Completed counts only within 1000 ms (Built, 3 drag session).
- 2 (swallow left armed after release then scroll): addressed by the 500 ms swallow expiry (check pass 2 note 6).
- Skipped #3 (`reviews/s298-medium.md`): "treats any ArgumentException as a normal save failure and only Debug.WriteLines it" - decided in the slice (see check pass 2 note 3).
- Skipped #4 (`reviews/s298-medium.md`): "NotifyScrollStarted allocates ToArray copy and RemoveAll on every scroll start" - low (see check pass 2 note 5).
- Not applied #5 (`reviews/s298-medium.md`): "also removes suppression for a hold release still in flight" - origin/main behaviour kept (item 5 was dropped).
- Not applied #6 (`reviews/s298-medium.md`): "Flow taps BookSheetReorder (not a 1 s pointer hold)" - item 5 was dropped, so the close-tap flow is gone from the build (Built 5: no close-tap flow).

*s298-check1.md (8):*
- Not applied #1 (`reviews/s298-check1.md`): "CloseReorder arms a 1500 ms swallow when closing within 800 ms of a hold-open" - item 5 dropped, origin/main behaviour kept.
- Not applied #2 (`reviews/s298-check1.md`): "OnOpenRequested (keyboard/Enter open) calls TakeSwallowNextOpen" - item 5 dropped, origin/main behaviour kept.
- Not applied #8 (`reviews/s298-check1.md`): "CloseReorder sets _suppressTapUntil = 0 unconditionally" - item 5 dropped, origin/main behaviour kept.
- 3 (Fired hold swallows the next tap after a scroll): addressed by the 500 ms swallow expiry.
- 4 (`_drag.Reset()` in the rebuild path): applied, `Reset` only in `Close` (Built, 3 drag session).
- 5 (no expiry on UndoneAwaitingDrop): applied, 1000 ms late-drop window.
- 6 (non-GUID id makes Done fail): no change, every shelf id passes `StoryIds.IsValid` (Built, 2 check pass 1 note).
- Skipped #7 (`reviews/s298-check1.md`): "Register/Unregister do O(n) RemoveAll per Loaded/Unloaded" - low.

*s298-check2.md (7):* #6 applied (the 500 ms swallow expiry, check pass 2 note 6); the rest are one line each, with the reasons also under *Review notes (check pass 2)*.
- Skipped #1 (`reviews/s298-check2.md`): "1000 ms late-drop window is a guess; if Drop arrives later on Windows the dropped order reverts silently" - decided in the slice.
- Skipped #2 (`reviews/s298-check2.md`): "a stray Drop within 1 s re-applies the mid-drag order and undoes the cancel (flicker)" - the 1 s late-drop window is the decided trade-off.
- Skipped #3 (`reviews/s298-check2.md`): "catching ArgumentException around whole save hides unrelated argument bugs" - decided in the slice (the catch stays; ids reaching the gallery are valid).
- Skipped #4 (`reviews/s298-check2.md`): "MergeShown uses result.Contains (linear) inside a loop" - trivial.
- Skipped #5 (`reviews/s298-check2.md`): "Register/Unregister mutate it unsynchronised and rescan fully" - low (the weak list is small and only touched on the UI thread).
- Skipped #7 (`reviews/s298-check2.md`): "old recognizers' DropCompleted applies stale _before to rebuilt entries" - the old recognizer's Completed restores the pre-drag id order, which is the right order for the rebuilt entries.

*s298-check3.md (7), all low and unchanged:*
- Skipped #1 (`reviews/s298-check3.md`): "Platform Press/Tapped ordering not checked" - low; not confirmed, and the next Press clears it.
- Skipped #2 (`reviews/s298-check3.md`): "A Release after a scroll-interrupted fired hold arms a fresh 500 ms swallow though no tap is expected" - low; a quick tap on the same cover within 500 ms is the cost.
- Skipped #3 (`reviews/s298-check3.md`): "A Drop arriving more than 1000 ms after Completed is discarded after the old order was restored" - the window is the decided trade-off.
- Skipped #4 (`reviews/s298-check3.md`): "the late Drop re-applies the dropped order with EndVisual=false, possibly flickering/mis-slotting" - low, unconfirmed on a device.
- Skipped #5 (`reviews/s298-check3.md`): "list mutated from Loaded/Unloaded/scroll callbacks with no thread guard" - low (UI-thread only).
- Skipped #6 (`reviews/s298-check3.md`): "a sample/legacy book would make every reorder Done fail" - no such id reaches the shelf.
- Skipped #7 (`reviews/s298-check3.md`): "the dedup hides an upstream duplicate" - cosmetic.

**Device (emulator, 2026-11-07, build key fdc0f216b731):** flows first-run, shelf-reorder (open from sheet, Cancel, Done, order survives restart), shelf-audio-on-start and dino-landscape-shelf all PASS. Logs: `D:/ReadTheStupidText-worktrees/reviews/s298-emu/`.

#### For CLAUDE.md (lifted 2026-11-07)

- `src/DinoReader.Core/ShelfDragSession.cs`: the platform may send Drop and DropCompleted in either order. Reset the session only in `ShelfReorderView.Close` and `Begin`, never on a rebuild mid-drag, and accept a Drop after Completed only within `LateDropWindowMilliseconds` (1000). (Slice 298: docs/plan/slices/298-slice-264-gallery-reorder-follow-ups.md)
- `src/DinoReader.Core/HoldGesture.cs`: `Cancel` leaves a Fired hold Fired (the swallow survives a relayout); a swallow after `Release` expires 500 ms later; callers pass `Environment.TickCount64`. (Slice 298: docs/plan/slices/298-slice-264-gallery-reorder-follow-ups.md)
- `FileStoryLibrary.SaveOrderAsync` throws `ArgumentException` on an id failing `StoryIds.IsValid`; `ListAsync` callers all get shelf order and none needs a different one. (Slice 298: docs/plan/slices/298-slice-264-gallery-reorder-follow-ups.md)

#### Not yet verified

- Real drag in both drop event orders (Drop then DropCompleted, and the reverse) on Android, iOS and the Windows head.
- A 1 s hold, and a scroll that cancels the hold, on Android, iOS and the Windows head.
- Hold release after a relayout (rotation) on Android, iOS and the Windows head.
- Windows keyboard reorder on the Windows head.
- iOS compile of the changes not run.
- Tablet-upright flow not run.
