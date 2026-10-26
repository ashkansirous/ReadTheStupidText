# Slice 141 — RTST Windows: a wait indicator after 500 ms

*Planned. Starts after Slice 140.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.App/WaitFlyout*` (new), `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `src/ReadTheStupidText.App/Tray*`, `src/ReadTheStupidText.App/Strings/**`, `src/ReadTheStupidText.Application/Reading/WaitStage.cs`, `tests/ReadTheStupidText.Tests/**`

## Why

Same as Slice 138: a hotkey or selection read that waits on a translation-pack download, the Marian
translation or the first synthesis gives no sign of life today unless the control panel is open.

## Decisions

- Decision 146 applies: after 500 ms show the stage text, progress where known, and Cancel; hide on
  first audio. Reuse the pure `WaitStage` timer model from Slice 138.
- **Open point, default:** reads started from the hotkey or a selection have no window, so the
  indicator is a small flyout by the tray clock (the existing tray-anchored placement), plus the
  control panel's `ProgressRing` area when the panel is open.

## Work

- [ ] Flyout by the tray with stage, progress and Cancel; the control panel shows the same state.
- [ ] Stages: translation-pack download, translate, first synthesis, mid-read stall.
- [ ] `wait_shown` / `wait_cancelled` events (Slice 138 set) from Windows.

## Verification

- Packaged dev run (`device-test`): first translation to a language without its pack shows the
  download stage and Cancel stops it; a quick read shows nothing.
