# Slice 391 — Dino Windows: Enter on the first-run story-languages step opens no book picker
**Product:** Dino

*Planned.*

**Touches:** `src/Dino/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`, `src/Dino/DinoReader.App/StoryLanguagesPage.cs`, `src/Dino/DinoReader.Core/DinoKeyMap.cs`, `tools/desktop-vm/checks/dino-onboarding-keys.ps1`, `docs/plan/slices/391-dino-windows-first-run-enter-on-story-languages.md`

## Why

The wave 6 VM run (2026-11-15) on Slice 382's build failed the prerequisite check `dino-onboarding-keys` twice: "Enter on the story-languages step did not open the book picker (waited 30 s)". The window trace says "Dino keyboard: Enter not used (FirstRunPage, no target)". Logs: `D:/ReadTheStupidText-worktrees/reviews/wave6-device/vm-reorder.log` and `vm-reorder2.log`. Slice 382 changed no first-run code; it is only the context (`plan-not-needed`). Because of this failure `dino-reorder` never ran on the VM, so Slice 382's Windows check is open.

## Work

- [ ] 1. Run `dino-onboarding-keys` on main's build to tell whether main has the failure, and read `DinoKeyboard` and `FirstRunPage` to find why Enter has no target (file:line).
- [ ] 2. Fix it on the Windows head (the keyboard path is Windows-only), with the VM check passing.
- [ ] 3. Then run `dino-reorder` on the VM (Slice 382's open check, [slice](382-dino-reorder-held-book-wiggles-plain-drag.md)).

## Verification

`dino-onboarding-keys` and `dino-reorder` pass on the VM.
