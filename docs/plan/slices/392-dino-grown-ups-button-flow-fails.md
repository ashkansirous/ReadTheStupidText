# Slice 392 — Dino: the `grown-ups-button` flow fails on main (GalleryDone still visible on Settings)
**Product:** Dino

*Built 2026-11-15 (wave 2026-11-15), merged as PR #1180 (d313444f). Verified on the Android emulator.*

**Touches:** `.maestro/dino/grown-ups-button.yaml`, `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/GrownUpHeader.cs`, this slice file.

## Why

Found in wave 8's device run (2026-11-15): `.maestro/dino/grown-ups-button.yaml` step 19, `Assert that id: GalleryDone is not visible... FAILED`, after `Tap on id: GallerySettings` with `SettingsTitle` visible. It fails the same way on main's build `90f93c61710d` and on Slice 384's build, so no open slice caused it. Logs: `D:/ReadTheStupidText-worktrees/reviews/wave8-device/grown-ups-button-main.log` and `grown-ups-button-run4.log` (outside the repo; they may be deleted).

## Work

- [x] 1. Find whether the app or the flow is wrong: read main's `GalleryPage` / Settings navigation and `GrownUpHeader.cs` against what the flow expects (file:line), and say which side is at fault.
  - **Finding: the flow is stale, the app is right.** Since Slice 325 Settings is a tab inside the grown-ups' corner (`GalleryPage.xaml.cs:1708` `SelectTabAsync` calls `_mode.SelectTab`), not a pushed page, so the coral pill `GalleryDone` (`CornerShell.cs:141`) is part of the corner chrome and stays on screen on every tab (Slice 325 decision 1). Android Back on the Settings list with no sub-page open calls `CompleteHandover()` and leaves to the kids' shelf (`GalleryPage.xaml.cs:1899-1906`). The flow (last edited by Slice 278, before Slice 325 reached it) still expected the old pushed Settings page: no `GalleryDone`, and Back returning to grown-up mode first.
- [x] 2. Fix it (the app if a hidden `GalleryDone` is still shown on Settings; the flow if its expectation is stale).
  - Done in the flow only: on Settings it now asserts `GalleryDone` visible, and one `back` goes to the kids' shelf. No app change.
- [x] 3. Rerun `grown-ups-button` on the Android emulator in the same form as the failing run.
  - Done 2026-11-15 on emulator-5554, `run-flows.ps1 -App dino -NoBuild`, cache `9cd4e416bae3`: `grown-ups-button` PASS (48.8 s); `dino-redesign-corner` PASS (60.8 s), so it holds no stale Settings expectation. Logs: `D:/ReadTheStupidText-worktrees/reviews/wave9-device/` (outside the repo; may be deleted).

## Verification

`grown-ups-button` passes on the emulator. Met (item 3).

*Changed while building:* the fix is in the flow only (`.maestro/dino/grown-ups-button.yaml`); no app change.

*From review:* no finding applied, each one is listed below with its reason (files outside the repo, may be deleted: `reviews/pr-1180-high.md`, `reviews/pr-1180-medium.md`).
- Skipped #1 (`reviews/pr-1180-high.md`): "Reworked flow not yet rerun" - the emulator rerun ran (item 3) and passed.
- Skipped #2 (`reviews/pr-1180-high.md`): "`assertVisible: GalleryDone` is weak and drops the old" - the flow already asserts `SettingsTitle` visible on Settings; `GalleryDone` visible only documents the corner chrome.
- Skipped #3 (`reviews/pr-1180-high.md`): "other flows tapping GallerySettings may hold stale pre-Slice-325 expectations." - `dino-redesign-corner` ran and passed.
- Skipped #4 (`reviews/pr-1180-high.md`): "finding cites line numbers" - low, doc drift only.
- Skipped #1 (`reviews/pr-1180-medium.md`): "no longer proves Settings is a tab, not a pushed page." - the flow already asserts `SettingsTitle` visible on Settings; `GalleryDone` visible only documents the corner chrome.
- Skipped #2 (`reviews/pr-1180-medium.md`): "single back relies on CompleteHandover closing a possible sub-page" - low; the rerun passed.
- Skipped #3 (`reviews/pr-1180-medium.md`): "status/finding read as fixed but work item 3" - the emulator rerun ran (item 3, PASS).
- Skipped #4 (`reviews/pr-1180-medium.md`): "cited GalleryPage.xaml.cs:1899-1906 is approximate" - low, doc drift only.
- Skipped #5 (`reviews/pr-1180-medium.md`): "Touches line lists app files" - low, not worth a change.
- Skipped #6 (`reviews/pr-1180-medium.md`): "flow name and earlier comments still say" - low, doc drift only.

#### Not yet verified

None.
