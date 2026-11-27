# Slice 465 — Dino: the store-restore, paid-books and subscription flows pass on a real phone
**Product:** Dino

**Touches:** `.maestro/dino/store-restore.yaml`, `.maestro/dino/paid-books.yaml`, `.maestro/dino/subscription.yaml`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/Testing/**`, `tests/Dino/DinoReader.Tests/**`

*Planned 2026-11-27 (index pass, from Slice 448's S21 run 4: 1 of Slice 396's 4 flows passed, these 3 failed).*

## Why
Slice 448 made `tools/phone/run-flows.ps1` run the `.maestro/dino` flows on the leased S21 (docs/plan/slices/448-tools-phone-runs-maestro-dino-flows.md). Its run 4 passed one flow and failed these three, each inside the store flows. Until they pass, a real-phone run cannot check buying, restoring or the subscription shelf.

## Work items
- [ ] 1. Read the S21 run 4 logs in `D:/ReadTheStupidText-worktrees/reviews/s448-device/run4/`. The three failures:
  - `store-restore`: the assert `Nothing bought with this .* yet\.` is not visible.
  - `paid-books`: the id `StoreRestoreResult` is not visible.
  - `subscription`: the marker `[Subscription] shelf columns=3 grownUp=True` is missing.
- [ ] 2. For each, find whether the flow or the app is wrong on a real phone (the emulator passes them), using `src/Dino/DinoReader.App/StorePage.xaml.cs` and the test hooks under `src/Dino/DinoReader.App/Testing/`.
- [ ] 3. Fix it (flow, app or test hook) with a test in `tests/Dino/DinoReader.Tests/` where the cause is in code.
- [ ] 4. Rerun on the S21: `pwsh -NoProfile -File tools/phone/run-flows.ps1 -App dino -Flow store-restore,paid-books,subscription -Install`; all three must pass.
