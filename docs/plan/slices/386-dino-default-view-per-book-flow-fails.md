# Slice 386 — Dino: the `default-view-per-book` flow fails on main
**Product:** Dino

*Planned.*

**Touches:** `.maestro/dino/default-view-per-book.yaml`, the Dino code the first work item names as wrong (`src/Dino/DinoReader.App/**`, only if the app is the faulty side), `docs/plan/slices/386-dino-default-view-per-book-flow-fails.md`

## Why

Slice 378's device run (2026-11-14) ran the Dino flows on a build of `main`. The flow `default-view-per-book` fails with:
`Assertion is false: "Picture only.*Selected" is visible`.
Logs: `D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/` (`dino-main-maestro.log`, `main-dino.log`). The flow comes from [Slice 196](196-dino-default-view-per-book.md).

## Work

- [ ] 1. Read the flow and the logs and find the cause: the app shows no "Selected" state for the picture-only view, or the flow's text no longer matches (state which, with file and line).
- [ ] 2. Fix it on the faulty side. If the app: Android and iOS alike.

## Verification

- `default-view-per-book` passes on the emulator, in the same form as the failed run.
