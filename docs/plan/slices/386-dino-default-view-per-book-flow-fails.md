# Slice 386 — Dino: the `default-view-per-book` flow fails on main
**Product:** Dino

*In progress (wave 2026-11-15), draft PR.*

**Touches:** `.maestro/dino/default-view-per-book.yaml`, `src/Dino/DinoReader.App/Resources/Raw/testbooks/**` (the test-store book asset; premise check 2026-11-15: `good-enough.dino` likely lacks `textInPictures`, so `Story.cs:172` opens it in Picture + text), `tools/dino/DinoReader.BookBuilder/**` (only if the test book is made by it), `src/Dino/DinoReader.App/Testing/TestStore.cs`, `docs/plan/slices/386-dino-default-view-per-book-flow-fails.md`. Any other app file needs a parallel-check first (wave 6 builds 381 and 382 beside it).

## Why

Slice 378's device run (2026-11-14) ran the Dino flows on a build of `main`. The flow `default-view-per-book` fails with:
`Assertion is false: "Picture only.*Selected" is visible`.
Logs: `D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/` (`dino-main-maestro.log`, `main-dino.log`). The flow comes from [Slice 196](196-dino-default-view-per-book.md).

## Work

- [ ] 1. Read the flow and the logs and find the cause: the app shows no "Selected" state for the picture-only view, or the flow's text no longer matches (state which, with file and line).
- [ ] 2. Fix it on the faulty side. If the app: Android and iOS alike.

## Verification

- `default-view-per-book` passes on the emulator, in the same form as the failed run.
