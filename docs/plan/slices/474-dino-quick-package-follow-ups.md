# Slice 474: Dino: quick-package follow-ups (review findings and slow-link list timeout)

**Product:** Dino

*Planned 2026-11-28 by plan-cleanup, not started.*

Moved out of Slice 463 (plan cleanup 2026-11-28). Code is the quick first-book package already on main (#1444, #1448).

**Touches:** `src/Dino/DinoReader.App/StoreClient.cs`, `StagedBookUpdates.cs`, `AudioPackStore.cs`, `FirstBookSwitch.cs`, tests under `tests/Dino/DinoReader.Tests/`.

**Work:**
- [ ] 1. F4: `SlowLinkWatch` (StoreClient.cs:648) uses `item.Bytes`; FirstBooksPage.cs:271 already sets `Bytes` to `BytesFor(size)`, so first check whether a slow high-res fetch can still switch to quick; fix with `item.BytesFor(size)` only if it cannot, and say so if already correct.
- [ ] 2. F8: the first-books store list timed out twice under an emulator `umts` link (ListTimeout 20 s, StoreClient.cs:207). Reproduce, decide whether the timeout or retry budget is too short for a slow link, fix, so the switch can be judged.
- [ ] 3. F10: classify a staged-package fault as transient only for IO, Unauthorized, Cancelled and ShelfRace, the rest bad (`StagedBookUpdates.cs`).
- [ ] 4. F11: `AudioPackStore.MoveToTrash` returns bool, falls back to `DeleteFolder`, drops `_opened` only on success.
- [ ] 5. F12: `DeleteBookAsync` clears `_opened` entries of the book's versions.
- [ ] 6. F13: dedupe leftover trash paths re-queued by a failed recovery pass.
- [ ] 7. F14: `UpgradeQuickPackAsync` re-checks cancel after the install gate wait.
- [ ] 8. F15: `AudioPackStore.Open` runs `EmptyTrash` on a background task after a kill.
- [ ] 9. F16: use a short `.trash` suffix so paths stay under MAX_PATH.
