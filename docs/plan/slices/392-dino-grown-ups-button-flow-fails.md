# Slice 392 — Dino: the `grown-ups-button` flow fails on main (GalleryDone still visible on Settings)
**Product:** Dino

*Planned 2026-11-15, not built.*

**Touches:** `.maestro/dino/grown-ups-button.yaml`, `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/GrownUpHeader.cs`, this slice file.

## Why

Found in wave 8's device run (2026-11-15): `.maestro/dino/grown-ups-button.yaml` step 19, `Assert that id: GalleryDone is not visible... FAILED`, after `Tap on id: GallerySettings` with `SettingsTitle` visible. It fails the same way on main's build `90f93c61710d` and on Slice 384's build, so no open slice caused it. Logs: `D:/ReadTheStupidText-worktrees/reviews/wave8-device/grown-ups-button-main.log` and `grown-ups-button-run4.log` (outside the repo; they may be deleted).

## Work

- [ ] 1. Find whether the app or the flow is wrong: read main's `GalleryPage` / Settings navigation and `GrownUpHeader.cs` against what the flow expects (file:line), and say which side is at fault.
- [ ] 2. Fix it (the app if a hidden `GalleryDone` is still shown on Settings; the flow if its expectation is stale).
- [ ] 3. Rerun `grown-ups-button` on the Android emulator in the same form as the failing run.

## Verification

`grown-ups-button` passes on the emulator.
