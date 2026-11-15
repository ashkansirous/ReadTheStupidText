# Slice 391 — Dino Windows: Enter on the first-run story-languages step opens no book picker
**Product:** Dino

*Built 2026-11-15, merged as PR #1170 (892d5995). Cause shown by the wave 7 VM trace; the final build passed `dino-onboarding-keys` on the VM; `dino-reorder` not reached (see Not yet verified).*

**Touches:** `src/Dino/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`, `src/Dino/DinoReader.App/StoryLanguagesPage.cs`, `src/Dino/DinoReader.Core/DinoKeyMap.cs`, `tools/desktop-vm/checks/dino-onboarding-keys.ps1`, `docs/plan/slices/391-dino-windows-first-run-enter-on-story-languages.md`, `tools/desktop-vm/checks/dino-reorder.ps1` (only if it must change)

## Why

The wave 6 VM run (2026-11-15) on Slice 382's build failed the prerequisite check `dino-onboarding-keys` twice: "Enter on the story-languages step did not open the book picker (waited 30 s)". The window trace says "Dino keyboard: Enter not used (FirstRunPage, no target)". Logs: `D:/ReadTheStupidText-worktrees/reviews/wave6-device/vm-reorder.log` and `vm-reorder2.log`. Slice 382 changed no first-run code; it is only the context (`plan-not-needed`). Because of this failure `dino-reorder` never ran on the VM, so Slice 382's Windows check is open.

## Work

- [x] 1. Run `dino-onboarding-keys` on main's build to tell whether main has the failure, and read `DinoKeyboard` and `FirstRunPage` to find why Enter has no target (file:line).
- [x] 2. Fix it on the Windows head (the keyboard path is Windows-only), with the VM check passing.
- [ ] 3. Then run `dino-reorder` on the VM (Slice 382's open check, [slice](382-dino-reorder-held-book-wiggles-plain-drag.md)).

## Verification

`dino-onboarding-keys` and `dino-reorder` pass on the VM.

## Cause (shown by the VM trace of main's build)

Not "Enter has no target": the story-languages page is a target (`OnboardingSteps.Languages.cs:171-185`, screen `FirstBooks`, `DinoKeyMap.cs:134-139`). The trace `reviews/wave7-device/391-main-copyout/crash-evidence/trace-tail-window-trace.log.txt` (main's build) reads `Space used (OnboardingLanguagesPage, FirstBooks)`, then `Space not used (OnboardingLanguagesPage, FirstBooks)`, then `Enter not used (FirstRunPage, no target)`. So a tick rebuilds the rows (`StoryLanguagePicker.Rebuild`, called by `ToggleAsync`), the focused row button is destroyed, and keyboard focus is lost: the next Space finds no focused row (`ToggleFocusedRow` returns false), and the step is gone before Enter. Main has the same code, so main has the failure. Which control WinUI focuses after the loss is not shown; that the focus is lost is.

## Fix (Windows head)

- `StoryLanguagesPage.cs` `StoryLanguagePicker.Rebuild` remembers which row's button had focus and refocuses that language's new button afterwards (`RefocusRow`), so a keyboard, mouse, touch or Enter tick all keep the focus; `ToggleFocusedRow` has no refocus call of its own.
- `RefocusRow` checks the result of `Focus()`: on false it tries once more 100 ms later (`RetryFocusWhenLoaded`; a delay, not a `Loaded` subscription, which `LifecycleSubscriptionTests` forbids on a Button), and writes one trace line naming the row and the result ("story language row <code> refocus [(retry)] ok|failed", Windows only).
- A first version also swallowed unused Space and Enter in `DinoKeyboard.cs` (`SwallowStrayOnboardingKey`); removed after review: it rested on an unproven cause and was too wide.
- VM proof owed: `dino-onboarding-keys` passes and the trace shows "refocus ok" after each tick.
- `DinoKeyboard.cs` is unchanged against main.

## VM evidence (wave 7)

- Main's build `4b816c867be8` fails `dino-onboarding-keys`: "Space used", "Space not used (OnboardingLanguagesPage, FirstBooks)", "Enter not used (FirstRunPage, no target)" (`reviews/wave7-device/391-main-copyout/crash-evidence/`).
- The first design's build `6e51827a8c7a` passed it (`reviews/wave7-device/391-fix.log`).
- The final build `50c83d7c6389` has not run on the VM: host free memory was 6.6-7.7 GB, under the 8 GB the VM needs.

*Changed while building:* the first design also swallowed unused Space and Enter in `DinoKeyboard.cs` (`SwallowStrayOnboardingKey`); removed after the high review. `Rebuild` now restores focus for every tick path instead of only the keyboard one.

*From review:* high (`reviews/pr-1170-high.md`) findings 1-6 applied as one design (focus kept in `Rebuild`, the swallow removed, `Focus()` result checked with one retry and a trace line); 7 settled by the VM run of the first-design build. Medium (`reviews/pr-1170-medium.md`) none applied:
- Skipped #1 (`reviews/pr-1170-medium.md`): "forces focus onto the new button" - `Focus()` runs only when a row had focus, the same on Android and iOS (no keyboard focus on touch, so no effect).
- Skipped #2 (`reviews/pr-1170-medium.md`): "can steal focus after Tab" - low, no failure path shown.
- Skipped #3 (`reviews/pr-1170-medium.md`): "Remember the last focused code in a field" - low, no failure path shown.
- Skipped #4 (`reviews/pr-1170-medium.md`): "Timing-dependent; VM proof still owed" - low, no failure path shown.
- Skipped #5 (`reviews/pr-1170-medium.md`): "Root cause unconfirmed" - the VM trace on main shows the focus loss.
- Skipped #6 (`reviews/pr-1170-medium.md`): "Pass the toggled code to Rebuild explicitly" - low, no failure path shown.

#### For CLAUDE.md (lifted 2026-11-15)

- `StoryLanguagePicker.Rebuild` (`StoryLanguagesPage.cs`) replaces the rows and must restore keyboard focus to the row that had it; without it a tick drops focus on Windows and the next Space or Enter has no target. Don't re-add a key swallow in `DinoKeyboard.cs` for this. (Slice 391: docs/plan/slices/391-dino-windows-first-run-enter-on-story-languages.md)

#### Not yet verified

- `dino-reorder` on the VM (item 3; Slice 382's open Windows check). Wave 7 run (final build `50c83d7c6389`, log `D:/ReadTheStupidText-worktrees/reviews/wave7-device/391-accept.log`): `dino-onboarding-keys` PASSED ("Dino keyboard: story language row sv refocus ok" after each tick), but the prerequisite `dino-launch` then failed "the shelf did not show (waited 30 s)" because the app was left on onboarding step 5 of 6 (Record Pim's lines), so `dino-reorder` stopped with "SETUP FAILURE: need two books on the shelf, found 0". Likely a harness gap: `-Check dino-reorder` runs onboarding-keys as a prerequisite, which leaves first run unfinished; a fix is to finish or skip the onboarding before `dino-launch` (tools/desktop-vm), not app code.
