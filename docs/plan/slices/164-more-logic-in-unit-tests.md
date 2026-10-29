# Slice 164 — Move more logic out of UI code into unit tests

*Built 2026-10-27 (first batch; follow-ups listed below), not verified on a device (pure moves, unit-tested).* Batch 36 (fast device tests), RTST (Windows and Mobile) and Dino Reader. Decision 165.

**Touches:** `src/ReadTheStupidText.App/ControlPanelWindow.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`,
`src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/DinoReader.App/GatePage.xaml.cs`,
`src/DinoReader.App/StorePage.xaml.cs`, new files in `src/ReadTheStupidText.Application/Reading/` and `/Input/`
(`ReadStatusLine`, `PlayButtonFace`, `PendingReadSettle`, `PanelPlacement`), `Reading/VoiceRouting.cs`,
new `src/DinoReader.Core/GateKeypad.cs` and `ChoiceLabels.cs`, `tests/ReadTheStupidText.Tests/ControlPanelLogicTests.cs`,
`tests/DinoReader.Tests/GateKeypadTests.cs`.

## Why

Every decision that lives in a code-behind or a platform class can only be checked on a device or the desktop,
which is the slow path. Pure logic in Application/Domain/Core is checked by `dotnet test` in seconds
(the root CLAUDE.md, *Tests*).

## Work

- [ ] **Audit first** (a `ground-in-code` pass): list the branching logic in code-behinds, `Windows.Tray`, and the
  Android/iOS platform classes that has no test. For example: which read path to use, when a control is enabled,
  pack state to UI state, hotkey and selection rules. Rank the items by how often a device test has caught a bug
  there (slice files' review notes). Write the list into this file and narrow **Touches**.
- [ ] For each item on the list, top first, in its own sub-PR (so it can run beside other work): move the decision
  into a pure class in Application (or Core for Dino) and keep the UI code as a thin caller. Then add xUnit tests,
  and delete the device-test step it replaces from the slice checklists that named it.
- [ ] Stop when what's left needs a real device (audio, OS integration, layout).

## Verification

- `dotnet test` for both suites passes, with the new tests named in this file.
- The device checklists in Slices 161-163 shrink by the steps moved into unit tests (listed here).
- No behaviour change: the Slice 162 flows and the Slice 163 checks still pass.

## Audit and first batch (built)

Most decisions (voice routing, wait stages, early start, gallery, store filter, gate challenge) were already pure.
Moved now, each a thin caller plus xUnit tests (`ControlPanelLogicTests`, `GateKeypadTests`), no behaviour change:

- Control panel status line (`ReadStatusLine`), play slot spinner/cancel face (`PlayButtonFace`), keep-on-screen clamp (`PanelPlacement`).
- Reader: when a started read's "first audio pending" state ends (`PendingReadSettle`).
- Voice picker: which row is highlighted (`VoiceRouting.SelectedRow`).
- Dino: gate number pad (`GateKeypad`), store author action-sheet labels (`ChoiceLabels`).

Follow-up (not done, to keep the PR readable): speed-preset tag parsing and highlight in `ControlPanelWindow` and
`SettingsPage` (two copies); `ReaderPage.ApplyScanAvailability` and empty-player rules; Dino `ReadingPage`,
`PagesStep`, `GalleryPage` and `StoryWizardPage` branching; `MainWindow`/`App.xaml.cs` tray wiring. The rest needs a
device (audio, layout, OS integration). No device-test step in Slices 161-163 named these decisions, so none was deleted.

## Built

Seven moves into pure classes with thin callers: `ReadStatusLine`, `PlayButtonFace`, `PendingReadSettle`, `VoiceRouting.SelectedRow` (Application/Reading), `PanelPlacement.ClampToWorkArea` (Application/Input), `GateKeypad` and `ChoiceLabels.Unique` (DinoReader.Core). New tests: `ControlPanelLogicTests.cs`, `GateKeypadTests.cs`. Behaviour unchanged.

*Checks:* RTST 1381 pass / 2 skip; Dino 1555 pass; Windows x64, Android and iOS builds of both MAUI apps green.

*From review:* high pass 7 findings, all low (skipped: style/simplification); medium pass 6 findings (applied 6: line endings restored; the rest low, skipped).

#### For CLAUDE.md (lifted 2026-10-27)

- `src/DinoReader.Core/CLAUDE.md`: Core now also holds small pure UI-decision helpers (`GateKeypad`, `ChoiceLabels`), not only dialogue. (Slice 164: docs/plan/slices/164-more-logic-in-unit-tests.md)

#### Not yet verified

- None needed (pure moves, unit-tested); no device run.

#### Follow-up built 2026-10-29

Done (pure class plus thin callers, tests in `SpeedPresetEmptyPlayerTests.cs` and `ReadingChoicesTests.cs`, behaviour unchanged):

- [x] Speed-preset tag parsing and highlight: `SpeedPreset` (Application/Reading), used by `ControlPanelWindow` and the phone `SettingsPage`.
- [x] Phone reader empty-player rule: `EmptyPlayer.Is` (Application/Reading) in `ReaderPage`.
- [x] Dino `ReadingPage`: `LanguagePick` (switchable, effective and shown language) and `ReadingLayoutRules` (layout shape, mode fold); `GalleryPage`: `LanguagePick.ShelfShown`; `StoryWizardPage`: `WizardSteps` (Back/Next face and enabled rule). All in DinoReader.Core.
- Skipped: `ReaderPage.ApplyScanAvailability` (one bool driving layout calls, no decision left), `PagesStep` (picker, thumbnail and platform code, no pure rule), Windows `MainWindow`/`App.xaml.cs` tray (single-condition menu wiring; `SpeedPresets.All.Contains` already pure).
