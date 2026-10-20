# Slice 13 — "Media Card" control-panel redesign + media-player

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 13 — "Media Card" control-panel redesign + media-player
      progress.** ([#52](https://github.com/ashkansirous/ReadTheStupidText/issues/52)) (Decisions 20, 21) Rebuild `ControlPanelWindow` to the
      `design_handoff_tray_panel/` spec with native WinUI Fluent controls and
      light/dark theme resources: gradient header + glyph watermark + eyebrow/
      title, animated waveform + dynamic status text, transport row (play/pause
      circle + **live progress bar** + speed pill), Fluent settings list (voice
      row, the two auto-read toggles, launch-at-startup), hotkey footer. Keep the
      pinned-topmost `AppWindow` (Decision 12 — no click-away dismiss). Wire the
      progress bar to `MediaPlayer` position + chunk completion; seek is
      best-effort (chunk-boundary resync) only.

## Verification (moved from plan.md)

- **Slice 13:** left-click the tray → the panel matches the "Media Card" design
  (gradient header, glyph watermark, waveform, transport row, settings list,
  hotkey footer) in both light and dark system themes, ~376 px wide, no clipped
  content. Start a read → the **progress bar advances** and status text shows the
  source ("Reading selection from Notepad…"); pause → bar/waveform stop, status
  shows `Paused`; idle → `Ready`. The panel stays pinned when you click into
  another app (no click-away dismiss). Speed pill + slider, voice row, the two
  auto-read toggles, and startup all still drive their services.
