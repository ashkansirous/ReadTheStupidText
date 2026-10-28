# Slice 170 — Tray panel: every Controls button fits

*Built 2026-10-27, not yet verified on the desktop (no screen tests until Slice 163).* Read The Stupid Text, Windows tray control panel only (no Android/iOS surface: the phone app has no Controls row).

**Touches:** `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml.cs`, `src/ReadTheStupidText.App/WaitFlyoutWindow.xaml.cs`,
`site/img/control-panel.webp`

## Why

Found while taking the website screenshots (2026-10-27): the fifth button in the panel's Controls row (Translate to my
language) is cut off at the right edge, on the idle and the reading panel alike. The panel is set to `LogicalWidth = 376`
(`ControlPanelWindow.xaml.cs:45`) but captured 362 px wide at 100% scale, and since the Translate toggle and the "Help improve
the app" row were added the row no longer fits. The other rows (pickers, buttons) end inside the window, so the Controls grid is
laid out wider than the window, or the window ends up narrower than the layout expects.

## Decisions (user, 2026-10-27: "redesign and fix them")

- **Find the root cause first** (window width vs. client area at 100/125/150% scale, the toggle style's sizing, or the root
  layout's width), then fix that rather than nudging margins.
- **Redesign rule:** every control in the panel is fully visible at the panel's width, at 100%, 125%, 150% and 200% display
  scale, in English and the longest UI language (Finnish/Portuguese labels elsewhere in the panel must not push it wider).
  The five Controls toggles share the row evenly; if five squares cannot keep a comfortable size, they shrink (icons stay
  24 px) rather than overflow; no horizontal scrolling and no hidden toggle.
- Keep the panel's look (the Claude Design mock in `docs/design/`): same toggle style, same order.

## Work

- [x] Cause (from code and the 362 vs 376 px capture; no desktop run yet): `AppWindow.Resize` sets the OUTER window size, so the
      frame (about 7 px a side at 100%) shrank the client below the 376 DIP root and clipped the last toggle. Not yet measured at
      100/125/150% (Not yet verified).
- [x] Fix the cause: one placement routine (`WindowPlacer`, pure parts in `PanelPlacement`) used by the panel and `WaitFlyoutWindow`: target point (saved, else the primary work area corner), that monitor's DPI, `ResizeClient` to the rounded-up logical size (width guarded, else the 376 design width), clamp to that work area, one Move only if the position changes.
- [ ] Make the Controls row fit by design (rule above), including when the Translate column is hidden.
      Follow-up, still open: the window-size fix above does not measure or assert that the row fits within 376 DIP (long labels, hidden Translate column).
- [ ] Retake the website picture (`site/img/control-panel.webp`, the reading panel, window only) once it fits.

## Verification

- `dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64`
- `device-test` on the Windows desktop: panel idle and reading, at 100% and 150% scale, English and Finnish: all five toggles
  and the close button fully visible, nothing clipped at any edge (screenshots of the window only).

## Not yet verified

- Device check at 100% and 150% scale, idle and reading, English and Finnish: all five toggles and the wait flyout fully visible.
- Retake `site/img/control-panel.webp` (open work item above), and the redesign rule at 200% scale with the Translate column hidden.

#### For CLAUDE.md (lifted 2026-10-27)

- Tray panel and flyout placement goes through `WindowPlacer` with `ResizeClient` (client-area sizing), never by sizing the outer window. (Slice 170: docs/plan/slices/170-tray-panel-controls-row-fits.md)
