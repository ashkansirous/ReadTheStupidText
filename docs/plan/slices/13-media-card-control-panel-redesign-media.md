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

## Notes moved from CLAUDE.md (2026-10-21)

As of **Slice 13 (Decisions 20–21)** the panel is the **"Media Card"** design
(`design_handoff_tray_panel/`): a brand-gradient header (glyph watermark, `NOW
READING` eyebrow, title, a live 5-bar **waveform** + dynamic status text, and a
transport row = 40px play/pause circle + **live progress bar** + speed pill) over
a compact Fluent body and a `Ctrl+Win+R` hotkey footer. **Compact rev (post-Slice
13):** the body no longer stacks full-width labelled rows — the **fine 0.05 speed
slider is hidden in the header** and only revealed when the speed pill (now a
`ToggleButton` carrying a flipping chevron) is tapped (the panel re-fits its height
on reveal). **Design refresh (post-Slice 13):** revealing the speed control now
shows the slider **plus a row of six quick-preset chips** (0.5x / 1x / 1.25x / 1.5x
/ 1.75x / 2x, `SpeedPresetStyle`); tapping a chip drives the slider, and the chip
matching the current rate is highlighted (solid-white fill + blue text) from code
(`ApplyPresetVisual`/`UpdatePresetHighlight`) — slider and chips stay in sync. The
body is a single **voice picker** row (the `ComboBox` now **stretches full width** +
a 34px **activity-log button** that opens `ActivityLogWindow` via the panel's
`ActivityLogRequested` event) above a `CONTROLS` row of **three square icon
toggles** — auto-read on selection, auto-read on copy, launch-at-startup — each a
styled `ToggleButton` (off = card fill + muted icon, on = accent fill + white icon
via its `CheckStates`), now **70px tall (radius 11)** with **larger stroked line
icons**, and a hover tooltip naming the control and its on/off state. The toggle
icons are **stroked `Path` shapes** (the design's source SVGs: a select-frame and
stacked sheets, each enclosing a two-wave sound glyph; launch-at-startup is a
**rocket** — Segoe Fluent has no rocket glyph, and the design deliberately avoids
the power symbol); their stroke colour is the toggle's icon colour, set in code via
`ApplyToggleVisual`. Built with
native WinUI Fluent controls + light/dark `ThemeDictionaries` from the design
tokens (the HTML is the visual source of truth, not shipped code); it **keeps** the
pinned-topmost behavior (no click-away/Esc dismiss). The progress bar is driven by
`ISpeechReader.ProgressChanged` (a 0..1 read-through fraction from `MediaPlayer`
position + chunk index, weighting chunks equally) and is **display-only** — true
scrubbing/seek is out of scope (Decision 21); the status line is composed in the
View from `ReadAloudService.CurrentReadWindow`/`CurrentReadTrigger`.

