# Slice 24 — Draggable, position-persisted control panel

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 24 — Draggable, position-persisted control panel.** ([#106](https://github.com/ashkansirous/ReadTheStupidText/issues/106)) (Decision 31) Make
      the borderless control panel draggable by its header (pointer-drag → `AppWindow`
      move) and persist the last position in `ISettingsStore` so it reopens in place;
      keep pinned-topmost / no light-dismiss.
      **Built:** pointer handlers on `HeaderBorder` move the `AppWindow` by the raw
      screen-cursor delta (`GetCursorPos`, not element-relative coords — those jitter as
      the window moves); on capture-lost the final position is saved. New
      `PanelPosition(X, Y)` record + `ISettingsStore.PanelPosition` (device pixels, two
      `PanelX`/`PanelY` keys in `LocalSettingsStore`); `ISettingsStore` threaded into the
      panel via `MainWindow`. `PositionPanel` restores the saved spot **clamped to the
      work area** (`ClampToWorkArea` + `DisplayArea.GetFromPoint`, so an offscreen/moved-
      monitor point is pulled back on-screen), falling back to the default bottom-right
      pin only when never moved. Child controls mark their pointer input handled, so a
      drag only starts on the header's empty areas; pinned-topmost / no-light-dismiss
      kept. UI/native code isn't unit-tested per the project's test story; runtime check
      under the (Package) profile remains.

## Verification (moved from plan.md)

- **Slice 24:** open the control panel, drag it by the header to a new spot → it
  moves; close and reopen → it reappears in the moved position. It still stays
  pinned-topmost and does not light-dismiss.
